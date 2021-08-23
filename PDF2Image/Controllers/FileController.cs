using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using PDF2Image.Models;
using PdfLibCore;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace PDF2Image.Controllers
{
    [Route("api/[controller]")]
    public class FileController : Controller
    {
        readonly static byte[][] _cache = new byte[999][];
        readonly IHubContext<ImageHub> _hubContext;
        readonly IWebHostEnvironment _environment;
        public FileController(IHubContext<ImageHub> hubContext, IWebHostEnvironment env)
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
            using (var ms = new MemoryStream())
            {
                file.CopyTo(ms);
                using (var pdfDocument = new PdfDocument(ms.ToArray()))
                {
                    int total = pdfDocument.Pages.Count;
                    int i = 0;
                    foreach (var page in pdfDocument.Pages)
                    {
                        using (page)
                        {
                            var pageWidth = (int)(size * page.Size.Width / 96F);
                            var pageHeight = (int)(size * page.Size.Height / 96F);
                            //var pageWidth = (int)page.Size.Width;
                            //var pageHeight = (int)page.Size.Height;

                            using (var bitmap = new PdfiumBitmap(pageWidth, pageHeight, false))
                            {
                                page.Render(bitmap);
                                //SaveToJpeg(bitmap.AsBmpStream(196D, 196D), Path.Combine(destination, $"{i++}.jpeg"));
                                //bitmap.Image.Save($"{i}.jpeg");
                                using (var m2 = new MemoryStream())
                                {
                                    bitmap.Image.Save(m2, System.Drawing.Imaging.ImageFormat.Jpeg);

                                    var gray = Ocr.__grayImage(m2.ToArray(), type_ocr);
                                    var buf = Ocr.__ocrProcess(gray, OCR_TYPE.TESSERACT);

                                    _cache[i] = buf;
                                    var o = new ImageMessage()
                                    {
                                        id = i,
                                        w = pageWidth,
                                        h = pageHeight,
                                        total = total,
                                        quality = quality,
                                        size = buf.Length
                                    };
                                    _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", o).GetAwaiter();
                                }
                            }
                        }

                        i++;
                        if (i > 3) break;
                    }
                }
            }
        }

    }
}
