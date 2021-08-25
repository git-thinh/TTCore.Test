using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using Microsoft.AspNetCore.Hosting;
using PDF2Image.Services;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Text;
using Redis;
using StackExchange.Redis;

namespace PDF2Image.Controllers
{
    [Route("local")]
    public class LocalController : Controller
    {
        readonly IHubContext<ImageHub> _hubContext;
        readonly IWebHostEnvironment _environment;
        readonly OcrService _ocr;

        readonly IDatabase _redisRead;
        const string REDIS_RAW = "RAW";

        public LocalController(
            IHubContext<ImageHub> hubContext, 
            IWebHostEnvironment env, 
            OcrService ocr,
            RedisService redis)
        {
            _hubContext = hubContext;
            _environment = env;

            _ocr = ocr;
            _redisRead = redis.GetDB(REDIS_TYPE.READ1);
        }

        [HttpPost("push-files")]
        public async Task<string> pushFiles(string path)
        {
            string files = "";
            try
            {
                if (Request.Body != null && Request.Body.CanRead)
                {
                    Request.EnableBuffering();
                    files = await new StreamReader(Request.Body, Encoding.Unicode).ReadToEndAsync();
                    Request.Body.Position = 0;
                }
            }
            catch { }
            if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(files)) 
                _ocr.ZipRawPDF(path, files);
            return "OK";
        }

        [HttpGet("image/{id}")]
        public IActionResult image(string id)
        {
            byte[] buf = new byte[] { };
            if (_redisRead.HashExists(REDIS_RAW, id))
                buf = _redisRead.HashGet(REDIS_RAW, id);
            return File(buf, "image/webp");
        }
    }
}
