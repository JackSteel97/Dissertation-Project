using GeoJSON.Net;
using GeoJSON.Net.Feature;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dissertation.Helpers
{
    public class Josm2Leaflet
    {
        /// <summary>
        /// Calculated file prefix for floor file.
        /// </summary>
        internal string filePrefix = "";

        /// <summary>
        /// Parse the input JSON to get a list of features we are interested in.
        /// </summary>
        /// <param name="collection">GeoJson output from JOSM</param>
        /// <returns>A list of GeoJSON features, filtered to only include ones marked for inclusion.</returns>
        public List<Feature> ParseJOSM(FeatureCollection collection)
        {
            // Get features.
            List<Feature> features = collection.Features;

            // Get all leaflet features.
            List<Feature> leafletFeatures = features.Where(f => f.Properties.ContainsKey("NAV_FEATURE")).ToList();

            // Order by node type to ensure consistency of z-index for features of the same type when drawn on leaflet.
            leafletFeatures = leafletFeatures.OrderBy(f => f.Properties["node_type"].ToString()).ToList();

            // Work out the file prefix.
            Feature feature = leafletFeatures.Find(f => f.Properties.ContainsKey("building") && f.Properties.ContainsKey("level") && f.Properties["building"].ToString().Length > 0 && f.Properties["level"] != null);
            if (feature != null)
            {
                filePrefix = feature.Properties["building"].ToString().ToLower() + feature.Properties["level"].ToString();
            }

            return leafletFeatures;
        }

        /// <summary>
        /// Remove corridor nodes from a GeoJson file.
        /// </summary>
        /// <param name="collection">GeoJson output from JOSM</param>
        /// <returns>A list of geojson features, with all corridor nodes and connections removed</returns>
        public List<Feature> RemoveCorridorNodes(FeatureCollection collection)
        {
            // Get list of features.
            List<Feature> features = collection.Features;

            // Return non corridor nodes.
            // Not marked as a corridor and not a line string.
            return features.Where(f => !f.Properties.ContainsKey("NAV_CORRIDOR") && f.Geometry.Type != GeoJSONObjectType.LineString).ToList();
        }

        /// <summary>
        /// Write some Json to the background map file on the server.
        /// </summary>
        /// <param name="jsonToWrite">Json to write to file</param>
        /// <param name="environment">Environment variable for absolute path</param>
        /// <returns>The absolute path to the written file</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        public async Task<string> WriteBackgroundToFile(string jsonToWrite, IWebHostEnvironment environment)
        {
            string dirPath = $"{environment.ContentRootPath}/Files/Json/";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            string fullPath = $"{dirPath}backgroundMap.json";
            await File.WriteAllTextAsync(fullPath, jsonToWrite);

            return fullPath;
        }

        /// <summary>
        /// Write some Json to a file using the calculated file prefix.
        /// </summary>
        /// <param name="jsonToWrite">Json to write to file</param>
        /// <param name="environment">Environment variable for absolute path</param>
        /// <returns>The absolute path to the written file</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        public async Task<string> WriteToFile(string jsonToWrite, IWebHostEnvironment environment)
        {
            string dirPath = $"{environment.ContentRootPath}/Files/Json/";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            string fullPath = $"{dirPath}{filePrefix}Rooms.json";
            await File.WriteAllTextAsync(fullPath, jsonToWrite);

            return fullPath;
        }
    }
}