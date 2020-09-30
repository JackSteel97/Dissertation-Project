using Newtonsoft.Json;

namespace Dissertation.Models
{
    public class Room
    {
        [JsonProperty("buildingName")]
        public string BuildingName { get; set; }

        [JsonProperty("nodeBuildingCode")]
        public string NodeBuildingCode { get; set; }

        [JsonProperty("nodeId")]
        public string NodeId { get; set; }

        [JsonProperty("roomName")]
        public string RoomName { get; set; }

        public Room()
        {
        }

        public Room(string nodeId, string nodeBuildingCode, string buildingName, string roomName)
        {
            NodeId = nodeId;
            NodeBuildingCode = nodeBuildingCode;
            BuildingName = buildingName;
            RoomName = roomName;
        }
    }
}