using Driver.Models;
using Driver.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Text;

namespace Driver.Controllers
{
    [Route("api/[controller]")]
    public class DriverController : Controller
    {
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        readonly IWebHostEnvironment _environment;
        readonly DriverService _driver;
        public DriverController(ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            DriverService driver)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _configuration = configuration;
            _environment = environment;
            _driver = driver;
            refreshAll();
        }

        static oItem[] _folders = new oItem[] { };
        static oItem[] _files = new oItem[] { };

        [HttpGet("refresh")]
        public bool refreshAll()
        {
            var items = _driver._gooGetAll();
            _files = items.Where(x => x.mime_type != "application/vnd.google-apps.folder").ToArray();
            _folders = items.Where(x => x.mime_type == "application/vnd.google-apps.folder").ToArray();
            return true;
        }

        [HttpGet("files")]
        public oItem[] fileAll() => _files;

        [HttpGet("folders")]
        public oItem[] folderAll() => _folders;

        [HttpGet("file/text/{fileId}")]
        public string fileGetText(string fileId)
        {
            string s = string.Empty;
            var buf = _driver._gooFile_Download(fileId);
            if (buf != null) s = Encoding.UTF8.GetString(buf);
            return s;
        }


        [HttpDelete("file/delete/{fileId}")]
        public bool fileDelete(string fileId)
        {
            var ok = _driver._gooFile_Delete(fileId);
            if (ok) refreshAll();
            return ok;
        }

        [HttpPost("folder/create/{folderName}")]
        public oItem folderCreate(string folderName)
        {
            var v = _driver._gooFolder_createNew(folderName);
            if (v != null) refreshAll();
            return v;
        }

        [HttpPost("file/upload/{folderName}")]
        [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue)]
        public oItem folderUploadFile(IFormFile file, string folderName = "")
        {
            var file2 = HttpContext.Request.Form.Files;
            if (file == null && file2 != null && file2.Count > 0) file = file2[0];
            if (file != null)
            {
                string folderId = string.Empty;
                if (!string.IsNullOrEmpty(folderName))
                {
                    var dir = _folders.Where(x => x.name.ToLower() == folderName.ToLower()).Take(1).SingleOrDefault();
                    if (dir != null)
                        folderId = dir.id;
                }
                var v = _driver._gooFile_uploadToFolder(file, folderId);
                if (v != null) refreshAll();
                return v;
            }
            return null;
        }

    }
}
