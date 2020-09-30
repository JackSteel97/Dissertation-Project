using Dissertation.Models.Database;
using System;
using System.Collections.Generic;

namespace Dissertation.Helpers
{
    public class TimetableGenerator
    {
        private readonly Random Rand;
        private readonly DateTime MinDate;
        private readonly DateTime MaxDate;
        private readonly TimeSpan MinTime;
        private readonly TimeSpan MaxTime;
        private readonly int MaxDurationHours;
        private readonly string[] AllRooms;

        public TimetableGenerator(DateTime minDate, DateTime maxDate, TimeSpan minTime, TimeSpan maxTime, int maxEventDurationHours, string[] allRoomIds)
        {
            Rand = new Random();
            MinDate = minDate;
            MaxDate = maxDate;
            MinTime = minTime;
            MaxTime = maxTime;

            // Add one because it is exclusive end of range.
            MaxDurationHours = maxEventDurationHours + 1;
            AllRooms = allRoomIds;
        }

        /// <summary>
        /// Generate student timetables for a given number of students.
        /// </summary>
        /// <param name="numberOfStudents">Number of student timetables to generate.</param>
        /// <returns>A list of timetable events randomly generated.</returns>
        public List<TimetableEvent> GenerateStudentTimetables(int numberOfStudents)
        {
            // Initialise a list with a decent capacity to start, avoiding lots of reallocations during generation.
            List<TimetableEvent> allEvents = new List<TimetableEvent>(numberOfStudents * 10);

            for (int i = 1; i <= numberOfStudents; i++)
            {
                allEvents.AddRange(GenerateTimetable(i));
            }

            return allEvents;
        }

        /// <summary>
        /// Generate a timetable for a single student.
        /// </summary>
        /// <param name="studentId">Student id to use for this student.</param>
        /// <returns>A list of timetable events for this student.</returns>
        private List<TimetableEvent> GenerateTimetable(int studentId)
        {
            List<TimetableEvent> outputEvents = new List<TimetableEvent>(20);

            DateTime currentDate = MinDate;

            // Iterate Days.
            while (currentDate <= MaxDate)
            {
                // Check the date is a weekday.
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Generate a random start time for the first event.
                    TimeSpan currentStartTime = MinTime.Add(TimeSpan.FromHours(Rand.Next(0, (int)(MaxTime - MinTime).TotalHours)));

                    // While we can start it?
                    while (currentStartTime < MaxTime)
                    {
                        // Get duration of this event.
                        int durationHours = Rand.Next(1, MaxDurationHours);

                        // Get end time from duration and start time.
                        TimeSpan currentEndTime = currentStartTime.Add(TimeSpan.FromHours(durationHours));

                        // Get random location.
                        string eventLocation = AllRooms[Rand.Next(0, AllRooms.Length)];

                        // Create the event.
                        outputEvents.Add(new TimetableEvent(studentId, currentDate, currentStartTime, currentEndTime, eventLocation));

                        // Get start time of next event.
                        currentStartTime = currentStartTime.Add(TimeSpan.FromHours(Rand.Next(0, Math.Abs((int)(MaxTime - currentEndTime).TotalHours * 2))));
                    }
                }

                // Skip ahead a random number of days, up to a week.
                currentDate = currentDate.AddDays(Rand.Next(1, 8));
            }

            return outputEvents;
        }
    }
}