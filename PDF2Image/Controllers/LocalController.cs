using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using PDF2Image.Models;
using System.IO;
using System.Drawing;
using Microsoft.AspNetCore.Hosting;
using System.Linq;
using System.Threading.Tasks;

namespace PDF2Image.Controllers
{
    [Route("local")]
    public class LocalController : Controller
    {
        readonly static byte[][] _cache = new byte[999][];
        readonly IHubContext<ImageHub> _hubContext;
        readonly IWebHostEnvironment _environment;
        public LocalController(IHubContext<ImageHub> hubContext, IWebHostEnvironment env)
        {
            _hubContext = hubContext;
            _environment = env;
        }

        [HttpGet("push-files")]
        public async Task<string> pushFiles(string path, string files)
        {
            if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(files))
            {
                var a = files.Split('|').Select(x => Path.Combine(path, x)).ToArray();
                string file = a[0];
                if (System.IO.File.Exists(file))
                {
                    for (int i = 0; i < _cache.Length; i++) _cache[0] = null;

                    await _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", "IMAGE_CLEAR");
                    await _hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", "FILE" + Path.GetFileName(a[0]));
                    var buf = System.IO.File.ReadAllBytes(file);
                    Ocr.__PDF2Image(_hubContext, _cache, buf, 200);
                }
            }
            return "OK";
        }

        [HttpGet("image/{id}")]
        public IActionResult image(int id)
        {
            byte[] buf = new byte[] { };
            if (id < _cache.Length)
                buf = _cache[id];
            return File(buf, "image/webp");
        }
    }
}
