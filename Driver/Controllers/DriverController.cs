using Driver.Models;
using Driver.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Driver.Controllers
{
    [Route("api/[controller]")]
    public class DriverController : Controller
    {
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        readonly DriverService _driver;
        public DriverController(ILoggerFactory loggerFactory,
            IConfiguration configuration,
            DriverService driver)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _configuration = configuration;
            _driver = driver;

            refreshAll();
        }

        static oItem[] _folders = new oItem[] { };
        static oItem[] _files = new oItem[] { };

        [HttpGet("refresh")]
        public bool refreshAll()
        {
            _files = _driver._gooFile_getAll();
            _folders = _driver._gooFolder_getAll();
            return true;
        }

        [HttpGet("files")]
        public oItem[] allFiles() => _files;
        [HttpGet("folders")]
        public oItem[] allFolder() => _folders;

        [HttpGet("file/{fileId}")]
        public string getFile(string fileId)
        {
            string s = string.Empty;
            var buf = _driver._gooFile_downloadFile(fileId);
            if (buf != null) s = Encoding.UTF8.GetString(buf);
            return s;
        }

        [HttpDelete("file/{fileId}")]
        public bool deleteFile(string fileId) => _driver._gooDeleteFile(fileId);

        [HttpPost("folder/create/{folderName}")]
        public oItem folderCreate(string folderName) => _driver._gooFolder_createNew(folderName);

        [HttpPost("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue)]
        public oItem uploadFile(IFormFile file)
        {
            var file2 = HttpContext.Request.Form.Files;
            if (file == null && file2 != null && file2.Count > 0) file = file2[0];
            if (file != null)
                return _driver._gooRoot_uploadFile(file);
            return null;
        }

        [HttpPost("upload/{folderName}")]
        [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue)]
        public oItem folderUploadFile(string folderName, IFormFile file)
        {
            var dir = _folders.Where(x => x.name.ToLower() == folderName.ToLower()).Take(1).SingleOrDefault();
            if (dir != null)
            {
                var file2 = HttpContext.Request.Form.Files;
                if (file == null && file2 != null && file2.Count > 0) file = file2[0];
                if (file != null)
                    return _driver._gooFolder_uploadFile(dir, file);
            }
            return null;
        }

    }
}
