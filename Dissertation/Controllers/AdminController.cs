using CsvHelper;
using Dissertation.Helpers;
using Dissertation.Models;
using Dissertation.Models.Database;
using Dissertation.Services;
using GeoJSON.Net.Feature;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dissertation.Controllers
{
    /// <summary>
    /// Controller for handling actions for the admin screen.
    /// </summary>
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly AppSettingsService AppSettings;
        private readonly DissDatabaseContext DissDatabaseContext;
        private readonly IWebHostEnvironment WebHostEnvironment;

        /// <summary>
        /// Constructor for the admin controller with dependency injection parameters.
        /// </summary>
        /// <param name="appSettings">Injected settings.</param>
        /// <param name="dbContext">Injected database context.</param>
        /// <param name="hostingEnvironment">Injected hosting environment details.</param>
        public AdminController(AppSettingsService appSettings, DissDatabaseContext dbContext, IWebHostEnvironment hostingEnvironment)
        {
            AppSettings = appSettings;
            DissDatabaseContext = dbContext;
            WebHostEnvironment = hostingEnvironment;
        }

        /// <summary>
        /// Endpoint to return the Vue view stored in the Index.cshtml file.
        /// </summary>
        [Route("")]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Endpoint to import timetable data from an extract CSV file.
        /// </summary>
        [HttpPost]
        [Route("timetable/importFromFile")]
        public async Task<IActionResult> ImportTimetableData()
        {
            try
            {
                // Create a list to store the events from the CSV file.
                List<TimetableEventFromExtract> inputEvents;

                // Read extracted data from a CSV file.
                using (var reader = new StreamReader("E:/OneDrive - University of Lincoln/PROJECT/Dev/timetableExtract.csv"))
                {
                    using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        csvReader.Configuration.Delimiter = ";";
                        inputEvents = csvReader.GetRecords<TimetableEventFromExtract>().ToList();
                    }
                }

                // Get the allowed rooms to avoid creating events for rooms that don't exist.
                HashSet<string> possibleRooms = (await DissDatabaseContext.Nodes
                        .Where(node => node.Type == NodeType.Room)
                        .Select(node => node.NodeId)
                        .ToArrayAsync()).ToHashSet();

                // Create the output list.
                List<TimetableEvent> outputEvents = new List<TimetableEvent>(inputEvents.Count);

                // Convert each input to an output type.
                foreach (TimetableEventFromExtract input in inputEvents)
                {
                    TimetableEvent output = input.MapToTimetableEvent(possibleRooms);
                    if (output != default)
                    {
                        outputEvents.Add(output);
                    }
                }

                // Remove entire content of events table.
                DissDatabaseContext.TimetableEvents.RemoveRange(DissDatabaseContext.TimetableEvents);

                await DissDatabaseContext.SaveChangesAsync();

                // Insert the new data.
                DissDatabaseContext.TimetableEvents.AddRange(outputEvents);

                await DissDatabaseContext.SaveChangesAsync();

                return Created("", "");
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to generate a randomised set of timetable data.
        /// </summary>
        /// <remarks>
        /// Originally used before timetable extract data was available for testing. Now a candidate for deprecation.
        /// </remarks>
        [HttpPost]
        [Route("timetable/generate")]
        public async Task<IActionResult> GenerateTimetableData()
        {
            try
            {
                // Construct a new generator with some sensible contraints.
                TimetableGenerator generator = new TimetableGenerator(
                    DateTime.Now.Date,
                    DateTime.Now.AddDays(7).Date,
                    TimeSpan.FromHours(9),
                    TimeSpan.FromHours(18),
                    2,
                    await DissDatabaseContext.Nodes
                        .Where(node => node.Type == NodeType.Room && node.LeafletNodeType == "room")
                        .Select(node => node.NodeId)
                        .ToArrayAsync()
                    );

                // Generate the list of events for 20,000 students.
                List<TimetableEvent> output = generator.GenerateStudentTimetables(20000);

                // Remove entire content of events table.
                DissDatabaseContext.TimetableEvents.RemoveRange(DissDatabaseContext.TimetableEvents);

                await DissDatabaseContext.SaveChangesAsync();

                // Add the new dataset to the database.
                DissDatabaseContext.TimetableEvents.AddRange(output);

                // Save changes - long running operation due to network transfer.
                await DissDatabaseContext.SaveChangesAsync();

                return Created("", "");
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint used to fix corridor area values for transitions between indoors and outdoors.
        /// Called at after uploading the background map.
        /// <remarks>
        /// Because floors are uploaded one at a time, the area calculation for edges between indoors and outdoor cannot be done with only the data available at single floor upload time.
        /// This method gets all areas that have not yet been calculated and calculates them correctly.
        /// It is called after uploading the background map as this should be the point at which all other floor data exists.
        /// </remarks>
        /// </summary>
        [HttpPut]
        [Route("nodes/fixarea")]
        public async Task<IActionResult> CalculateCorridorAreaForBuildingTransitions()
        {
            try
            {
                // Get all corridor edges that haven't been assigned an area.
                NodeEdge[] allEdges = await DissDatabaseContext.NodeEdges
                    .Include(e => e.Node1)
                    .Include(e => e.Node2)
                    .Where(e => e.CorridorArea == null && e.Node1.Type == NodeType.Corridor && e.Node2.Type == NodeType.Corridor)
                    .ToArrayAsync();

                // Iterate these edges.
                foreach (NodeEdge edge in allEdges)
                {
                    // Calculate distance between these points.
                    double distance = SharedFunctions.GetDistanceFromLatLonInMeters(edge.Node1.Latitude.GetValueOrDefault(), edge.Node1.Longitude.GetValueOrDefault(), edge.Node2.Latitude.GetValueOrDefault(), edge.Node2.Longitude.GetValueOrDefault());
                    // Calculate average area between points.
                    // Get average width.
                    double width = (edge.Node1.CorridorWidth.GetValueOrDefault() + edge.Node2.CorridorWidth.GetValueOrDefault()) / 2;
                    // Calculate area.
                    edge.CorridorArea = width * distance;
                }

                if (allEdges.Length > 0)
                {
                    // Save if we made any changes.
                    await DissDatabaseContext.SaveChangesAsync();
                }

                return Ok();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to create a building.
        /// </summary>
        /// <param name="newBuilding">Building posted in the request body.</param>
        [HttpPost]
        [Route("building")]
        public async Task<IActionResult> CreateBuilding([FromBody] Building newBuilding)
        {
            try
            {
                // Check if building already exists.
                Building building = await DissDatabaseContext
                    .Buildings
                    .FirstOrDefaultAsync(b => b.BuildingCode.ToLower() == newBuilding.BuildingCode.ToLower());

                if (building == default)
                {
                    // Does not exist, insert.
                    DissDatabaseContext.Buildings.Add(newBuilding);
                }
                else
                {
                    // Exists, update.
                    building.BuildingName = newBuilding.BuildingName;
                }
                await DissDatabaseContext.SaveChangesAsync();

                return Created("", newBuilding.BuildingCode);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to delete a building.
        /// </summary>
        /// <param name="code">Unique building code of the building to delete.</param>
        [HttpDelete]
        [Route("building/{code}")]
        public async Task<IActionResult> DeleteBuilding(string code)
        {
            try
            {
                // Create an object with the same primary key. This allows us to delete without having to get the object from the database first.
                Building building = new Building { BuildingCode = code.ToLower() };
                DissDatabaseContext.Buildings.Remove(building);

                await DissDatabaseContext.SaveChangesAsync();

                return Ok();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to perform the JOSM to Leaflet conversion and extraction.
        /// </summary>
        /// <param name="josm">JOSM GeoJSON data.</param>
        [HttpPost]
        [Route("josm/josmtoleaflet")]
        public async Task<IActionResult> JosmToLeaflet([FromBody] FeatureCollection josm)
        {
            try
            {
                // Create a list of errors to be added to.
                List<string> errors = new List<string>();

                // Start a timer.
                DateTime start = DateTime.Now;

                // Make sure there is some data to extract.
                if (josm == default || josm.Features.Count == 0)
                {
                    errors.Add("JOSM JSON cannot be empty.");
                    return BadRequest(errors);
                }

                // Initialise a converter.
                Josm2Leaflet converter = new Josm2Leaflet();

                // Extract relevant features.
                List<Feature> features = converter.ParseJOSM(josm);

                // Make sure there are features in this GeoJSON to work with.
                if (features.Count > 0)
                {
                    // Serialise to JSON.
                    string result = JsonConvert.SerializeObject(new FeatureCollection(features));

                    // Write the JSON to a file on the server for serving later.
                    string path = await converter.WriteToFile(result, WebHostEnvironment);

                    // Make sure writing the file succeeded.
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        errors.Add("JOSM2Leaflet: Failed to write JSON file.");
                    }

                    // Stop the timer.
                    DateTime end = DateTime.Now;

                    // Return a message to the front-end for display to the user indicating processing time.
                    return Created(path, $"JOSM2Leaflet for {converter.filePrefix.ToUpper()} finished in {Math.Round((end - start).TotalMilliseconds, 2)}ms.");
                }
                return Created("", "No content to extract.");
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to perform JOSM to Leaflet for the background map.
        /// </summary>
        /// <remarks>
        /// This process is slightly different as we only need to remove all the corridor nodes from the GeoJSON to avoid displaying them.
        /// </remarks>
        /// <param name="josm">JOSM GeoJSON data.</param>
        [HttpPost]
        [Route("josm/background/josmtoleaflet")]
        public async Task<IActionResult> BackgroundJosmToLeaflet([FromBody] FeatureCollection josm)
        {
            try
            {
                // Start a timer.
                DateTime start = DateTime.Now;

                List<string> errors = new List<string>();

                // Make sure we have data to work with.
                if (josm == default || josm.Features.Count == 0)
                {
                    errors.Add("JOSM JSON cannot be empty.");
                    return BadRequest(errors);
                }

                // Create a new instance of the converter.
                Josm2Leaflet converter = new Josm2Leaflet();

                // Remove corridor nodes.
                List<Feature> features = converter.RemoveCorridorNodes(josm);

                // Serialise result.
                string result = JsonConvert.SerializeObject(new FeatureCollection(features));

                // Write to file.
                string path = await converter.WriteBackgroundToFile(result, WebHostEnvironment);

                // Ensure the file write succeeded.
                if (path == null)
                {
                    errors.Add("JOSM2Leaflet: Failed to write background JSON file.");
                    return BadRequest(errors);
                }

                // Stop the timer.
                DateTime end = DateTime.Now;
                return Created(path, $"JOSM2Leaflet for background finished in {Math.Round((end - start).TotalMilliseconds, 2)}ms.");
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to perform Node Extraction on the JOSM GeoJSON data.
        /// </summary>
        /// <param name="josm">JOSM GeoJSON data.</param>
        [HttpPost]
        [Route("nodes/extractnodes")]
        public async Task<IActionResult> ExtractNodes([FromBody] FeatureCollection josm)
        {
            try
            {
                // Start a timer.
                DateTime start = DateTime.Now;

                // Attempt to classify.
                if (!NodeClassifier.Classify(josm, out List<string> errors))
                {
                    return BadRequest(new { NodeExtractorErrors = errors });
                }

                // Validate the data.
                if (!NodeValidator.Validate(josm, out errors, out List<string> warnings))
                {
                    return BadRequest(new { NodeExtractorErrors = errors, NodeExtractorWarnings = warnings });
                }

                // Parse the JOSM data to nodes and edges.
                if (!NodeExtractor.ParseNodeSource(josm, out List<Node> nodes, out List<NodeEdge> edges))
                {
                    errors.Add("Parsing of JOSM failed!");
                    return BadRequest(new { NodeExtractorErrors = errors });
                }

                // Check for broken connections on this floor.
                List<string> brokenConnections = NodeExtractor.VerifyNodeEdgesSingleFloor(nodes, edges);

                if (brokenConnections.Count > 0)
                {
                    errors.AddRange(brokenConnections);
                    return BadRequest(new { NodeExtractorErrors = errors });
                }

                // Check for duplicates.
                foreach (Node node in nodes)
                {
                    int duplicateCount = nodes.Count(n => n.NodeId == node.NodeId);
                    if (duplicateCount > 1)
                    {
                        errors.Add($"Node Extractor: The node Id {node.NodeId} is duplicated {duplicateCount} times, Ids must be unique!");
                        return BadRequest(new { NodeExtractorErrors = errors });
                    }
                }

                // Write nodes to database.
                await WriteNodesToDatabase(nodes);

                // Calculate weights.
                edges = NodeExtractor.CalculateWeights(nodes, edges, AppSettings.EdgeCaseWeights);

                // Write edges to database.
                await WriteNodeEdgesToDatabase(edges);

                await DissDatabaseContext.SaveChangesAsync();

                // Stop the timer and return to the client a message indicating run time and warnings.
                DateTime end = DateTime.Now;
                return Created("", new { processingTime = $"Node Extractor for {nodes.FirstOrDefault()?.BuildingCode.ToUpper()}{nodes.FirstOrDefault()?.Floor} finished in: {Math.Round((end - start).TotalMilliseconds, 2)}ms.", warnings });
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Method to save nodes to the database.
        /// </summary>
        /// <remarks>
        /// This method performs an upsert of the node data.
        /// If a node already exists all it's non-key properties are updated.
        /// </remarks>
        /// <param name="nodes">List of nodes to save.</param>
        private async Task WriteNodesToDatabase(List<Node> nodes)
        {
            // Uses a HashSet to speed up `.Contains` existence check performance to O(1) from O(n) + database latency.
            HashSet<string> existingNodes = (await DissDatabaseContext.Nodes
                .AsNoTracking()
                .Select(n => n.NodeId)
                .ToArrayAsync())
                .ToHashSet();

            // Iterate the list of nodes to save.
            foreach (Node node in nodes)
            {
                // Check if it already exists in the database.
                if (existingNodes.Contains(node.NodeId))
                {
                    // Update.
                    DissDatabaseContext.Nodes.Update(node);
                }
                else
                {
                    // Insert.
                    DissDatabaseContext.Nodes.Add(node);
                }
            }
        }

        /// <summary>
        /// Method used to save node edges to the database.
        /// </summary>
        /// <param name="edges">Node edges to save.</param>
        private async Task WriteNodeEdgesToDatabase(List<NodeEdge> edges)
        {
            // Uses a Dictionary to speed up .ContainsKey existence check performance to O(1) from O(n) + database latency.
            Dictionary<(string, string), NodeEdge> existingEdges = (await DissDatabaseContext.NodeEdges
                .ToArrayAsync())
                .ToDictionary(e => (e.Node1Id, e.Node2Id));

            // Iterate the edges to save.
            foreach (NodeEdge edge in edges)
            {
                // Get this edge as a tuple of both ids.
                (string, string) thisEdge = (edge.Node1Id, edge.Node2Id);

                // Check if this edge exists in the database.
                if (existingEdges.ContainsKey(thisEdge))
                {
                    // Update.
                    existingEdges[thisEdge].Weight = edge.Weight;
                    existingEdges[thisEdge].CorridorArea = edge.CorridorArea;
                }
                else
                {
                    // Insert.
                    DissDatabaseContext.NodeEdges.Add(edge);
                    existingEdges.Add(thisEdge, edge);
                }
            }
        }
    }
}