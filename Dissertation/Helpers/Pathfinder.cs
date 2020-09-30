using Dissertation.Models.Database;
using Dissertation.Services;
using Microsoft.EntityFrameworkCore;
using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dissertation.Helpers
{
    public class Pathfinder
    {
        /// <summary>
        /// All nodes used for this pathfinder's graph.
        /// </summary>
        private readonly Dictionary<string, Node> AllNodes;

        /// <summary>
        /// Constructor for a pathfinder.
        /// </summary>
        /// <param name="dissDatabaseContext">Db context.</param>
        /// <exception cref="InvalidCastException"></exception>
        public Pathfinder(DissDatabaseContext dissDatabaseContext)
        {
            // Get all nodes from the database.
            AllNodes = dissDatabaseContext
                .Nodes
                .AsNoTracking()
                .Include(n => n.OutgoingEdges)
                    .ThenInclude(e => e.Node2)
                .Include(n => n.IncomingEdges)
                    .ThenInclude(e => e.Node1)
                .ToDictionary(n => n.NodeId);
        }

        /// <summary>
        /// Use the A* algorithm to calculate a route, adjusted for congestion.
        /// </summary>
        /// <param name="start">Start node id</param>
        /// <param name="end">End node id</param>
        /// <param name="allStudentRoutes">A collection of all student routes for today.</param>
        /// <param name="startingTime">The requested starting time of this route.</param>
        /// <returns>A route object with the calculated route</returns>
        /// <exception cref="Exception"></exception>
        public Route BuildAStar(string start, string end, StudentRoute[] allStudentRoutes, TimeSpan startingTime)
        {
            // Check if the start is the same as the end, or either the start or end are invalid nodes.
            if (start == end || !AllNodes.ContainsKey(start) || !AllNodes.ContainsKey(end))
            {
                // return an empty route
                return new Route();
            }

            // Get the start node object from its id.
            Node startNode = AllNodes[start];
            // Get the end node object from its id.
            Node endNode = AllNodes[end];

            // Find the closest corridor node from the end node - this is used by A* as the lat/long end point for heuristic function.
            // This is often one of the direct connections most cases, however we may need to look further into the graph for outlier cases.
            Node endCorridor = FindNextCorridorNode(endNode, AllNodes);

            // Initialise the frontier priority queue.
            FastPriorityQueue<AStarNode> frontier = new FastPriorityQueue<AStarNode>(AllNodes.Count);

            // Initialise a lookup table for the queue.
            Dictionary<string, AStarNode> queueTable = new Dictionary<string, AStarNode>();

            // Heuristic for A* algorithm - Calculate the distance between the given node and the end corridor, or default to 1 if the given node is not a corridor.
            float heuristicFunction(Node node) => node.Type == NodeType.Corridor || node.Type == NodeType.Parking ? (float)node.DistanceInMetersTo(endCorridor) : SharedFunctions.NonCorridorDistance;

            // Instantiate an AStarNode from the start node.
            AStarNode starter = new AStarNode(startNode, null, 0f);
            // Add to the queue table
            queueTable.Add(startNode.NodeId, starter);
            // Add to the priority queue.
            frontier.Enqueue(starter, heuristicFunction(startNode));

            // Loop until the priority queue is empty.
            while (frontier.Count > 0)
            {
                // Dequeue the lowest cost node.
                AStarNode current = frontier.Dequeue();
                // Get its true cost.
                float currentCost = current.TrueCost;

                // Check if the current node is the end corridor.
                if (current.Node.NodeId == endCorridor.NodeId)
                {
                    // Route found.
                    // Backtrack to build the route.
                    List<Node> route = BackTrack(current);
                    // Add end node as we only routed to the nearest corridor.
                    route.Add(endNode);
                    // Return the final route.
                    return new Route(route, current.TrueCost);
                }

                // Mark the current node as visited.
                current.MarkVisited();

                AStarNode currentNode = current;

                double distanceToThisPoint = 0;

                // Calculate the time spent getting to this point.
                while (currentNode.LeadingNode != null)
                {
                    // Check this is a corridor node.
                    if (currentNode.Node.Type == NodeType.Corridor && currentNode.LeadingNode.Node.Type == NodeType.Corridor)
                    {
                        // Get the distance to the leading node.
                        distanceToThisPoint += currentNode.Node.DistanceInMetersTo(currentNode.LeadingNode.Node);
                    }
                    else
                    {
                        distanceToThisPoint += SharedFunctions.NonCorridorDistance;
                    }

                    currentNode = currentNode.LeadingNode;
                }

                // Calculate the time taken to get to this point.
                double timeElapsed = SharedFunctions.CalculateWalkingTimeNoRounding(distanceToThisPoint);

                // Calculate the time of day from the elapsed time.
                TimeSpan currentTime = startingTime.Add(TimeSpan.FromSeconds(timeElapsed));

                // Calculate the occupancies at this point.
                Dictionary<(string entryId, string exitId), int> edgeOccupancies = CongestionHelper.CalculateEdgeOccupanciesAtTime(allStudentRoutes, currentTime);

                // Iterate through all edges of the current node.
                foreach (NodeEdge edge in current.Node.OutgoingEdges)
                {
                    // End node of edge.
                    Node edgeEnd = edge.Node2;

                    // Check if the node already exists in the queue table.
                    bool edgeEndAlreadyPresent = queueTable.TryGetValue(edgeEnd.NodeId, out AStarNode target);
                    // Check if we have already visited this node.
                    bool alreadyVisitedEdgeEnd = edgeEndAlreadyPresent && target.Visited;

                    // If we have already visited this node, skip it.
                    if (alreadyVisitedEdgeEnd)
                    {
                        continue;
                    }

                    float congestionMultiplier = CalculateCongestionMultiplier(edge, edgeOccupancies);

                    // Calculate the true cost of this edge.
                    float trueCost = currentCost + ((float)edge.Weight * congestionMultiplier);
                    // Calculate the heuristic cost of this node.
                    float heuristicCost = heuristicFunction(edgeEnd);

                    // Total the costs and use the congestion multiplier.
                    float combinedCost = trueCost + heuristicCost;

                    // Is the edge already in the queue?
                    if (edgeEndAlreadyPresent)
                    {
                        // Only use new route if it is better.
                        if (combinedCost < target.TrueCost)
                        {
                            // It is better, update the nodes in the queue.
                            target.Update(current, trueCost);
                            frontier.UpdatePriority(target, combinedCost);
                        }
                    }
                    else
                    {
                        // It is not in the queue, instantiate an AStarNode object.
                        target = new AStarNode(AllNodes[edgeEnd.NodeId], current, trueCost);
                        // Add to the queue table.
                        queueTable.Add(edgeEnd.NodeId, target);
                        // Add to the queue.
                        frontier.Enqueue(target, combinedCost);
                    }
                }
            }
            // No route could be found.
            return new Route();
        }

        /// <summary>
        /// Calculate the congestion multiplier for use in increasing the cost of an edge proportional to the congestion on that edge.
        /// </summary>
        /// <param name="edge">Edge to consider.</param>
        /// <param name="edgeOccupancies">The dictionary of edges and their occupancies at the current time.</param>
        /// <returns>A decimal multiplier.</returns>
        public static float CalculateCongestionMultiplier(NodeEdge edge, Dictionary<(string entryId, string exitId), int> edgeOccupancies)
        {
            (double adjustedTravelTime, double baseTravelTime) = CalculateWalkingTimeWithCongestion(edge, edgeOccupancies);

            // How much bigger is adjustedTravelTime than baseTravelTime?
            return Convert.ToSingle(adjustedTravelTime / baseTravelTime);
        }

        /// <summary>
        /// Calculate the time for a given edge, and the adjusted time accounting for congestion on that edge.
        /// </summary>
        /// <param name="edge">Edge to consider.</param>
        /// <param name="edgeOccupancies">The dictionary of edges and their occupancies at the current time.</param>
        /// <returns>A tuple of the time with congestion and time without congestion considered.</returns>
        public static (double timeWithCongestion, double timeWithoutCongestion) CalculateWalkingTimeWithCongestion(NodeEdge edge, Dictionary<(string entryId, string exitId), int> edgeOccupancies)
        {
            // Check if the edge is contained in the occupancies dictionary.
            if (!edgeOccupancies.TryGetValue((edge.Node1Id, edge.Node2Id), out int occupancy))
            {
                // Not in this way. Check for it the other way round.
                edgeOccupancies.TryGetValue((edge.Node2Id, edge.Node1Id), out occupancy);
            }
            // At this point occupancy is either default 0 or a value from the edgeOccupancies dictionary.

            double length;
            double area;
            double alpha = 1;

            // Check if we have a pre-calculated area for this edge.
            if (edge.CorridorArea.HasValue)
            {
                area = edge.CorridorArea.GetValueOrDefault();
            }
            else
            {
                area = SharedFunctions.NonCorridorArea;
            }

            // Check if we can calculate or get the distance of this edge.
            if (edge.Node1.Latitude.HasValue && edge.Node1.Longitude.HasValue && edge.Node2.Latitude.HasValue && edge.Node2.Longitude.HasValue)
            {
                // Get length of arc.
                if (edge.Node1.BuildingCode == edge.Node2.BuildingCode)
                {
                    // If the nodes are in the same building, the distance is just the weight of this edge.
                    length = edge.Weight;
                }
                else
                {
                    // We need to calculate it because it crosses building entrance/exit.
                    length = edge.Node1.DistanceInMetersTo(edge.Node2);
                }
            }
            else
            {
                length = SharedFunctions.NonCorridorDistance;
                if (edge.Node1.Type == NodeType.Stairs || edge.Node2.Type == NodeType.Stairs)
                {
                    // Stairs are much slower than flat plane walking.
                    alpha = 0.1;
                }
            }

            // Calculate the base travel time of this edge. This provides a lower bound when density is zero.
            double baseTravelTime = SharedFunctions.CalculateWalkingTimeNoRounding(length);

            // Calculate the crowd density on this edge.
            double crowdDensity = occupancy / area;

            // Calculate the adjusted travel time using the equation used by (Vermuyten et al., 2016).
            double adjustedTravelTime = ((length / alpha) * crowdDensity) + baseTravelTime;

            return (adjustedTravelTime, baseTravelTime);
        }

        /// <summary>
        /// Use the A* algorithm to calculate a route.
        /// <br />
        /// Average run time = 1.2ms
        /// </summary>
        /// <param name="start">Start node id</param>
        /// <param name="end">End node id</param>
        /// <returns>A route object with the calculated route</returns>
        /// <exception cref="Exception"></exception>
        public Route BuildAStar(string start, string end)
        {
            // Check if the start is the same as the end, or either the start or end are invalid nodes.
            if (start == end || !AllNodes.ContainsKey(start) || !AllNodes.ContainsKey(end))
            {
                // return an empty route
                return new Route();
            }
            // Get the start node object from its id.
            Node startNode = AllNodes[start];
            // Get the end node object from its id.
            Node endNode = AllNodes[end];

            // Find the closest corridor node from the end node - this is used by A* as the lat/long end point for heuristic function.
            // This is often one of the direct connections most cases, however we may need to look further into the graph for outlier cases.
            Node endCorridor = FindNextCorridorNode(endNode, AllNodes);

            // Initialise the frontier priority queue.
            FastPriorityQueue<AStarNode> frontier = new FastPriorityQueue<AStarNode>(AllNodes.Count);

            // Initialise a lookup table for the queue.
            Dictionary<string, AStarNode> queueTable = new Dictionary<string, AStarNode>();

            // Heuristic for A* algorithm - Calculate the distance between the given node and the end corridor, or default to 1 if the given node is not a corridor.
            float heuristicFunction(Node node) => node.Type == NodeType.Corridor || node.Type == NodeType.Parking ? (float)node.DistanceInMetersTo(endCorridor) : 1f;

            // Instantiate an AStarNode from the start node.
            AStarNode starter = new AStarNode(startNode, null, 0f);
            // Add to the queue table
            queueTable.Add(startNode.NodeId, starter);
            // Add to the priority queue.
            frontier.Enqueue(starter, heuristicFunction(startNode));

            // Loop until the priority queue is empty.
            while (frontier.Count > 0)
            {
                // Dequeue the lowest cost node.
                AStarNode current = frontier.Dequeue();
                // Get its true cost.
                float currentCost = current.TrueCost;

                // Check if the current node is the end corridor.
                if (current.Node.NodeId == endCorridor.NodeId)
                {
                    // Route found.
                    // Backtrack to build the route.
                    List<Node> route = BackTrack(current);
                    // Add end node as we only routed to the nearest corridor.
                    route.Add(endNode);
                    // Return the final route.
                    return new Route(route, current.TrueCost);
                }

                // Mark the current node as visited.
                current.MarkVisited();

                // Iterate through all edges of the current node.
                foreach (NodeEdge edge in current.Node.OutgoingEdges)
                {
                    // End node of edge.
                    Node edgeEnd = edge.Node2;

                    // Check if the node already exists in the queue table.
                    bool edgeEndAlreadyPresent = queueTable.TryGetValue(edgeEnd.NodeId, out AStarNode target);
                    // Check if we have already visited this node.
                    bool alreadyVisitedEdgeEnd = edgeEndAlreadyPresent && target.Visited;

                    // If we have already visited this node, skip it.
                    if (alreadyVisitedEdgeEnd)
                    {
                        continue;
                    }

                    // Calculate the true cost of this edge.
                    float trueCost = currentCost + (float)edge.Weight;
                    // Calculate the heuristic cost of this node.
                    float heuristicCost = heuristicFunction(edgeEnd);
                    // Total the costs.
                    float combinedCost = trueCost + heuristicCost;

                    // Is the edge already in the queue?
                    if (edgeEndAlreadyPresent)
                    {
                        // Only use new route if it is better.
                        if (combinedCost < target.TrueCost)
                        {
                            // It is better, update the nodes in the queue.
                            target.Update(current, trueCost);
                            frontier.UpdatePriority(target, combinedCost);
                        }
                    }
                    else
                    {
                        // It is not in the queue, instantiate an AStarNode object.
                        target = new AStarNode(AllNodes[edgeEnd.NodeId], current, trueCost);
                        // Add to the queue table.
                        queueTable.Add(edgeEnd.NodeId, target);
                        // Add to the queue.
                        frontier.Enqueue(target, combinedCost);
                    }
                }
            }
            // No route could be found.
            return new Route();
        }

        /// <summary>
        /// Helper method to get a route from an AStarNode by backtracking.
        /// </summary>
        /// <param name="lastNode">Node to backtrack from</param>
        /// <returns>A List of Nodes, making up a route in order from start to end</returns>
        private static List<Node> BackTrack(AStarNode lastNode)
        {
            List<Node> route = new List<Node>();

            while (lastNode != null)
            {
                route.Add(lastNode.Node);
                lastNode = lastNode.LeadingNode;
            }

            route.Reverse();
            return route;
        }

        /// <summary>
        /// Breadth First search the graph for a corridor node.
        /// </summary>
        /// <param name="node">Root node</param>
        /// <returns>Null if no corridor node can be found</returns>
        /// <exception cref="Exception">Thrown when the graph is possible disconnected and invalid.</exception>
        /// <exception cref="ApplicationException"></exception>
        private Node FindNextCorridorNode(Node node, Dictionary<string, Node> allNodes)
        {
            // This node is already a corridor.
            if (node.Type == NodeType.Corridor || node.Type == NodeType.Parking)
            {
                return node;
            }

            // Initialise a collection of node ids that have been visited.
            HashSet<string> visited = new HashSet<string>();
            // Initialise a queue.
            Queue<Node> Q = new Queue<Node>();
            // Add the root node.
            Q.Enqueue(node);
            // Mark root node as visited.
            visited.Add(node.NodeId);
            // Loop until the queue is empty.
            while (Q.Count > 0)
            {
                // Dequeue a node.
                Node vertex = Q.Dequeue();

                // Iterate through its edges.
                foreach (NodeEdge edge in vertex.OutgoingEdges)
                {
                    Node target = allNodes[edge.Node2Id];

                    // Have we already visited this node?
                    if (!visited.Contains(target.NodeId))
                    {
                        // No, enqueue it and mark as visited.
                        Q.Enqueue(target);
                        visited.Add(target.NodeId);
                    }

                    // Check if it is a corridor.
                    if (target.Type == NodeType.Corridor || target.Type == NodeType.Parking)
                    {
                        return target;
                    }
                }
            }

            // This should never happen as long as the rest of the system works properly.
            throw new ApplicationException($"A corridor node could not be found - The graph may be disconnected.{Environment.NewLine}Root Node: {node.NodeId}{Environment.NewLine}Check JSON validator and service error log.");
        }
    }

    /// <summary>
    /// A class for collating Route data in one object.
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Nodes in this route, ordered from start to end.
        /// </summary>
        public List<Node> RouteNodes { get; }

        /// <summary>
        /// Total cost of this route.
        /// </summary>
        public float TotalCost { get; }

        /// <summary>
        /// Total distance in meters this route covers.
        /// </summary>
        public double TotalDistance { get; private set; }

        public int WalkingTimeSeconds { get; private set; }

        public Dictionary<string, float> Congestion { get; private set; }

        public float MaxCongestion { get; private set; }

        /// <summary>
        /// Construct a new route with the given node list and total cost.
        /// </summary>
        /// <param name="route">List of nodes, ordered from start to end</param>
        /// <param name="cost">Total cost of this route</param>
        public Route(List<Node> route, float cost)
        {
            RouteNodes = route;
            TotalCost = cost;
            CalculateTotalDistance();
            CalculateWalkingTime();
        }

        /// <summary>
        /// Construct an empty route.
        /// </summary>
        public Route()
        {
            RouteNodes = new List<Node>();
            TotalCost = -1;
            TotalDistance = -1;
        }

        /// <summary>
        /// Method to calculate the total distance of this route.
        /// </summary>
        /// <returns></returns>
        private void CalculateTotalDistance()
        {
            double total = 0;

            // Iterate through the nodes up until the second from last one - because we access the one in front of the current iteration too.
            for (int i = 0; i < RouteNodes.Count - 1; i++)
            {
                // Check that both this node and the next have valid latitude and longitude values.
                if (RouteNodes[i].Type == NodeType.Corridor && RouteNodes[i + 1].Type == NodeType.Corridor)
                {
                    // They do, a distance can be calculated from these.
                    total += RouteNodes[i].DistanceInMetersTo(RouteNodes[i + 1]);
                }
                else
                {
                    // They don't this edge connects over a stair or lift. Use the constant distance instead.
                    total += SharedFunctions.NonCorridorDistance;
                }
            }
            TotalDistance = total;
        }

        /// <summary>
        /// Calculate the walking time for the total distance of this route.
        /// </summary>
        private void CalculateWalkingTime()
        {
            WalkingTimeSeconds = SharedFunctions.CalculateWalkingTime(TotalDistance);
        }

        /// <summary>
        /// Calculate the adjusted walking time for this route, taking congestion into account.
        /// </summary>
        /// <param name="allStudentRoutes">A collection of all student routes today.</param>
        /// <param name="startingTime">The starting time of this route.</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AggregateException"></exception>
        public void CalculateAdjustedWalkingTime(StudentRoute[] allStudentRoutes, TimeSpan startingTime)
        {
            double distanceSoFar = 0;
            double totalAdjustedWalkingTime = 0;

            List<Dictionary<(string entryId, string exitId), int>> allCongestionValues = new List<Dictionary<(string entryId, string exitId), int>>(RouteNodes.Count);

            for (int i = 0; i < RouteNodes.Count - 1; i++)
            {
                // Increment the distance walked so far.
                if (RouteNodes[i].Type == NodeType.Corridor && RouteNodes[i + 1].Type == NodeType.Corridor)
                {
                    distanceSoFar += RouteNodes[i].DistanceInMetersTo(RouteNodes[i + 1]);
                }
                else
                {
                    distanceSoFar += SharedFunctions.NonCorridorDistance;
                }

                // Calculate the time spent getting to this point and get the current time of day from that.
                TimeSpan currentTime = startingTime.Add(TimeSpan.FromSeconds(SharedFunctions.CalculateWalkingTimeNoRounding(distanceSoFar)));

                // Calculate the occupancies at this point.
                Dictionary<(string entryId, string exitId), int> edgeOccupancies = CongestionHelper.CalculateEdgeOccupanciesAtTime(allStudentRoutes, currentTime);

                if (edgeOccupancies.Count > 0)
                {
                    // Add to running count for heatmap congestion calculation later.
                    allCongestionValues.Add(edgeOccupancies);
                }

                // Get the edge object.
                NodeEdge edge = RouteNodes[i].OutgoingEdges.Find(e => e.Node2.NodeId == RouteNodes[i + 1].NodeId);
                if (edge != default)
                {
                    // Increment the actual walking time.
                    totalAdjustedWalkingTime += Pathfinder.CalculateWalkingTimeWithCongestion(edge, edgeOccupancies).timeWithCongestion;
                }
            }

            // Congestion for heatmap display.
            Congestion = new Dictionary<string, float>();
            if (allCongestionValues.Count > 0)
            {
                foreach (Dictionary<(string entryId, string exitId), int> entry in allCongestionValues)
                {
                    foreach (KeyValuePair<(string entryId, string exitId), int> congestedEdge in entry)
                    {
                        string newKey = $"{congestedEdge.Key.entryId},{congestedEdge.Key.exitId}";

                        if (!Congestion.TryAdd(newKey, congestedEdge.Value))
                        {
                            // Already exists, add to it
                            Congestion[newKey] += congestedEdge.Value;
                        }
                    }
                }

                if (Congestion.Count > 0)
                {
                    MaxCongestion = Congestion.Values.Max();
                }
            }

            WalkingTimeSeconds = Convert.ToInt32(Math.Round(totalAdjustedWalkingTime));
        }
    }

    /// <summary>
    /// Class used to represent a node for the A* algorithm.
    /// Must inherit from FastPriorityQueueNode to work with the priority queue data structure.
    /// </summary>
    internal class AStarNode : FastPriorityQueueNode
    {
        /// <summary>
        /// The node which leads to this node.
        /// Null if there is no leading node.
        /// </summary>
        public AStarNode LeadingNode { get; private set; }

        /// <summary>
        /// The Node this AStarNode is responsible for.
        /// </summary>
        public Node Node { get; }

        /// <summary>
        /// The total cost of the route up to this Node.
        /// </summary>
        public float TrueCost { get; private set; }

        /// <summary>
        /// Indicates whether this node has been visited yet.
        /// </summary>
        public bool Visited { get; private set; }

        /// <summary>
        /// Initialises an un-visited AStarNode.
        /// </summary>
        /// <param name="node">Node row from the database</param>
        /// <param name="leadingNode">Node leading to this one</param>
        /// <param name="trueCost">Cost up to this node</param>
        public AStarNode(Node node, AStarNode leadingNode, float trueCost)
        {
            Node = node;
            LeadingNode = leadingNode;
            TrueCost = trueCost;
            Visited = false;
        }

        /// <summary>
        /// Marks this node as visited.
        /// </summary>
        public void MarkVisited() => Visited = true;

        /// <summary>
        /// Updates the leading node and cost for this node.
        /// </summary>
        /// <param name="newLeadingNode">Node leading to this one</param>
        /// <param name="newTrueCost">New cost of the route up to this node</param>
        public void Update(AStarNode newLeadingNode, float newTrueCost)
        {
            LeadingNode = newLeadingNode;
            TrueCost = newTrueCost;
        }
    }
}