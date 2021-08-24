using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using Microsoft.AspNetCore.Hosting;
using PDF2Image.Services;

namespace PDF2Image.Controllers
{
    [Route("local")]
    public class LocalController : Controller
    {
        readonly IHubContext<ImageHub> _hubContext;
        readonly IWebHostEnvironment _environment;
        readonly OcrService _ocr;
        public LocalController(IHubContext<ImageHub> hubContext, IWebHostEnvironment env, OcrService ocr)
        {
            _hubContext = hubContext;
            _environment = env;
            _ocr = ocr;
        }

        [HttpGet("push-files")]
        public string pushFiles(string path, string files)
        {
            if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(files)) 
                _ocr.ZipRawPDF(path, files);
            return "OK";
        }

        [HttpGet("image/{id}")]
        public IActionResult image(int id)
        {
            byte[] buf = new byte[] { };
            //if (id < _cache.Length)
            //    buf = _cache[id];
            return File(buf, "image/webp");
        }
    }
}
