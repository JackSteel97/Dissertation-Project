using Dissertation.Helpers;
using Newtonsoft.Json;

namespace Dissertation.Models
{
    public class RouteRequestResponse
    {
        [JsonProperty("normalRoute")]
        public Route NormalRoute { get; set; }

        [JsonProperty("adjustedRoute")]
        public Route AdjustedRoute { get; set; }

        public RouteRequestResponse()
        {
            NormalRoute = new Route();
            AdjustedRoute = new Route();
        }
    }
}