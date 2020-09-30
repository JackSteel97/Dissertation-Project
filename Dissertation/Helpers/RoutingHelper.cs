using Dissertation.Models.Database;
using Dissertation.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dissertation.Helpers
{
    public static class RoutingHelper
    {
        /// <summary>
        /// Find the route of lowest distance.
        /// </summary>
        /// <param name="possibleRoutes">Given set of routes</param>
        /// <returns>A single route, of lowest total distance from the set</returns>
        public static Route FindTheBestRouteFromPossibleRoutes(List<Route> possibleRoutes)
        {
            // Initialise lowest variables.
            double lowestDistance = double.MaxValue;
            Route lowestDistanceRoute = new Route();

            if (possibleRoutes.Count > 0)
            {
                foreach (Route route in possibleRoutes)
                {
                    if (route.TotalDistance < lowestDistance && route.TotalCost >= 0)
                    {
                        lowestDistance = route.TotalDistance;
                        lowestDistanceRoute = route;
                    }
                }
            }
            return lowestDistanceRoute;
        }

        /// <summary>
        /// Get a list of node ids that satisfy the route type part from the given initial id.
        /// </summary>
        /// <param name="dissDatabaseContext">Db context</param>
        /// <param name="code">Initial node id</param>
        /// <param name="routeTypePortion">Route type code.</param>
        /// <returns>A list of unique node ids.</returns>
        public static async Task<List<string>> GetMatchingIds(DissDatabaseContext dissDatabaseContext, string code, string routeTypePortion)
        {
            List<string> output = new List<string>();
            switch (routeTypePortion)
            {
                case "r":
                    // Room.
                    output.Add(code);
                    return output;

                case "b":
                    // Building.
                    // Get entrances.
                    return await dissDatabaseContext
                        .NodeEdges
                        .AsNoTracking()
                        .Include(e => e.Node1)
                        .Include(e => e.Node2)
                        .Where(edge => edge.Node1.BuildingCode == code
                            && edge.Node1.Type == NodeType.Corridor
                            && edge.Node2.BuildingCode == "out"
                            && edge.Node2.Type == NodeType.Corridor)
                        .Select(e => e.Node1Id).ToListAsync();
            }
            return output;
        }
    }
}