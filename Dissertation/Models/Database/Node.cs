using Dissertation.Helpers;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Dissertation.Models.Database
{
    public enum NodeType : byte
    {
        MaleToilet = 0,
        GenderNeutralToilet = 1,
        DisabledToilet = 2,
        Parking = 3,
        ShowerRoom = 4,
        Unrouteable = 5,
        Corridor = 6,
        FemaleToilet = 7,
        UnisexToilet = 8,
        Stairs = 9,
        Lift = 10,
        Room = 11,
        BabyChanging = 12,
        Unknown = 255
    }

    public class Node
    {
        [JsonProperty("nodeId")]
        public string NodeId { get; set; }

        [JsonProperty("buildingCode")]
        public string BuildingCode { get; set; }

        [JsonProperty("floor")]
        public byte Floor { get; set; }

        [JsonProperty("type")]
        public NodeType Type { get; set; }

        [JsonProperty("latitude")]
        public double? Latitude { get; set; }

        [JsonProperty("longitude")]
        public double? Longitude { get; set; }

        [JsonProperty("roomName")]
        public string RoomName { get; set; }

        [JsonProperty("corridorWidth")]
        public double? CorridorWidth { get; set; }

        [JsonProperty("outgoingEdges")]
        public List<NodeEdge> OutgoingEdges { get; set; }

        [JsonProperty("incomingEdges")]
        public List<NodeEdge> IncomingEdges { get; set; }

        [JsonProperty("building")]
        public Building Building { get; set; }

        [JsonProperty("leafletNodeType")]
        public string LeafletNodeType { get; set; }

        [JsonIgnore]
        public List<TimetableEvent> Events { get; set; }

        public double DistanceInMetersTo(Node other) => SharedFunctions
            .GetDistanceFromLatLonInMeters(
                Latitude.GetValueOrDefault(),
                Longitude.GetValueOrDefault(),
                other.Latitude.GetValueOrDefault(),
                other.Longitude.GetValueOrDefault()
            );
    }
}