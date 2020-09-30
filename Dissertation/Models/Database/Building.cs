using Newtonsoft.Json;
using System.Collections.Generic;

namespace Dissertation.Models.Database
{
    public class Building
    {
        [JsonProperty("buildingCode")]
        public string BuildingCode { get; set; }

        [JsonProperty("buildingName")]
        public string BuildingName { get; set; }

        [JsonProperty("nodes")]
        public List<Node> Nodes { get; set; }
    }
}