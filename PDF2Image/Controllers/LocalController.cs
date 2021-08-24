using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using Microsoft.AspNetCore.Hosting;
using PDF2Image.Services;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Text;

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
        public IActionResult image(int id)
        {
            byte[] buf = new byte[] { };
            //if (id < _cache.Length)
            //    buf = _cache[id];
            return File(buf, "image/webp");
        }
    }
}
