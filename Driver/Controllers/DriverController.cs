using Driver.Models;
using Driver.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

            if (_rootFolder == null)
            {
                _rootFolder = _driver._gooGetRoot();
                refreshAll();
            }
        }
        static oItem _rootFolder = null;
        static oItem[] _items = new oItem[] { };

        [HttpGet("refresh")]
        public bool refreshAll()
        {
            _items = _driver._gooGetAll();
            foreach (var it in _items)
            {
                if (it.mime_type == "application/vnd.google-apps.folder") it.is_dir = true;
                if (it.parents != null && it.parents.IndexOf(_rootFolder.id) != -1)
                {
                    it.is_root = true;
                    it.back_id = _rootFolder.id;
                }
            }
            return true;
        }

        [HttpGet("root")]
        public oItem getRoot()
        {
            var item = _driver._gooGetRoot();
            return item;
        }

        [HttpGet("all")]
        public oItem[] fileAll() => _items;

        [HttpGet("file/text/{fileId}")]
        public string fileGetText(string fileId)
        {
            string s = string.Empty;
            var buf = _driver._gooFile_Download(fileId);
            if (buf != null) s = Encoding.UTF8.GetString(buf);
            return s;
        }


        [HttpGet("delete/{itemId}")]
        public bool itemDelete(string itemId)
        {
            var ok = _driver._goo_Delete(itemId);
            if (ok) refreshAll();
            return ok;
        }

        [HttpGet("folder/create/{folderName}")]
        public dynamic folderCreate(string folderName, string parentFolderId)
        {
            try
            {
                string folderId = string.Empty;
                if (!string.IsNullOrEmpty(parentFolderId))
                {
                    var dir = _items.Where(x => x.is_dir && x.id == parentFolderId).Take(1).SingleOrDefault();
                    if (dir == null) return new { Ok = false, Message = "Parent folder not exist" };
                }

                if (!string.IsNullOrEmpty(folderName))
                {
                    var v = _driver._gooFolder_createNew(folderName, parentFolderId);
                    if (v != null) refreshAll();
                    return v;
                }
                return new { Ok = false, Message = "Folder name not empty" };
            }
            catch (Exception ex) {
                return new { Ok = false, Message = ex.Message };
            }
        }

        [HttpPost("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue)]
        public oItem[] uploadFiles(IFormFile[] files)
        {
            var files2 = HttpContext.Request.Form.Files;
            if ((files == null || files.Length == 0) 
                && files2 != null && files2.Count > 0) 
                files = files2.ToArray();
            if (files != null && files.Length > 0)
            {
                var ls = new List<oItem>();
                for (int i = 0; i < files.Length; i++)
                {
                    var v = _driver._gooFile_uploadToFolder(files[i]);
                    ls.Add(v);
                }
                refreshAll();
                return ls.ToArray();
            }
            return new oItem[] { };
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
                    var dir = _items.Where(x => x.is_dir && x.name.ToLower() == folderName.ToLower()).Take(1).SingleOrDefault();
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
