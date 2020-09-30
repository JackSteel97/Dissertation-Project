using Dissertation.Models;
using Dissertation.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Dissertation.Controllers
{
    public class HomeController : Controller
    {
        private readonly DissDatabaseContext DissDatabaseContext;

        public HomeController(DissDatabaseContext dissDatabaseContext)
        {
            DissDatabaseContext = dissDatabaseContext;
        }

        [Route("")]
        [Route("home")]
        public IActionResult Index()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}