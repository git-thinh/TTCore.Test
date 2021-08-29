using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;

namespace Driver.Controllers
{
    [Route("api/[controller]")]
    public class FileController : Controller
    {
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        readonly IWebHostEnvironment _environment;
        public FileController(ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet("files")]
        public string[] getFiles(string dir)
        {
            var a = new string[] { };
            string path = Path.Combine(_environment.WebRootPath, dir);
            if (Directory.Exists(path)) 
                a = Directory.GetFiles(path).Select(x => Path.GetFileName(x)).ToArray();
            return a;
        }


    }
}
