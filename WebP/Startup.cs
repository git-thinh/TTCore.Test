using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net;

namespace WebP
{
    public class Startup
    {
        public byte[] __convertWebP(string type, string url, int quality = 75)
        {
            byte[] buf = null;
            Stream stream = null;
            try
            {
                if (type == "file" && File.Exists(url))
                    stream = new FileStream(url, FileMode.Open);
                else if (type == "url")
                {
                    var bs = new WebClient().DownloadData(url);
                    stream = new MemoryStream(bs);
                }
                if (stream != null)
                {
                    buf = TTCore.WebPImage.Convert.StreamToWebP(stream, quality);
                }
            }
            catch { }
            return buf;
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    string type = context.Request.Query["type"];
                    string url = context.Request.Query["url"];
                    string squality = context.Request.Query["quality"];
                    int quality = 75;
                    int.TryParse(squality, out quality);
                    if (quality <= 0) quality = 75;

                    if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(url))
                    {
                        var buf = __convertWebP(type, url, quality);
                        if (buf != null)
                        {
                            context.Response.ContentType = "image/webp";
                            await context.Response.Body.WriteAsync(buf, 0, buf.Length);
                            return;
                        }
                    }
                    await context.Response.WriteAsync(string.Empty);
                });
            });
        }
    }
}
