using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using PDF2Image.Models;
using System.IO;
using System.Drawing;
using Microsoft.AspNetCore.Hosting;

namespace PDF2Image.Controllers
{
    [Route("api/[controller]")]
    public class OcrController : Controller
    {
        readonly static byte[][] _cache = new byte[999][];
        readonly IHubContext<ImageHub> _hubContext;
        readonly IWebHostEnvironment _environment;
        public OcrController(IHubContext<ImageHub> hubContext, IWebHostEnvironment env)
        {
            _hubContext = hubContext;
            _environment = env;
        }

        [HttpPost("upload/{type_ocr}/{size}/{quality}")]
        public dynamic UploadFiles(IFormFile file, int type_ocr, int size = 100, int quality = 75)
        {
            var file2 = HttpContext.Request.Form.Files;
            if (file == null && file2 != null && file2.Count > 0) file = file2[0];
            if (file != null)
                __convertJpg(file, (OCR_TYPE)type_ocr, size, quality);
            return new { Ok = true };
        }

        [HttpGet("image/{id}")]
        public IActionResult image(int id)
        {
            byte[] buf = new byte[] { };
            if (id < _cache.Length)
            {
                buf = _cache[id];
            }
            return File(buf, "image/webp");
        }

        void __convertJpg(IFormFile file, OCR_TYPE type_ocr, int size = 100, int quality = 75)
        {
            var m2 = new MemoryStream();
            Bitmap.FromStream(file.OpenReadStream()).Save(m2, System.Drawing.Imaging.ImageFormat.Jpeg);
            var b0 = m2.ToArray();

            var b1 = Ocr.__grayImage(b0, type_ocr);
            var b2 = Ocr.__ocrProcess(b1, OCR_TYPE.OPENCV);
            var b3 = Ocr.__ocrProcess(b1, OCR_TYPE.TESSERACT);
            _cache[0] = b1;
            _cache[1] = b2;
            _cache[2] = b3;

            _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", new ImageMessage { id = 0 }).GetAwaiter();
            _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", new ImageMessage { id = 1 }).GetAwaiter();
            _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", new ImageMessage { id = 2 }).GetAwaiter();
        }

    }
}
