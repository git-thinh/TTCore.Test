using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using PDF2Image.Models;
using System.IO;
using System.Drawing;
using Microsoft.AspNetCore.Hosting;
using PDF2Image.Services;
using StackExchange.Redis;
using Redis;
using System.Threading.Tasks;

namespace PDF2Image.Controllers
{
    [Route("api/[controller]")]
    public class OcrController : Controller
    {
        readonly IHubContext<ImageHub> _hubContext;
        readonly IWebHostEnvironment _environment;
        readonly OcrService _ocr;

        readonly IDatabase _redisRead;
        const string REDIS_RAW = "RAW";

        public OcrController(
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

        [HttpPost("pdf/upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<dynamic> UploadFiles(IFormFile file)
        {
            var file2 = HttpContext.Request.Form.Files;
            if (file == null && file2 != null && file2.Count > 0) file = file2[0];
            if (file != null)
            {
                var ms = new MemoryStream();
                await file.OpenReadStream().CopyToAsync(ms);
                await _ocr.ZipRawPDF(ms.ToArray(), file.Name);
            }
            return new { Ok = true };
        }

        [HttpGet("image/{id}")]
        public IActionResult image(int id)
        {
            byte[] buf = new byte[] { };
            if (_redisRead.HashExists(REDIS_RAW, id))
                buf = _redisRead.HashGet(REDIS_RAW, id);
            return File(buf, "image/webp");
        }

        void __convertJpg(IFormFile file, OCR_TYPE type_ocr, int size = 100, int quality = 75)
        {
            //var m2 = new MemoryStream();
            //Bitmap.FromStream(file.OpenReadStream()).Save(m2, System.Drawing.Imaging.ImageFormat.Jpeg);
            //var b0 = m2.ToArray();

            //var b1 = Ocr.__grayImage(b0, type_ocr);
            //var b2 = Ocr.__ocrProcess(b1, OCR_TYPE.OPENCV);
            //var b3 = Ocr.__ocrProcess(b1, OCR_TYPE.TESSERACT);
            //_cache[0] = b1;
            //_cache[1] = b2;
            //_cache[2] = b3;

            //_hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", new ImageMessage { id = 0 }).GetAwaiter();
            //_hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", new ImageMessage { id = 1 }).GetAwaiter();
            //_hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", new ImageMessage { id = 2 }).GetAwaiter();
        }

    }
}
