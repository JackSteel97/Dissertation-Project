using GeoJSON.Net.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dissertation.Helpers
{
    public static class NodeValidator
    {
        /// <summary>
        /// Validate JOSM output.
        /// </summary>
        /// <param name="collection">JOSM output</param>
        /// <param name="errors">output errors found</param>
        /// <param name="warnings">output warnings</param>
        /// <returns>True if valid</returns>
        public static bool Validate(FeatureCollection collection, out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();
            try
            {
                List<Feature> features = collection.Features;

                // Floor counter keeps track of how many nodes are on what floor. If there is more than one floor detected in a single file, then this file is not valid.
                Dictionary<string, int> floorCounter = new Dictionary<string, int>(2);

                // Building tag counter keeps track of how many nodes are labelled with what building tag. If there is more than one building tag detected in a single file, then the file is not valid.
                Dictionary<string, int> buildingTagCounter = new Dictionary<string, int>(2);

                // Iterate through features.
                foreach (Feature f in features)
                {
                    // Check there are properties.
                    if (f.Properties?.Count > 0 && (f.Properties.ContainsKey("NAV_FEATURE") || f.Properties.ContainsKey("NAV_CORRIDOR")))
                    {
                        // Check if it has a node_type tag.
                        if (!f.Properties.ContainsKey("node_type"))
                        {
                            // Missing node type.
                            errors.Add($"Feature is missing a node type tag");
                            continue;
                        }

                        // Check if it has an ID tag.
                        if (!f.Properties.ContainsKey("id") && !string.Equals(f.Properties["node_type"].ToString(), "block", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Feature with node type {f.Properties["node_type"]} is missing an id tag");
                            continue;
                        }

                        // Check for flag property. Check the node type.
                        if (f.Properties.ContainsKey("NAV_FEATURE") && (!IsValidNodeType(f.Properties["node_type"].ToString()) || string.Equals(f.Properties["node_type"].ToString(), "corridor", StringComparison.OrdinalIgnoreCase)))
                        {
                            errors.Add($"Feature has an invalid node type of {f.Properties["node_type"]}");
                            continue;
                        }

                        // Check for flag property. Check the node type.
                        if (f.Properties.ContainsKey("NAV_CORRIDOR") && IsValidNodeType(f.Properties["node_type"].ToString()) && !string.Equals(f.Properties["node_type"].ToString(), "corridor", StringComparison.OrdinalIgnoreCase) && !string.Equals(f.Properties["node_type"].ToString(), "parking", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Feature has an invalid node type of {f.Properties["node_type"]}");
                            continue;
                        }

                        // Check all the tags exist.
                        if (!HasRequiredTags(f, errors))
                        {
                            continue;
                        }

                        // Check the id format - if it is not a block and therefore should have an ID.
                        if (!string.Equals(f.Properties["node_type"].ToString(), "block", StringComparison.OrdinalIgnoreCase) && !IsIdValid(f, errors, warnings))
                        {
                            continue;
                        }

                        // Add to floor counter.
                        if (floorCounter.ContainsKey(f.Properties["level"].ToString()))
                        {
                            floorCounter[f.Properties["level"].ToString()]++;
                        }
                        else
                        {
                            floorCounter.Add(f.Properties["level"].ToString(), 1);
                        }

                        // Add to the building counter.
                        if (buildingTagCounter.ContainsKey(f.Properties["building"].ToString().ToLower()))
                        {
                            buildingTagCounter[f.Properties["building"].ToString().ToLower()]++;
                        }
                        else
                        {
                            buildingTagCounter.Add(f.Properties["building"].ToString().ToLower(), 1);
                        }
                    }
                }

                // Run check on floor counter to see if there any issues.
                CheckFloors(floorCounter, features, errors);

                // Run check on building tag counter to see if there are any issues.
                CheckBuildings(buildingTagCounter, features, errors);

                if (errors.Count > 0)
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                errors.Add($"Validator: {e.Message}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Check a given building tag counter for problems.
        /// </summary>
        /// <param name="buildingCounter">Building tag counter to check</param>
        /// <param name="features">Features for this file</param>
        /// <param name="errors">Reference to errors list</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private static void CheckBuildings(Dictionary<string, int> buildingCounter, List<Feature> features, List<string> errors)
        {
            // Is there more than one building?
            if (buildingCounter.Keys.Count > 1)
            {
                // Yes, There are conflicting building codes.
                StringBuilder err = new StringBuilder().Append(Environment.NewLine).Append("There are conflicting building codes on this set of features.").Append(Environment.NewLine);
                KeyValuePair<string, int> highest = new KeyValuePair<string, int>(buildingCounter.Keys.First(), buildingCounter.Values.First());

                // The building code that has the highest number of features associated with it is assumed to be the intended correct building.
                foreach (KeyValuePair<string, int> entry in buildingCounter)
                {
                    err.Append("There are ").Append(entry.Value).Append(" feature(s) labelled as building '").Append(entry.Key).Append('\'').Append(Environment.NewLine);

                    // Find entry with highest number of features.
                    if (highest.Value < entry.Value)
                    {
                        highest = entry;
                    }
                }

                // Write out the errors.
                err.Append("\tThe following are features that are not labelled as part of the assumed correct building of '").Append(highest.Key).Append('\'').Append(Environment.NewLine);
                foreach (Feature f in features)
                {
                    if (f.Properties.ContainsKey("building") && (f.Properties.ContainsKey("NAV_FEATURE") || f.Properties.ContainsKey("NAV_CORRIDOR")) && f.Properties["building"].ToString().ToLower() != highest.Key)
                    {
                        if (f.Properties.ContainsKey("id"))
                        {
                            err.Append("\t\t").Append(f.Properties["id"]).Append(" is labelled as building code '").Append(f.Properties["building"]).Append('\'').Append(Environment.NewLine);
                        }
                        else
                        {
                            err.Append("\t\tA feature without an ID tag is labelled as building code '").Append(f.Properties["building"]).Append('\'').Append(Environment.NewLine);
                        }
                    }
                }
                errors.Add(err.ToString());
            }
        }

        /// <summary>
        /// Check a given floor counter for problems.
        /// </summary>
        /// <param name="floorCounter">Floor counter to check</param>
        /// <param name="features">Features for this floor</param>
        /// <param name="errors">reference to errors list</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private static void CheckFloors(Dictionary<string, int> floorCounter, List<Feature> features, List<string> errors)
        {
            // Is there more than one floor?
            if (floorCounter.Keys.Count > 1)
            {
                // Yes, There are conflicting floor numbers.
                StringBuilder err = new StringBuilder().Append(Environment.NewLine).Append("There are conflicting floor numbers on this set of features.").Append(Environment.NewLine);
                KeyValuePair<string, int> highest = new KeyValuePair<string, int>(floorCounter.Keys.First(), floorCounter.Values.First());

                // The floor number that has the highest number of features associated with it is
                // assumed to be the intended correct floor.
                foreach (KeyValuePair<string, int> entry in floorCounter)
                {
                    err.Append("There are ").Append(entry.Value).Append(" feature(s) labelled as floor ").Append(entry.Key).Append(Environment.NewLine);

                    // Find entry with highest number of features.
                    if (highest.Value < entry.Value)
                    {
                        highest = entry;
                    }
                }

                // Write out the errors.
                err.Append("\tThe following are features that are not labelled as part of the assumed correct floor of ").Append(highest.Key).Append(Environment.NewLine);
                foreach (Feature f in features)
                {
                    if (f.Properties.ContainsKey("level") && (f.Properties.ContainsKey("NAV_FEATURE") || f.Properties.ContainsKey("NAV_CORRIDOR")) && f.Properties["level"].ToString() != highest.Key)
                    {
                        if (f.Properties.ContainsKey("id"))
                        {
                            err.Append("\t\t").Append(f.Properties["id"]).Append(" is labelled as floor ").Append(f.Properties["level"]).Append(Environment.NewLine);
                        }
                        else
                        {
                            err.Append("\t\tA feature without an ID tag is labelled as floor ").Append(f.Properties["level"]).Append(Environment.NewLine);
                        }
                    }
                }
                errors.Add(err.ToString());
            }
        }

        /// <summary>
        /// Helper method to check a feature has the required tags for its type.
        /// </summary>
        /// <param name="feature">Feature to check</param>
        /// <param name="errors">reference to errors list<param>
        /// <returns>True if required tags are present</returns>
        private static bool HasRequiredTags(Feature feature, List<string> errors)
        {
            // Not including node_type as it has already been checked.
            string[] requiredTags = Array.Empty<string>();
            string nodeType = feature.Properties["node_type"].ToString().ToLower().Trim();

            // Work out which tags are required based on the node type.
            switch (nodeType)
            {
                case "room":
                    requiredTags = new string[] { "building", "id", "level", "name" };
                    break;

                case "stairs":
                case "lift":
                    requiredTags = new string[] { "building", "id", "level", "name", "connected_nodes" };
                    break;

                case "block":
                    requiredTags = new string[] { "building", "level" };
                    break;

                case "corridor":
                    requiredTags = new string[] { "building", "level", "id", "corridor_width" };
                    break;

                case "parking":
                    requiredTags = new string[] { "building", "level", "id", "name" };
                    break;

                case "other":
                    requiredTags = new string[] { "building", "id", "level", "name" };
                    break;

                case "wcm":
                case "wcf":
                case "wcd":
                case "wcb":
                case "wcmf":
                case "wcn":
                case "wcs":
                    requiredTags = new string[] { "building", "level", "id", "name" };
                    break;
            }

            // Perform check against required tags.
            foreach (string tag in requiredTags)
            {
                if (!feature.Properties.ContainsKey(tag) || string.IsNullOrWhiteSpace(feature.Properties[tag].ToString()))
                {
                    errors.Add($"Feature of node type '{nodeType}' is missing the '{tag}' tag. Id: {feature.Properties["id"]}");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if a feature has a valid id property.
        /// </summary>
        /// <param name="f">Feature to check</param>
        /// <param name="errors">reference to errors list</param>
        /// <param name="warnings">reference to warnings list</param>
        /// <returns>True if valid</returns>
        private static bool IsIdValid(Feature f, List<string> errors, List<string> warnings)
        {
            // Get property values.
            string id = f.Properties["id"].ToString().ToLower().Trim();
            string level = f.Properties["level"].ToString().ToLower().Trim();
            string building = f.Properties["building"].ToString().ToLower().Trim();
            string nodeType = f.Properties["node_type"].ToString().ToLower().Trim();

            // Split Id string.
            string[] idParts = id.Split('_');
            StringBuilder buildingCode = new StringBuilder();
            StringBuilder floor = new StringBuilder();

            string idNodeType;
            if (idParts.Length > 0)
            {
                // Get the node type character.
                idNodeType = idParts[0];
                if (idParts.Length > 1)
                {
                    // Get the building code and floor number string.
                    string buildingFloorCode = idParts[1];
                    // Get building code and floor number separately.
                    for (int i = 0; i < buildingFloorCode.Length; i++)
                    {
                        if (char.IsLetter(buildingFloorCode[i]))
                        {
                            buildingCode.Append(buildingFloorCode[i]);
                        }
                        else if (char.IsDigit(buildingFloorCode[i]))
                        {
                            floor.Append(buildingFloorCode[i]);
                        }
                    }
                }
                else
                {
                    errors.Add($"Could not find a valid id for feature.");
                    return false;
                }
            }
            else
            {
                errors.Add($"Could not find a valid id for feature.");
                return false;
            }

            // Check node ID matches name if it is a routable room.
            if (nodeType == "room" || nodeType == "other")
            {
                string roomCodeFromId = SharedFunctions.GetRoomCodeFromId(id);
                string name = f.Properties["name"].ToString();
                if (!name.StartsWith(roomCodeFromId, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"The room {id} has a potentially invalid name: '{name}' for the given id. Human Check Required. (Consider if this node should be named with the room code or not)");
                }
            }

            // Validation checks.
            if (idNodeType == "r" && nodeType != "room" && nodeType != "other")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "m" && nodeType != "room" && nodeType != "other")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "s" && nodeType != "stairs")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "l" && nodeType != "lift")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "c" && nodeType != "corridor")
            {
                errors.Add($"The corridor node {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "p" && nodeType != "parking")
            {
                errors.Add($"The parking node {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "wcm" && nodeType != "wcm")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "wcf" && nodeType != "wcf")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "wcb" && nodeType != "wcb")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "wcd" && nodeType != "wcd")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (idNodeType == "wcmf" && nodeType != "wcmf")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }

            if (idNodeType == "wcn" && nodeType != "wcn")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }

            if (idNodeType == "wcs" && nodeType != "wcs")
            {
                errors.Add($"The room {id} does not have a valid node_type tag");
                return false;
            }
            if (building != buildingCode.ToString())
            {
                errors.Add($"The feature {id} does not have a valid building tag");
                return false;
            }
            if (floor.ToString() != level)
            {
                errors.Add($"The feature {id} does not have a valid level tag");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Helper method to check if a node type is a valid navigate me node type.
        /// </summary>
        /// <param name="nodeType">Node type to check</param>
        /// <returns>True if valid</returns>
        private static bool IsValidNodeType(string nodeType)
        {
            string[] validTypes = { "room", "other", "block", "stairs", "lift", "wcm", "wcf", "wcmf", "wcd", "wcb", "wcn", "wcs", "corridor", "parking" };
            if (string.IsNullOrWhiteSpace(nodeType))
            {
                return false;
            }
            foreach (string type in validTypes)
            {
                if (type.ToLower().Trim() == nodeType.ToLower().Trim())
                {
                    return true;
                }
            }
            return false;
        }
    }
}