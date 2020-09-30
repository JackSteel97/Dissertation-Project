using GeoJSON.Net.Feature;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dissertation.Helpers
{
    public static class NodeClassifier
    {
        /// <summary>
        /// Attempt to classify the provided JOSM as a floor.
        /// </summary>
        /// <param name="collection">JOSM output to classify</param>
        /// <param name="errors">output errors</param>
        /// <returns>True if successful</returns>
        public static bool Classify(FeatureCollection collection, out List<string> errors)
        {
            errors = new List<string>();
            try
            {
                List<Feature> features = collection.Features;

                if (features == null)
                {
                    errors.Add("Json Parsing failed");
                    return false;
                }

                // Iterate through features.
                foreach (Feature f in features)
                {
                    // Check there are properties to check.
                    if (f.Properties?.Count > 0)
                    {
                        StringBuilder props = new StringBuilder();
                        foreach (KeyValuePair<string, object> item in f.Properties)
                        {
                            props.Append(Environment.NewLine).Append(item.Key).Append(" = ").Append(item.Value);
                        }

                        // Check for too many flag properties.
                        if (f.Properties.ContainsKey("NAV_FEATURE") && f.Properties.ContainsKey("NAV_CORRIDOR"))
                        {
                            // This feature has both tags making it ambiguous.
                            errors.Add($"A feature with the properties: {props}{Environment.NewLine}has both the NAV_CORRIDOR and NAV_FEATURE tags.");
                            continue;
                        }

                        // Check for flag property. We expect this to be a polygon.
                        if (f.Properties.ContainsKey("NAV_FEATURE") && f.Geometry.Type != GeoJSON.Net.GeoJSONObjectType.Polygon)
                        {
                            // It is not a polygon type.
                            errors.Add($"A feature with the properties: {props}{Environment.NewLine}has the tag 'NAV_FEATURE' but is not a room polygon - has type: {f.Geometry.Type.ToString()}");
                        }

                        // Check for flag property. We expect this to be a corridor.
                        if (f.Properties.ContainsKey("NAV_CORRIDOR") && f.Geometry.Type != GeoJSON.Net.GeoJSONObjectType.Point)
                        {
                            // It is not a point type.
                            errors.Add($"A feature with the properties: {props}{Environment.NewLine}has the tag 'NAV_CORRIDOR' but is not a corridor feature - has type {f.Geometry.Type.ToString()}");
                        }

                        // Check for no flag properties.
                        if (!f.Properties.ContainsKey("NAV_FEATURE") && !f.Properties.ContainsKey("NAV_CORRIDOR"))
                        {
                            // We expect this feature to have no relevant tags.
                            List<(string, string)> tags = ContainsValidTags(f);
                            if (tags.Count > 0)
                            {
                                foreach ((string, string) tag in tags)
                                {
                                    errors.Add($"A feature with the properties: {props}{Environment.NewLine}is not indicated to be part of the required features, but has the tag: {tag.Item1} = {tag.Item2}");
                                }
                            }
                        }
                    }
                }
                return errors.Count == 0;
            }
            catch (Exception e)
            {
                errors.Add($"Classifier: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Helper method to check if a feature has valid tags.
        /// </summary>
        /// <param name="feature">Feature to check</param>
        /// <returns>A list of valid tags that the feature has, if any</returns>
        private static List<(string, string)> ContainsValidTags(Feature feature)
        {
            List<(string, string)> validTags = new List<(string, string)>();

            // Does this feature contain the connected_nodes tag?
            if (feature.Properties.ContainsKey("connected_nodes"))
            {
                // Yes, get the value too.
                (string, string) tag = ("connected_nodes", feature.Properties["connected_nodes"].ToString());
                validTags.Add(tag);
            }

            // Does this feature have an id tag?
            if (feature.Properties.ContainsKey("id"))
            {
                // Yes, get the value too.
                (string, string) tag = ("id", feature.Properties["id"].ToString());
                validTags.Add(tag);
            }

            // Does this feature have a node_type tag?
            if (feature.Properties.ContainsKey("node_type"))
            {
                // Yes, get the value too.
                (string, string) tag = ("node_type", feature.Properties["node_type"].ToString());
                validTags.Add(tag);
            }
            return validTags;
        }
    }
}