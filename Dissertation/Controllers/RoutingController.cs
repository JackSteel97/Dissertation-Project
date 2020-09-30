using Dissertation.Helpers;
using Dissertation.Models;
using Dissertation.Models.Database;
using Dissertation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dissertation.Controllers
{
    /// <summary>
    /// A controller for handling routing requests.
    /// </summary>
    [Route("routing")]
    public class RoutingController : Controller
    {
        private readonly IMemoryCache MemoryCache;
        private readonly DissDatabaseContext DissDatabaseContext;
        private readonly Pathfinder Pathfinder;
        private StudentRoute[] AllStudentRoutes;
        private readonly AppSettingsService AppSettings;

        /// <summary>
        /// Constructor for the routing controller.
        /// </summary>
        /// <param name="dissDatabaseContext">Injected database context.</param>
        /// <param name="memoryCache">Injected memory cache.</param>
        /// <param name="appSettingsService">Injected app settings.</param>
        /// <exception cref="InvalidCastException"></exception>
        public RoutingController(DissDatabaseContext dissDatabaseContext, IMemoryCache memoryCache, AppSettingsService appSettingsService)
        {
            DissDatabaseContext = dissDatabaseContext;
            MemoryCache = memoryCache;
            AppSettings = appSettingsService;

            // Does a pathfinder exist in the cache already built?
            if (!MemoryCache.TryGetValue("pathfinder", out Pathfinder))
            {
                // No, build a new pathfinder and cache it for an hour.
                Pathfinder = new Pathfinder(DissDatabaseContext);
                MemoryCache.Set("pathfinder", Pathfinder, new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.NeverRemove).SetAbsoluteExpiration(TimeSpan.FromHours(1)));
            }
        }

        /// <summary>
        /// Endpoint to handle a request for a route.
        /// </summary>
        /// <param name="startNode">Origin node for route.</param>
        /// <param name="endNode">Destination node for route.</param>
        /// <param name="routeType">Route type string.</param>
        /// <param name="startingTime">Route starting time.</param>
        /// <returns></returns>
        [HttpGet]
        [Route("routerequest/{startNode}/{endNode}/{routeType}/{startingTime}")]
        public async Task<IActionResult> RouteRequest(string startNode, string endNode, string routeType, TimeSpan startingTime)
        {
            try
            {
                // Get all student routes.
                AllStudentRoutes = await CongestionHelper.GenerateAllStudentRoutes(DissDatabaseContext, Pathfinder, MemoryCache, AppSettings.Accommodations);

                RouteRequestResponse response = new RouteRequestResponse();

                // If start and end are the same, return empty response.
                if (startNode == endNode)
                {
                    return Ok(response);
                }

                // Get a list of possible start and end nodes that fulfil this request.
                List<string> starts = await RoutingHelper.GetMatchingIds(DissDatabaseContext, startNode, routeType[0].ToString());
                List<string> ends = await RoutingHelper.GetMatchingIds(DissDatabaseContext, endNode, routeType.Last().ToString());

                // Check we have a start location.
                if (starts.Count == 0)
                {
                    return BadRequest("Could not find start points.");
                }

                // Check we have an end location.
                if (ends.Count == 0)
                {
                    return BadRequest("Could not find end points");
                }

                // Calculate all possible routes between all starts and all ends.
                // There is more than one start or end point - probably more efficient to calculate in parallel.
                if (starts.Count > 1 || ends.Count > 1)
                {
                    ConcurrentBag<Route> possibleRoutes = new ConcurrentBag<Route>();
                    ConcurrentBag<Route> possibleAdjustedRoutes = new ConcurrentBag<Route>();
                    Parallel.ForEach(starts, startPoint =>
                    {
                        Parallel.ForEach(ends, destination =>
                        {
                            // Calculate route and add to possible.
                            possibleRoutes.Add(Pathfinder.BuildAStar(startPoint, destination));
                            possibleAdjustedRoutes.Add(Pathfinder.BuildAStar(startPoint, destination, AllStudentRoutes, startingTime));
                        });
                    });

                    // Find the shortest route from the possible routes.
                    response.NormalRoute = RoutingHelper.FindTheBestRouteFromPossibleRoutes(possibleRoutes.ToList());
                    response.AdjustedRoute = RoutingHelper.FindTheBestRouteFromPossibleRoutes(possibleAdjustedRoutes.ToList());
                }
                else
                {
                    // Only one start and end point - only need to do this once, so avoid parallel overheads.
                    response.NormalRoute = Pathfinder.BuildAStar(starts[0], ends[0]);
                    response.AdjustedRoute = Pathfinder.BuildAStar(starts[0], ends[0], AllStudentRoutes, startingTime);
                }

                // Adjust walking times based on congestion values.
                response.NormalRoute.CalculateAdjustedWalkingTime(AllStudentRoutes, startingTime);
                response.AdjustedRoute.CalculateAdjustedWalkingTime(AllStudentRoutes, startingTime);

                if (response.AdjustedRoute.WalkingTimeSeconds == response.NormalRoute.WalkingTimeSeconds)
                {
                    // Both routes are the same, remove the adjusted route - there was likely no congestion.
                    response.AdjustedRoute = new Route();
                }

                return Ok(response);
            }
            catch (Exception exception)
            {
                return StatusCode(500, exception.Message);
            }
        }
    }
}