using Dissertation.Models.Database;
using Dissertation.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dissertation.Helpers
{
    public static class SharedFunctions
    {
        /// <summary>
        /// Average human walking speed in meters per second (3.1mph).
        /// </summary>
        public const double AvgWalkingSpeed = 1.38582;

        /// <summary>
        /// Distance to use for an edge when one or both nodes are not geographic points.
        /// </summary>
        public const int NonCorridorDistance = 2;

        /// <summary>
        /// Constant width to use for an edge when one or both nodes are not geographic points.
        /// </summary>
        public const int NonCorridorWidth = 1;

        /// <summary>
        /// Constant area to use for an edge when one or both nodes are not geographic points.
        /// </summary>
        public const double NonCorridorArea = NonCorridorDistance * NonCorridorWidth;

        /// <summary>
        /// Find all the lifts in the same lift shaft given any lift
        /// </summary>
        /// <param name="liftId">ID of lift to start at</param>
        /// <param name="dbContext">Database context</param>
        /// <returns>A list of lift nodes in this shaft</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task<List<Node>> GetAllLiftNodesOnThisBranch(string liftId, DissDatabaseContext dbContext)
        {
            List<Node> outputLifts = new List<Node>();
            Node currentLift = await dbContext.Nodes.AsNoTracking().Include(l => l.IncomingEdges).ThenInclude(l => l.Node2).FirstOrDefaultAsync(node => node.NodeId == liftId);
            if (currentLift == default(Node))
            {
                return outputLifts;
            }
            outputLifts.Add(currentLift);
            Queue<Node> liftsToCheck = new Queue<Node>();

            bool allLiftsFound = false;
            while (!allLiftsFound)
            {
                // Get all lifts connected to the current one
                Node[] liftResults = currentLift.IncomingEdges.Where(edge => edge.Node1Id == currentLift.NodeId && edge.Node2Id.StartsWith("l_", StringComparison.OrdinalIgnoreCase)).Select(e => e.Node2).ToArray();

                foreach (Node result in liftResults)
                {
                    // Have we already seen this one?
                    if (!outputLifts.Any(x => x.NodeId == result.NodeId))
                    {
                        // No, add to check queue
                        liftsToCheck.Enqueue(result);
                        outputLifts.Add(result);
                    }
                }

                if (liftsToCheck.Count > 0)
                {
                    currentLift = await dbContext.Nodes.AsNoTracking().Include(l => l.IncomingEdges).ThenInclude(l => l.Node2).FirstOrDefaultAsync(node => node.NodeId == liftsToCheck.Dequeue().NodeId);
                    if (currentLift == default(Node))
                    {
                        return outputLifts;
                    }
                }
                else
                {
                    allLiftsFound = true;
                }
            }
            return outputLifts;
        }

        /// <summary>
        /// get the building code from a unique ID
        /// </summary>
        /// <param name="nodeId">ID</param>
        /// <returns>building code of node</returns>
        public static string GetBuildingCodeFromId(string nodeId)
        {
            string[] parts = nodeId.Split('_');
            if (parts.Length > 1 && parts[1].Length > 0)
            {
                // Get part [1] omitting the last character.
                string buildingCode = parts[1][0..^1].ToLower().Trim();
                if (!string.IsNullOrWhiteSpace(buildingCode) && buildingCode.All(c => char.IsLetter(c)))
                {
                    return buildingCode;
                }
            }
            return null;
        }

        /// <summary>
        /// Calculate a distance from lat/lon points.
        /// </summary>
        /// <param name="lat1">Latitude of point 1</param>
        /// <param name="lon1">Longitude of point 1</param>
        /// <param name="lat2">Latitude of point 2</param>
        /// <param name="lon2">Longitude of point 2</param>
        /// <returns>A distance in meters</returns>
        public static double GetDistanceFromLatLonInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0088; //radius of the earth in km
            double dLat = Deg2Rad(lat2 - lat1);
            double dLon = Deg2Rad(lon2 - lon1);

            // Equation source: http://www.movable-type.co.uk/scripts/latlong.html
            double a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) + (Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double d = R * c; //distance in km
            return d * 1000; //distance in meters
        }

        /// <summary>
        /// Gets the level from a unique ID
        /// </summary>
        /// <param name="nodeId">ID</param>
        /// <returns>level of the node</returns>
        public static sbyte GetLevelFromId(string nodeId)
        {
            string[] parts = nodeId.Split('_');
            if (parts.Length > 1 && parts[1].Length > 0)
            {
                int startOfLevel = 0;
                foreach (char c in parts[1])
                {
                    if (!char.IsDigit(c))
                    {
                        startOfLevel++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (sbyte.TryParse(parts[1].Substring(startOfLevel), out sbyte result))
                {
                    return result;
                }
            }
            return -1;
        }

        /// <summary>
        /// Convert a string type to an enum value.
        /// </summary>
        /// <param name="type">Type to convert</param>
        /// <returns>Matching Enum value or unknown</returns>
        public static NodeType GetNodeType(string type)
        {
            return (type.ToLower().Trim()) switch
            {
                "wcm" => NodeType.MaleToilet,
                "wcn" => NodeType.GenderNeutralToilet,
                "wcd" => NodeType.DisabledToilet,
                "parking" => NodeType.Parking,
                "wcs" => NodeType.ShowerRoom,
                "unroutable" => NodeType.Unrouteable,
                "corridor" => NodeType.Corridor,
                "wcf" => NodeType.FemaleToilet,
                "wcmf" => NodeType.UnisexToilet,
                "stairs" => NodeType.Stairs,
                "room" => NodeType.Room,
                "lift" => NodeType.Lift,
                "other" => NodeType.Room,
                "wcb" => NodeType.BabyChanging,
                _ => NodeType.Unknown,
            };
        }

        /// <summary>
        /// Try and get a normal room code from a room id.
        /// </summary>
        /// <param name="id">Room id</param>
        /// <returns>Room code</returns>
        public static string GetRoomCodeFromId(string id)
        {
            string[] splt = id.Split('_');
            if (splt.Length > 2)
            {
                return $"{splt[1]}{splt[2]}".ToUpper();
            }
            return null;
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        /// <param name="deg">Degrees value</param>
        /// <returns>Radians value</returns>
        private static double Deg2Rad(double deg) => deg * (Math.PI / 180);

        public static int CalculateWalkingTime(double distance)
        {
            return Convert.ToInt32(Math.Ceiling(distance / AvgWalkingSpeed));
        }

        public static double CalculateWalkingTimeNoRounding(double distance)
        {
            return distance / AvgWalkingSpeed;
        }
    }
}