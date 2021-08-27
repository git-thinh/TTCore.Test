using Driver.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Driver.Services
{
    public class DriverService : BackgroundService
    {
        readonly string ServiceName = nameof(DriverService);
        readonly ILogger _logger;
        readonly IConfiguration _configuration;
        readonly IWebHostEnvironment _environment;
        public DriverService(ILoggerFactory loggerFactory,
            IWebHostEnvironment env,
            IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _configuration = configuration;
            _environment = env;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _gooInit();
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{ServiceName} is starting.");
            stoppingToken.Register(() => _logger.LogInformation($"{ServiceName} background task is stopping."));

            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    if (_isRunning == false)
            //    {
            //        _isRunning = true;

            //        _isRunning = false;
            //    }
            //    await Task.Delay(500, stoppingToken);
            //}

            await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogDebug($"{ServiceName} is stopping.");
        }


        DriveService _gooService = null;

        public oItem _gooFolder_createNew(string folderName)
        {
            try
            {
                var FileMetaData = new Google.Apis.Drive.v3.Data.File();
                FileMetaData.Name = folderName;
                FileMetaData.MimeType = "application/vnd.google-apps.folder";

                var r = _gooService.Files.Create(FileMetaData);
                //r.Fields = "id";
                r.Fields = "*";
                var file = r.Execute();

                _gooPermission_Create(file.Id);

                return _toModelItem(file);
            }
            catch (Exception ex)
            {
            }
            return null;
        }

        public oItem _gooFile_uploadToFolder(IFormFile file, string folderId = "")
        {
            var f = new Google.Apis.Drive.v3.Data.File();
            f.Name = file.FileName;
            f.MimeType = file.ContentType;
            if (!string.IsNullOrEmpty(folderId))
                f.Parents = new List<string>() { folderId };

            var r = _gooService.Files.Create(f, file.OpenReadStream(), f.MimeType);
            //r.Fields = "id";
            r.Fields = "*";
            r.Upload();

            var v = r.ResponseBody;
            _gooPermission_Create(v.Id);
            return _toModelItem(v);
        }

        public oItem[] _gooGetAll()
        {
            var ls = new List<oItem>();
            var r = _gooService.Files.List();
            //r.PageSize = 2;
            //r.Fields = "nextPageToken, files(id, name)";
            r.Fields = "nextPageToken, files(*)";
            var files = r.Execute().Files;

            // For getting only folders    
            //files = files.Where(x => x.MimeType == "application/vnd.google-apps.folder").ToList();

            if (files != null && files.Count > 0)
                foreach (var file in files)
                    ls.Add(_toModelItem(file));
            return ls.ToArray();
        }

        public oItem _gooFolder_createFolderInFolder(string folderId, string folderName)
        {
            var FileMetaData = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(folderName),
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string>() { folderId }
            };
            var r = _gooService.Files.Create(FileMetaData);
            r.Fields = "*";
            var v = r.Execute();
            _gooPermission_Create(v.Id);
            return _toModelItem(v);
        }

        public bool _gooFile_Delete(string fileId)
        {
            try
            {
                var v = _gooService.Files.Delete(fileId).Execute();
                return true;
            }
            catch (Exception ex)
            {
            }
            return false;
        }

        public bool _gooFolder_Exist(string folderOrFileName)
        {
            var r = _gooService.Files.List();
            r.Fields = "nextPageToken, files(*)";
            var files = r.Execute().Files;
            files = files.Where(x => x.Name.ToLower() == folderOrFileName.ToLower()).ToList();
            if (files.Count > 0)
                return true;
            return false;
        }

        public byte[] _gooFile_Download(string fileId)
        {
            var r = _gooService.Files.Get(fileId);
            var v = r.Execute();
            string FileName = v.Name;
            byte[] buf = null;
            var stream = new MemoryStream();

            // Add a handler which will be notified on progress changes.    
            // It will notify on each chunk download and when the    
            // download is completed or failed.    
            r.MediaDownloader.ProgressChanged += (Google.Apis.Download.IDownloadProgress progress) =>
            {
                switch (progress.Status)
                {
                    case DownloadStatus.Downloading:
                        Console.WriteLine(progress.BytesDownloaded);
                        break;
                    case DownloadStatus.Completed:
                        buf = stream.ToArray();
                        break;
                    case DownloadStatus.Failed:
                        break;
                }
            };
            r.Download(stream);
            return buf;
        }

        public bool _gooFile_Move(string fileId, string folderId)
        {
            var getRequest = _gooService.Files.Get(fileId);
            getRequest.Fields = "parents";
            var file = getRequest.Execute();
            string previousParents = String.Join(",", file.Parents);

            // Move the file to the new folder    
            var updateRequest = _gooService.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
            updateRequest.Fields = "id, parents";
            updateRequest.AddParents = folderId;
            updateRequest.RemoveParents = previousParents;

            file = updateRequest.Execute();
            if (file != null)
                return true;
            return false;
        }

        public bool _gooFile_Copy(string fileId, string folderId)
        {
            // Retrieve the existing parents to remove    
            var r = _gooService.Files.Get(fileId);
            r.Fields = "parents";
            var file = r.Execute();

            // Copy the file to the new folder    
            var updateRequest = _gooService.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
            updateRequest.Fields = "id, parents";
            updateRequest.AddParents = folderId;
            //updateRequest.RemoveParents = previousParents;    
            file = updateRequest.Execute();
            if (file != null)
            {
                _gooPermission_Create(file.Id);
                return true;
            }

            return false;
        }

        public oItem _gooFile_Rename(string fileId, string newTitle)
        {
            try
            {
                var file = new Google.Apis.Drive.v3.Data.File();
                file.Name = newTitle;

                // Rename the file.    
                var request = _gooService.Files.Update(file, fileId);
                var v = request.Execute();
                return _toModelItem(v);
            }
            catch (Exception e)
            {
            }
            return null;
        }

        public oItem _gooFile_uploadProgress(IFormFile file, string folderId = "", string description = "")
        {
            var body = new Google.Apis.Drive.v3.Data.File();
            body.Name = file.FileName;
            body.Description = description;
            body.MimeType = file.ContentType;
            if (!string.IsNullOrEmpty(folderId))
                body.Parents = new List<string> { folderId };

            try
            {
                var r = _gooService.Files.Create(body, file.OpenReadStream(), file.ContentType);
                r.SupportsTeamDrives = true;
                r.ProgressChanged += (progress) =>
                {
                    // progress.Status + " " + progress.BytesSent;
                };
                r.ResponseReceived += (_file) =>
                {
                    // if (_file != null) "File was uploaded sucessfully--" + _file.Id;
                };
                r.Upload();

                var v = r.ResponseBody;
                _gooPermission_Create(v.Id);
                return _toModelItem(v);
            }
            catch (Exception e)
            {
            }
            return null;
        }

        Permission _gooPermission_Create(string itemId)
        {
            var p = new Permission();
            p.AllowFileDiscovery = false;
            p.Type = "anyone"; // "user", "group", "domain" or "default" "anyone"
            p.Role = "reader"; // "owner", "writer" or "reader"
            //p.EmailAddress = "abc@gmail.com";

            try
            {
                return _gooService.Permissions.Create(p, itemId).Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
            }
            return null;
        }

        oItem _toModelItem(Google.Apis.Drive.v3.Data.File file)
        {
            return new oItem()
            {
                id = file.Id,
                name = file.Name,
                created_time = file.CreatedTime == null ? 0 : long.Parse(((DateTime)file.CreatedTime).ToString("yyMMddHHmmss")),
                mime_type = file.MimeType,
                parents = file.Parents,
                resource_key = file.ResourceKey,
                shared = file.Shared == null ? false : (bool)file.Shared,
                size = file.Size == null ? 0 : (long)file.Size,
                content_link = file.WebContentLink,
                view_link = file.WebViewLink
            };
        }

        void _gooInit()
        {
            UserCredential credential = null;
            string credPath = _environment.WebRootPath + "\\token";
            if (!Directory.Exists(_environment.WebRootPath)) Directory.CreateDirectory(_environment.WebRootPath);
            if (!Directory.Exists(credPath)) Directory.CreateDirectory(credPath);

            string tokenActive = _configuration.GetSection("Driver:token_active").Value;
            string clientId = _configuration.GetSection("Driver:" + tokenActive + ":client_id").Value;
            string clientSecret = _configuration.GetSection("Driver:" + tokenActive + ":client_secret").Value;
            string appName = _configuration.GetSection("Driver:" + tokenActive + ":app_name").Value;
            string[] scopes = new string[] { DriveService.Scope.Drive, DriveService.Scope.DriveFile };

            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            }, scopes, tokenActive + ".json", CancellationToken.None, new FileDataStore(credPath)).Result;
            //using (var stream = new FileStream(_environment.WebRootPath + "\\token\\key.desktop.json", FileMode.Open, FileAccess.Read))
            //{
            //    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(GoogleClientSecrets.Load(stream).Secrets,
            //        scopes, tokenActive+ ".json", CancellationToken.None, new FileDataStore(credPath)).Result;
            //}

            _gooService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = appName,
            });
            _gooService.HttpClient.Timeout = TimeSpan.FromHours(5);
        }

    }
}
