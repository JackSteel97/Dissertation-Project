using Newtonsoft.Json;

namespace Dissertation.Models.Database
{
    public class NodeEdge
    {
        [JsonProperty("rowId")]
        public long RowId { get; set; }

        [JsonProperty("node1Id")]
        public string Node1Id { get; set; }

        [JsonProperty("node2Id")]
        public string Node2Id { get; set; }

        [JsonProperty("node1")]
        public Node Node1 { get; set; }

        [JsonProperty("node2")]
        public Node Node2 { get; set; }

        [JsonProperty("weight")]
        public double Weight { get; set; }

        [JsonProperty("corridorArea")]
        public double? CorridorArea { get; set; }

        public NodeEdge()
        {
        }

        public NodeEdge(string id1, string id2, float weight)
        {
            Node1Id = id1;
            Node2Id = id2;
            Weight = weight;
        }
    }
}