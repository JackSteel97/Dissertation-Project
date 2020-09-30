using System.Collections.Generic;

namespace Dissertation.Models
{
    /// <summary>
    /// Used to represent edge case node configuration.
    /// <br />
    /// Some node connections are special and require specific weights in order to work correctly.
    /// </summary>
    public class EdgeCaseWeightsConfiguration
    {
        /// <summary>
        /// List where both node string starters much match to apply.
        /// </summary>
        public IEnumerable<BothNodesEntry> BothNodes { get; set; }

        /// <summary>
        /// List where either node string starter must match to apply.
        /// </summary>
        public IEnumerable<EitherNodeEntry> EitherNode { get; set; }
    }

    public class BothNodesEntry
    {
        public string Node1String { get; set; }
        public string Node2String { get; set; }
        public double Weight { get; set; }
    }

    public class EitherNodeEntry
    {
        public string StringStart { get; set; }
        public double Weight { get; set; }
    }
}