using Dissertation.Models.Database;
using Dissertation.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dissertation.Helpers
{
    public static class CongestionHelper
    {
        /// <summary>
        /// Generate all student routes from the timetable data.
        /// </summary>
        /// <param name="dissDatabaseContext">Database context.</param>
        /// <param name="pathfinder">Pathfinder to use.</param>
        /// <param name="memoryCache">The memory cache to use.</param>
        /// <param name="accommodations">The list of student accommodations that can be used as home destinations.</param>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="Exception"></exception>
        public static async Task<StudentRoute[]> GenerateAllStudentRoutes(DissDatabaseContext dissDatabaseContext, Pathfinder pathfinder, IMemoryCache memoryCache, string[] accommodations)
        {
            DateTime today = new DateTime(2020, 3, 2);

            string cacheKey = $"{today.ToString("yyyy.MM.dd")}.studentRoutes";

            // Get the routes from the cache if they exist there.
            if (memoryCache.TryGetValue(cacheKey, out StudentRoute[] studentRoutes))
            {
                return studentRoutes;
            }

            // Get all student timetables. Grouped by student id. An array of arrays. The inner array contains all of that students events.
            TimetableEvent[][] studentTimetables = (await dissDatabaseContext
                .TimetableEvents
                .AsNoTracking()
                .Where(te => te.EventDate == today)
                .OrderBy(te => te.EventDate)
                    .ThenBy(te => te.StartTime)
                .ToArrayAsync())
                .GroupBy(te => te.StudentId)
                .Select(g => g.ToArray())
                .ToArray();

            // Initialise a randomiser.
            Random random = new Random();

            ConcurrentBag<StudentRoute> allStudentRoutes = new ConcurrentBag<StudentRoute>();

            // Iterate through the student timetables in parallel.
            Parallel.ForEach(studentTimetables, studentTimetable =>
            {
                if (studentTimetable.Length > 0)
                {
                    // Route from a student accommodation to their first event. People tend to leave 10-15 minutes before a lecture.
                    StudentRoute studentRoute = new StudentRoute(studentTimetable[0].StartTime.Subtract(TimeSpan.FromMinutes(10)));
                    string thisStudentsAccommodation = $"c_{accommodations[random.Next(0, accommodations.Length)]}0_001";

                    // Get a route from the accommodation to the first event.
                    studentRoute.Route = pathfinder.BuildAStar(thisStudentsAccommodation, studentTimetable[0].LocationNodeId);
                    studentRoute.CalculateEdgeDurations();
                    allStudentRoutes.Add(studentRoute);

                    // Iterate through the events of this student.
                    for (int eventIndex = 0; eventIndex < studentTimetable.Length - 1; eventIndex++)
                    {
                        // Create a new student route from the end time of this event.
                        StudentRoute currentStudentRoute = new StudentRoute(studentTimetable[eventIndex].EndTime);

                        // Calculate time till start of next event.
                        double waitingTime = (studentTimetable[eventIndex + 1].StartTime - studentTimetable[eventIndex].EndTime).TotalMinutes;

                        // Get the leaving point and the point of their next event.
                        string startId = studentTimetable[eventIndex].LocationNodeId;
                        string endId = studentTimetable[eventIndex + 1].LocationNodeId;

                        // Check how long between events.
                        if (waitingTime > 45)
                        {
                            // Send them home first.
                            currentStudentRoute.Route = pathfinder.BuildAStar(startId, thisStudentsAccommodation);
                            currentStudentRoute.CalculateEdgeDurations();
                            allStudentRoutes.Add(currentStudentRoute);

                            // Then back to the lecture.
                            StudentRoute backToCampusRoute = new StudentRoute(studentTimetable[eventIndex + 1].StartTime.Subtract(TimeSpan.FromMinutes(10)));
                            backToCampusRoute.Route = pathfinder.BuildAStar(thisStudentsAccommodation, endId);
                            backToCampusRoute.CalculateEdgeDurations();
                            allStudentRoutes.Add(backToCampusRoute);
                        }
                        else
                        {
                            // Go straight to next event.
                            currentStudentRoute.Route = pathfinder.BuildAStar(startId, endId);
                            currentStudentRoute.CalculateEdgeDurations();

                            // Add to a list of all routes.
                            allStudentRoutes.Add(currentStudentRoute);
                        }
                    }

                    // Go home after the last event.
                    StudentRoute homeRoute = new StudentRoute(studentTimetable[^1].EndTime);

                    // Get a route from the last event to their accommodation.
                    homeRoute.Route = pathfinder.BuildAStar(studentTimetable[^1].LocationNodeId, thisStudentsAccommodation);
                    homeRoute.CalculateEdgeDurations();
                    allStudentRoutes.Add(homeRoute);
                }
            });

            // Convert to array.
            studentRoutes = allStudentRoutes.ToArray();

            // Cache today's routes.
            memoryCache.Set(cacheKey, studentRoutes);
            return studentRoutes;
        }

        /// <summary>
        /// Calculate the occupancy values for every edge in every route at a given time.
        /// </summary>
        /// <param name="allRoutes">All student routes today.</param>
        /// <param name="time">The given time to calculate the occupancy for.</param>
        /// <returns></returns>
        /// <exception cref="AggregateException"></exception>
        public static Dictionary<(string entryId, string exitId), int> CalculateEdgeOccupanciesAtTime(StudentRoute[] allRoutes, TimeSpan time)
        {
            Dictionary<(string entryId, string exitId), int> edgeOccupancies = new Dictionary<(string, string), int>();

            Parallel.ForEach(allRoutes, route =>
            {
                foreach (TimedEdge edge in route.EdgeDurations)
                {
                    TimeSpan entryTime = route.StartTime.Add(edge.EntryTime);
                    TimeSpan exitTime = route.StartTime.Add(edge.ExitTime);

                    // Is this route in this node at the given time?
                    if (time >= entryTime && time < exitTime)
                    {
                        // Yes.
                        // Closer to entry or exit?
                        double entryDiff = Math.Abs((time - entryTime).TotalSeconds);
                        double exitDiff = Math.Abs((time - exitTime).TotalSeconds);

                        List<(string entryNodeId, string exitNodeId)> edgesToUse;
                        if (entryDiff < exitDiff)
                        {
                            // Closer to entry.
                            edgesToUse = edge.EntryNode.IncomingEdges.ConvertAll(e => (e.Node1Id, e.Node2Id));
                        }
                        else
                        {
                            // Closer to exit.
                            edgesToUse = edge.ExitNode.IncomingEdges.ConvertAll(e => (e.Node2Id, e.Node1Id));
                        }

                        foreach ((string entryNodeId, string exitNodeId) possiblyOccupiedEdge in edgesToUse)
                        {
                            lock (edgeOccupancies)
                            {
                                // Add to occupancy counter.
                                if (!edgeOccupancies.TryAdd(possiblyOccupiedEdge, 1))
                                {
                                    // Already exists, increment.
                                    edgeOccupancies[possiblyOccupiedEdge]++;
                                }
                            }
                        }
                    }
                }
            });

            return edgeOccupancies;
        }
    }
}