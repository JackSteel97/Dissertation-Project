using Dissertation.Models;
using Dissertation.Models.Database;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dissertation.Helpers
{
    public static class NodeExtractor
    {
        /// <summary>
        /// Calculate the weights of each edge.
        /// </summary>
        /// <param name="nodes">A list of all nodes involved</param>
        /// <param name="edges">A list of all edges to weigh</param>
        /// <returns>A list of edges with correct weights</returns>
        public static List<NodeEdge> CalculateWeights(List<Node> nodes, List<NodeEdge> edges, EdgeCaseWeightsConfiguration edgeCases)
        {
            // Iterate through edges
            foreach (NodeEdge edge in edges)
            {
                // Default weight is high to avoid routing weird ways - any arbitrary high number will do.
                double weight = 999999;
                double? area = null;

                // Lower weight for stairs to prioritise them when changing floors.
                if (edge.Node1Id.StartsWith("s_") || edge.Node2Id.StartsWith("s_"))
                {
                    weight = 888888;
                }

                // Get nodes for each side of the edge.
                Node node1 = nodes.Find(n => n.NodeId == edge.Node1Id);
                Node node2 = nodes.Find(n => n.NodeId == edge.Node2Id);

                double distance;
                if (node1 != null && node2 != null)
                {
                    if (node1.Latitude.HasValue && node2.Latitude.HasValue && node1.Longitude.HasValue && node2.Longitude.HasValue)
                    {
                        // Calculate weight from physical distance.
                        distance = SharedFunctions.GetDistanceFromLatLonInMeters(node1.Latitude.GetValueOrDefault(), node1.Longitude.GetValueOrDefault(), node2.Latitude.GetValueOrDefault(), node2.Longitude.GetValueOrDefault());
                        weight = distance;
                    }
                    else
                    {
                        distance = SharedFunctions.NonCorridorDistance;
                    }

                    if (node1.CorridorWidth != null && node2.CorridorWidth != null)
                    {
                        // Calculate average area between points.
                        // Get average width.
                        double width = (node1.CorridorWidth.GetValueOrDefault() + node2.CorridorWidth.GetValueOrDefault()) / 2;
                        // Calculate area.
                        area = width * distance;
                    }
                }

                // Handle special cases.
                // Iterate through either node list.
                foreach (EitherNodeEntry item in edgeCases.EitherNode)
                {
                    if (NodeIdStartsWith(edge.Node1Id, item.StringStart) || NodeIdStartsWith(edge.Node2Id, item.StringStart))
                    {
                        weight = item.Weight;
                    }
                }

                // Iterate through both nodes list.
                foreach (BothNodesEntry item in edgeCases.BothNodes)
                {
                    if (NodeIdStartsWith(edge.Node1Id, item.Node1String) && NodeIdStartsWith(edge.Node2Id, item.Node2String))
                    {
                        weight = item.Weight;
                    }
                }

                edge.Weight = weight;
                edge.CorridorArea = area;
            }
            return edges;
        }

        /// <summary>
        /// Parse the given JOSM GeoJson output to extract nodes.
        /// </summary>
        /// <param name="collection">GeoJson from JOSM</param>
        /// <param name="nodes">Extracted nodes</param>
        /// <param name="edges">Extracted edges</param>
        /// <returns>True if successful</returns>
        public static bool ParseNodeSource(FeatureCollection collection, out List<Node> nodes, out List<NodeEdge> edges)
        {
            nodes = new List<Node>(100);
            edges = new List<NodeEdge>(200);

            try
            {
                List<(IPosition, IPosition)> CorridorPointPairs = new List<(IPosition, IPosition)>(200);
                List<Feature> features = collection.Features;

                if (features == null)
                {
                    return false;
                }

                // Iterate through features.
                foreach (Feature f in features)
                {
                    Node node = new Node();
                    if (f.Properties != null)
                    {
                        // Check for flag property. This is a room/stairs/lift/block node.
                        if (f.Properties.ContainsKey("NAV_FEATURE") && f.Properties.ContainsKey("node_type") && !string.Equals(f.Properties["node_type"].ToString(), "block", StringComparison.OrdinalIgnoreCase))
                        {
                            // We don't need to add block nodes as they are not used for navigation.
                            node.NodeId = f.Properties["id"].ToString().ToLower().Trim();
                            node.BuildingCode = f.Properties["building"].ToString().ToLower().Trim();
                            node.Floor = Convert.ToByte(f.Properties["level"].ToString().ToLower().Trim());
                            node.Latitude = null;
                            node.Longitude = null;
                            node.Type = SharedFunctions.GetNodeType(f.Properties["node_type"].ToString());
                            node.LeafletNodeType = f.Properties["node_type"].ToString().ToLower().Trim();

                            if (node.NodeId[0] == 'm')
                            {
                                node.Type = NodeType.Unrouteable;
                            }
                            // Check for a name.
                            if (f.Properties.ContainsKey("name") && f.Properties["name"] != null)
                            {
                                node.RoomName = f.Properties["name"].ToString();
                            }

                            // Check for connected nodes property.
                            if (f.Properties.ContainsKey("connected_nodes") && f.Properties["connected_nodes"] != null)
                            {
                                string[] cons = f.Properties["connected_nodes"].ToString().ToLower().Split(',');
                                foreach (string con in cons)
                                {
                                    edges.Add(new NodeEdge(node.NodeId, con.ToLower().Trim(), 999999));
                                }
                            }

                            nodes.Add(node);
                        }

                        // Is this feature a line string?
                        if (f.Geometry?.Type == GeoJSON.Net.GeoJSONObjectType.LineString)
                        {
                            LineString linestring = f.Geometry as LineString;
                            if (linestring.Coordinates.Count > 0)
                            {
                                // Iterate through the coordinates.
                                for (int i = 1; i < linestring.Coordinates.Count; i++)
                                {
                                    // Extract all coordinate pairs for later use matching against extracted corridor nodes.
                                    IPosition point1 = linestring.Coordinates[i - 1];
                                    IPosition point2 = linestring.Coordinates[i];
                                    CorridorPointPairs.Add((point1, point2));
                                }
                            }
                        }

                        // Check for flag property.
                        if (f.Properties.ContainsKey("NAV_CORRIDOR"))
                        {
                            // This is a corridor.
                            node.NodeId = f.Properties["id"].ToString().ToLower().Trim();
                            node.BuildingCode = f.Properties["building"].ToString().ToLower().Trim();
                            node.Floor = Convert.ToByte(f.Properties["level"].ToString().ToLower().Trim());
                            node.Latitude = ((Point)f.Geometry).Coordinates.Latitude;
                            node.Longitude = ((Point)f.Geometry).Coordinates.Longitude;
                            node.Type = SharedFunctions.GetNodeType(f.Properties["node_type"].ToString());
                            node.CorridorWidth = Convert.ToDouble(f.Properties["corridor_width"].ToString());
                            node.LeafletNodeType = f.Properties["node_type"].ToString().ToLower().Trim();

                            if (f.Properties.ContainsKey("name"))
                            {
                                node.RoomName = f.Properties["name"].ToString().Trim();
                            }

                            // Check for connected nodes property.
                            if (f.Properties.ContainsKey("connected_nodes") && f.Properties["connected_nodes"] != null)
                            {
                                string[] cons = f.Properties["connected_nodes"].ToString().ToLower().Split(',');
                                foreach (string con in cons)
                                {
                                    edges.Add(new NodeEdge(node.NodeId, con.ToLower().Trim(), 999999));
                                }
                            }
                            nodes.Add(node);
                        }
                    }
                }// End foreach loop.

                // Sort out line-string connections - Corridors can be connected to each other using
                // a simple line string.
                edges.AddRange(LineStringsToEdges(CorridorPointPairs, nodes, features));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verify Node edges on this single floor.
        /// </summary>
        /// <param name="nodes">Nodes on this floor</param>
        /// <param name="edges">Corresponding edges on this floor</param>
        /// <returns>A list of broken connection errors, if any</returns>
        public static List<string> VerifyNodeEdgesSingleFloor(List<Node> nodes, List<NodeEdge> edges)
        {
            List<string> brokenConnections = new List<string>();

            // Iterate through all nodes.
            foreach (Node node in nodes)
            {
                // Ensure this node has connections.
                NodeEdge[] connections = edges.Where(e => e.Node1Id == node.NodeId || e.Node2Id == node.NodeId).ToArray();

                // Iterate through its connections.
                foreach (NodeEdge connection in connections)
                {
                    // Skip connections between stairs and lifts because they require more than one floor to verify.
                    if ((connection.Node1Id.StartsWith("s", StringComparison.OrdinalIgnoreCase) && connection.Node2Id.StartsWith("s", StringComparison.OrdinalIgnoreCase))
                        || (connection.Node1Id.StartsWith("l", StringComparison.OrdinalIgnoreCase) && connection.Node2Id.StartsWith("l", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    string node1BC = SharedFunctions.GetBuildingCodeFromId(connection.Node1Id);
                    string node2BC = SharedFunctions.GetBuildingCodeFromId(connection.Node2Id);
                    sbyte node1Floor = -1;
                    try
                    {
                        node1Floor = SharedFunctions.GetLevelFromId(connection.Node1Id);
                        if (node1Floor < 0)
                        {
                            throw new InvalidOperationException("No Level!");
                        }
                    }
                    catch
                    {
                        brokenConnections.Add($"{connection.Node1Id} is missing a level number in the connection to {connection.Node2Id}");
                        continue;
                    }
                    sbyte node2Floor = -1;
                    try
                    {
                        node2Floor = SharedFunctions.GetLevelFromId(connection.Node2Id);
                        if (node2Floor < 0)
                        {
                            throw new InvalidOperationException("No Level!");
                        }
                    }
                    catch
                    {
                        brokenConnections.Add($"{connection.Node2Id} is missing a level number in the connection to {connection.Node1Id}");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node1BC))
                    {
                        brokenConnections.Add($"{connection.Node1Id} is missing a building code in its connection to {connection.Node2Id}");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node2BC))
                    {
                        brokenConnections.Add($"{connection.Node2Id} is missing a building code in its connection to {connection.Node1Id}");
                        continue;
                    }

                    // Skip connections between outdoors and indoors - different building, or different floors - codes as they require more than on json file to verify.
                    if (node1BC != node2BC || node1Floor != node2Floor)
                    {
                        continue;
                    }

                    Node node1 = nodes.Find(n => n.NodeId == connection.Node1Id);
                    Node node2 = nodes.Find(n => n.NodeId == connection.Node2Id);

                    // Check both nodes exist.
                    if (node1 == default)
                    {
                        brokenConnections.Add($"Cannot find node: {connection.Node1Id}");
                        continue;
                    }

                    if (node2 == default)
                    {
                        brokenConnections.Add($"Cannot find node: {connection.Node2Id}");
                        continue;
                    }

                    NodeEdge result = Array.Find(connections, c => c.Node1Id == node2.NodeId && c.Node2Id == node1.NodeId);

                    // Check a connection exists both ways.
                    if (result == null)
                    {
                        // Connection does not exist.
                        brokenConnections.Add($"Broken Connection: {node1.NodeId} is only connected to {node2.NodeId} one way");
                    }
                }
            }

            return brokenConnections;
        }

        /// <summary>
        /// Convert Line string connections to Node edges.
        /// </summary>
        /// <param name="linestringConnections">A list of line string connections</param>
        /// <param name="nodes">Associated nodes for this floor</param>
        /// <param name="nonCorridors">List of features that can be used to check for line strings connections to rooms</param>
        /// <returns>Node edges calculated from provided line strings</returns>
        private static List<NodeEdge> LineStringsToEdges(List<(IPosition, IPosition)> linestringConnections, List<Node> nodes, List<Feature> nonCorridors)
        {
            List<NodeEdge> edgesOutput = new List<NodeEdge>();

            // Iterate through line strings.
            foreach ((IPosition, IPosition) pair in linestringConnections)
            {
                NodeEdge edge = new NodeEdge();
                NodeEdge edge2 = new NodeEdge();

                bool found = false;
                // Iterate through nodes.
                foreach (Node node in nodes)
                {
                    // Do the lat/lon points of this node match either of the lat/lon points in the pair?
                    if (node.Latitude == pair.Item1.Latitude && node.Longitude == pair.Item1.Longitude)
                    {
                        // They match the first item.
                        edge.Node1Id = node.NodeId;
                        edge.Node1 = node;
                        edge2.Node2Id = node.NodeId;
                        edge2.Node2 = node;
                    }
                    else if (node.Latitude == pair.Item2.Latitude && node.Longitude == pair.Item2.Longitude)
                    {
                        // They match the second item.
                        edge.Node2Id = node.NodeId;
                        edge.Node2 = node;
                        edge2.Node1Id = node.NodeId;
                        edge2.Node1 = node;
                    }

                    if (edge.Node1Id != null && edge.Node2Id != null && edge.Node1Id.Length > 0 && edge.Node2Id.Length > 0)
                    {
                        // Both have been found.
                        edge.Weight = 999999;
                        edgesOutput.Add(edge);
                        edge2.Weight = 999999;
                        edgesOutput.Add(edge2);
                        found = true;
                        break;
                    }
                }

                // Don't bother checking the rooms if we already found the connections in the corridors.
                if (found)
                {
                    continue;
                }

                // Check for room links.
                (bool found, NodeEdge edge, NodeEdge edge2) result = CheckForRoomLinks(nonCorridors, pair, edge, edge2);
                if (result.found)
                {
                    // Add to output if a connection is found.
                    edgesOutput.Add(result.edge);
                    edgesOutput.Add(result.edge2);
                }
            }
            return edgesOutput;
        }

        /// <summary>
        /// Check the LineString for connections to rooms.
        /// </summary>
        /// <param name="nonCorridors">A list of all possible rooms.</param>
        /// <param name="pair">This line string node pair.</param>
        /// <param name="edge">The edge to set</param>
        /// <param name="edge2">The reverse edge to set.</param>
        /// <returns>True if a connection is found, along with the two edges required for that connection.</returns>
        private static (bool found, NodeEdge edge, NodeEdge edge2) CheckForRoomLinks(List<Feature> nonCorridors, (IPosition, IPosition) pair, NodeEdge edge, NodeEdge edge2)
        {
            foreach (Feature feature in nonCorridors)
            {
                if (feature.Geometry?.Type == GeoJSON.Net.GeoJSONObjectType.Polygon)
                {
                    foreach (LineString coordPart in ((Polygon)feature.Geometry).Coordinates)
                    {
                        foreach (IPosition coord in coordPart.Coordinates)
                        {
                            if (coord.Latitude == pair.Item1.Latitude && coord.Longitude == pair.Item1.Longitude)
                            {
                                // They match the first item.
                                if (feature.Properties.ContainsKey("id"))
                                {
                                    edge.Node1Id = feature.Properties["id"].ToString().ToLower();
                                    edge2.Node2Id = feature.Properties["id"].ToString().ToLower();
                                }
                            }
                            else if (coord.Latitude == pair.Item2.Latitude && coord.Longitude == pair.Item2.Longitude && feature.Properties.ContainsKey("id"))
                            {
                                edge.Node2Id = feature.Properties["id"].ToString().ToLower();
                                edge2.Node1Id = feature.Properties["id"].ToString().ToLower();
                            }

                            if (!string.IsNullOrEmpty(edge.Node1Id) && !string.IsNullOrEmpty(edge.Node2Id))
                            {
                                // Both have been found.
                                edge.Weight = 999999;
                                edge2.Weight = 999999;
                                return (true, edge, edge2);
                            }
                        }
                    }
                }
            }
            return (false, edge, edge2);
        }

        /// <summary>
        /// Helper method to determine if a node starts with a string correctly.
        /// </summary>
        /// <param name="nodeId">NodeId to check.</param>
        /// <param name="startsWith">Starting string</param>
        /// <returns>True if the node id starts with the starting string and the last character is a digit.</returns>
        private static bool NodeIdStartsWith(string nodeId, string startsWith) => nodeId.StartsWith(startsWith) && (nodeId.Length > startsWith.Length) && char.IsDigit(nodeId[startsWith.Length]);
    }
}