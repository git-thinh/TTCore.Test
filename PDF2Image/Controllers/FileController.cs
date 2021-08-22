using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using PDF2Image.Models;
using PdfLibCore;
using System.IO;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PDF2Image.Controllers
{
    [Route("api/[controller]")]
    public class FileController : Controller
    {
        readonly static byte[][] _cache = new byte[999][];
        readonly IHubContext<ImageHub> _hubContext;
        public FileController(IHubContext<ImageHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost("upload/{size}/{quality}")]
        public dynamic UploadFiles(IFormFile file,int size = 100, int quality = 75)
        {
            var file2 = HttpContext.Request.Form.Files;
            if (file == null && file2 != null && file2.Count > 0) file = file2[0];
            if (file != null)
                __convertJpg(file, size, quality);
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

        void __convertJpg(IFormFile file, int size = 100, int quality = 75)
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
                                using (var ms2 = new MemoryStream())
                                {
                                    bitmap.Image.Save(ms2, System.Drawing.Imaging.ImageFormat.Png);
                                    var buf = TTCore.WebPImage.Convert.StreamToWebP(ms2, quality);

                                    var img = new ImageMessage()
                                    {
                                        id = i,
                                        w = pageWidth,
                                        h = pageHeight,
                                        total = total,
                                        quality = quality,
                                        size = buf.Length
                                    };
                                    //await _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", img);
                                    _cache[i] = buf;
                                    _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", img).GetAwaiter();
                                }
                            }
                        }

                        i++;
                        //if (i > 0) break;
                    }
                }
            }
        }


        static void __saveToJpeg(Stream image, string destination)
        {
            if (image == null)
            {
                return;
            }

            image.Position = 0;
            var bmpDecoder = new BmpDecoder();
            var img = bmpDecoder.Decode(Configuration.Default, image);
            if (img == null)
            {
                return;
            }
            var width = (int)(img.Width * 0.5D);
            var height = (int)(img.Height * 0.5D);
            img.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(width, height)
            }));

            using var ms = new MemoryStream();
            img.Save(ms, JpegFormat.Instance);
            img.Dispose();

            //File.WriteAllBytes(destination, ms.ToArray());
        }
    }
}
