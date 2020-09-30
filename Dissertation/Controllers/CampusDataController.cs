using Dissertation.Models;
using Dissertation.Models.Database;
using Dissertation.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dissertation.Controllers
{
    /// <summary>
    /// A controller to handle requests for campus geographical data.
    /// </summary>
    [Route("campusdata")]
    public class CampusDataController : Controller
    {
        private readonly DissDatabaseContext DissDatabaseContext;
        private readonly IWebHostEnvironment HostingEnvironment;

        /// <summary>
        /// Constructor for the campus data controller, using dependency injection.
        /// </summary>
        /// <param name="dbContext">Injected database context.</param>
        /// <param name="hostingEnvironment">Injected hosting environment details.</param>
        public CampusDataController(DissDatabaseContext dbContext, IWebHostEnvironment hostingEnvironment)
        {
            DissDatabaseContext = dbContext;
            HostingEnvironment = hostingEnvironment;
        }

        /// <summary>
        /// Index view, returns from the index.cshtml.
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Endpoint to get all corridor nodes.
        /// </summary>
        [HttpGet]
        [Route("corridors")]
        public async Task<IActionResult> GetCorridors()
        {
            try
            {
                // Select all corridor nodes from the database.
                return Ok(await DissDatabaseContext
                    .Nodes
                    .AsNoTracking()
                    .Include(n => n.OutgoingEdges)
                        .ThenInclude(e => e.Node2)
                    .Where(node => node.Type == NodeType.Corridor || node.Type == NodeType.Parking)
                    .ToArrayAsync());
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to get all buildings.
        /// </summary>
        [HttpGet]
        [Route("buildings")]
        public async Task<IActionResult> GetBuildings()
        {
            try
            {
                // Select all buildings from the database.
                return Ok(await DissDatabaseContext.Buildings
                    .AsNoTracking()
                    .OrderBy(b => b.BuildingName)
                    .ToArrayAsync());
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        /// <summary>
        /// Endpoint to get a map JSON file from the server.
        /// </summary>
        /// <param name="fileName">Filename to request, usually in the format {BuildingCode}{FloorNumber}</param>
        [HttpGet]
        [Route("map/{filename}")]
        public async Task<IActionResult> GetMap(string fileName)
        {
            try
            {
                // Make sure the request is valid.
                if (fileName == null)
                {
                    return BadRequest("Filename cannot be null.");
                }

                // Construct the filepath to the GeoJSON file store.
                string filePath = $"{HostingEnvironment.ContentRootPath}/Files/Json/{fileName}";

                // Check file exists.
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound($"The filename: {fileName} was not found.");
                }

                // Read file contents.
                string result = await System.IO.File.ReadAllTextAsync(filePath);

                return Ok(result);
            }
            catch (Exception exeception)
            {
                return StatusCode(500, exeception.Message);
            }
        }

        /// <summary>
        /// Endpoint to get all the rooms for destination or starting points.
        /// </summary>
        [HttpGet]
        [Route("rooms")]
        public async Task<IActionResult> GetRooms()
        {
            try
            {
                // Select all the room nodes and construct Room objects to only return necessary data.
                return Ok(await DissDatabaseContext
                    .Nodes
                    .AsNoTracking()
                    .Where(node => node.Type == NodeType.Room)
                    .OrderBy(r => r.NodeId)
                    .Select(r => new Room(r.NodeId, r.BuildingCode, r.Building.BuildingName, r.RoomName))
                    .ToArrayAsync());
            }
            catch (Exception exception)
            {
                return StatusCode(500, exception.Message);
            }
        }
    }
}