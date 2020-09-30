using Dissertation.Models;

namespace Dissertation.Services
{
    public class AppSettingsService
    {
        public string DatabaseConnection { get; set; }

        public EdgeCaseWeightsConfiguration EdgeCaseWeights { get; set; }

        public string[] Accommodations { get; set; }
    }
}