using Dissertation.Helpers;
using System;
using System.Collections.Generic;

namespace Dissertation.Models.Database
{
    public class StudentRoute
    {
        public TimeSpan StartTime { get; set; }

        public Route Route { get; set; }

        public List<TimedEdge> EdgeDurations { get; set; }

        public StudentRoute()
        {
        }

        public StudentRoute(TimeSpan startTime)
        {
            StartTime = startTime;
        }

        public void CalculateEdgeDurations()
        {
            // Initialise the list of durations.
            EdgeDurations = new List<TimedEdge>(Route.RouteNodes.Count);

            double currentDurationSeconds = 0;

            TimeSpan currentDuration = new TimeSpan();
            for (int nodeIndex = 0; nodeIndex < Route.RouteNodes.Count - 1; nodeIndex++)
            {
                Node thisNode = Route.RouteNodes[nodeIndex];
                Node nextNode = Route.RouteNodes[nodeIndex + 1];

                TimedEdge thisEdge = new TimedEdge(thisNode, nextNode, currentDuration);

                double edgeDistance;
                // Calculate distance to next node.
                if (thisNode.Type != NodeType.Corridor || nextNode.Type != NodeType.Corridor)
                {
                    // One of the nodes isn't a corridor so we can't calculate distance. This edge is between stair or lift node(s).
                    // Use a constant distance for traversal.
                    edgeDistance = SharedFunctions.NonCorridorDistance;
                }
                else
                {
                    // We can calculate a distance.
                    edgeDistance = thisNode.DistanceInMetersTo(nextNode);
                }
                currentDurationSeconds += SharedFunctions.CalculateWalkingTimeNoRounding(edgeDistance);

                // Get a timespan from this duration.
                currentDuration = TimeSpan.FromSeconds(currentDurationSeconds);
                // Set exit time for this edge.
                thisEdge.ExitTime = currentDuration;

                // Add to list.
                EdgeDurations.Add(thisEdge);
            }
        }
    }

    public class TimedEdge
    {
        public Node EntryNode { get; }

        public Node ExitNode { get; }

        public string EntryNodeId { get { return EntryNode.NodeId; } }

        public TimeSpan EntryTime { get; set; }

        public string ExitNodeId { get { return ExitNode.NodeId; } }

        public TimeSpan ExitTime { get; set; }

        public TimedEdge(Node entryNode, Node exitNode, TimeSpan entryTime)
        {
            EntryNode = entryNode;
            ExitNode = exitNode;
            EntryTime = entryTime;
        }
    }
}