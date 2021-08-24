using ICSharpCode.SharpZipLib.Zip;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenCvSharp;
using PDF2Image.Hubs;
using PDF2Image.Models;
using PdfLibCore;
using PdfLibCore.Enums;
using Redis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace PDF2Image.Services
{
    public enum OCR_TYPE
    {
        ACCORD = 0,
        OPENCV = 1,
        TESSERACT = 2
    }

    public class OcrService : BackgroundService
    {
        readonly ILogger _logger;
        readonly IHubContext<ImageHub> _hubContext;
        readonly string ServiceName = nameof(OcrService);
        readonly IConfiguration _configuration;
        readonly string _rawPassword = string.Empty;
        readonly string _rawPath = string.Empty;

        readonly RedisService _redis;
        readonly IDatabase _redisWrite;
        readonly IDatabase _redisRead;
        const string REDIS_PDF = "PDF";
        const string REDIS_TEMP = "TEMP";
        const string REDIS_ERROR = "ERROR";
        int QUEUE_COUNTER = 0;

        public OcrService(
            ILoggerFactory loggerFactory,
            IHubContext<ImageHub> hubContext,
            IConfiguration configuration,
            RedisService redis
            )
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _hubContext = hubContext;

            _redis = redis;
            _redisWrite = _redis.GetDB(REDIS_TYPE.WRITE);
            _redisRead = _redis.GetDB(REDIS_TYPE.READ1);

            _configuration = configuration;
            _rawPassword = configuration.GetSection("AppSetting:Raw:Password").Value;
            _rawPath = configuration.GetSection("AppSetting:Raw:Dir1").Value;
            if (string.IsNullOrEmpty(_rawPath))
                _rawPath = configuration.GetSection("AppSetting:Raw:Dir2").Value;
            if (!Directory.Exists(_rawPath))
                Directory.CreateDirectory(_rawPath);
        }

        bool _isRunning = false;
        public void ZipRawPDF(string path, string files)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(files)) return;

            var a = files.Split('|').Select(x => Path.Combine(path, x)).ToArray();
            foreach (var f in a)
                if (File.Exists(f))
                    _redisWrite.HashSet(REDIS_PDF, f.ToLower(), string.Empty);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"{ServiceName} is starting.");
            stoppingToken.Register(() => _logger.LogInformation($"{ServiceName} background task is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_isRunning == false)
                {
                    _isRunning = true;

                    var keys = _redisRead.HashKeys(REDIS_PDF);
                    QUEUE_COUNTER = keys.Length;
                    if (QUEUE_COUNTER > 0)
                        await _zipRawBook(keys[0]);

                    _isRunning = false;
                }
                await Task.Delay(500, stoppingToken);
            }

            //await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogDebug($"{ServiceName} is stopping.");
        }

        async Task _zipRawBook(string file)
        {
            int i = 0;
            try
            {
                _redisWrite.KeyDelete(REDIS_TEMP);

                string fileName = Path.GetFileNameWithoutExtension(file);

                using (var doc = new PdfDocument(file))
                {
                    int total = doc.Pages.Count;

                    var r = new RawBook();
                    r.Page = total;
                    r.FileName = fileName;
                    r.Categories = Path.GetFileName(Path.GetDirectoryName(file));

                    r.Title = doc.GetMetaText(MetadataTags.Title);
                    r.Author = doc.GetMetaText(MetadataTags.Author);
                    r.Subject = doc.GetMetaText(MetadataTags.Subject);
                    r.Keywords = doc.GetMetaText(MetadataTags.Keywords);
                    r.Creator = doc.GetMetaText(MetadataTags.Creator);
                    r.Producer = doc.GetMetaText(MetadataTags.Producer);
                    r.CreationDate = doc.GetMetaText(MetadataTags.CreationDate);
                    r.ModDate = doc.GetMetaText(MetadataTags.ModDate);

                    await _hubContext.Clients.All.SendAsync("RAW_FILE", r);


                    foreach (var page in doc.Pages)
                    {
                        byte[] buf = null;
                        ImageMessage img = null;
                        using (page)
                        {
                            var ps = page.Size;
                            int w = 1200, h = (int)(w * ps.Height / ps.Width);
                            if (ps.Width > ps.Height)
                            {
                                w = w * 2;
                                h = h * 2;
                            }
                            using (var bitmap = new PdfiumBitmap(w, h, false))
                            {
                                page.Render(bitmap);
                                //SaveToJpeg(bitmap.AsBmpStream(196D, 196D), Path.Combine(destination, $"{i++}.jpeg"));
                                var raw = bitmap.Image.ToByteArray(System.Drawing.Imaging.ImageFormat.Png);
                                //var gray = __grayImage(raw, OCR_TYPE.ACCORD);
                                //buf = __ocrProcess(gray, OCR_TYPE.TESSERACT);
                                buf = TTCore.WebPImage.Convert.StreamToWebP(new MemoryStream(raw), 60);

                                img = new ImageMessage()
                                {
                                    id = i,
                                    w = w,
                                    h = h,
                                    total = total,
                                    quality = 0,
                                    size = raw.Length,
                                    size_min = buf.Length
                                };
                            }
                        }

                        if (buf != null) _redisWrite.HashSet(REDIS_TEMP, i.ToString(), buf);
                        if (img != null) await _hubContext.Clients.All.SendAsync("RAW_PROCESS", img);

                        i++;
                    }

                    __zip(_rawPath, r, _rawPassword);
                }
                await _hubContext.Clients.All.SendAsync("RAW_DONE", new { File = file, Queue = QUEUE_COUNTER });
            }
            catch (Exception ex)
            {
                string err =
                    file + Environment.NewLine +
                    i.ToString() + Environment.NewLine +
                    ex.Message + Environment.NewLine + ex.StackTrace;
                _redisWrite.HashSet(REDIS_ERROR, file, err);
                await _hubContext.Clients.All.SendAsync("RAW_ERROR", err);
            }

            _redisWrite.HashDelete(REDIS_PDF, file);
            _redisWrite.KeyDelete(REDIS_TEMP);
        }

        void __zip(string path, RawBook r, string pass)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string file = path + r.FileName + ".raw";

            if (File.Exists(file)) File.Delete(file);

            using (var fs = File.Create(file))
            using (var outStream = new ZipOutputStream(fs))
            {
                outStream.SetLevel(9);
                outStream.Password = pass;

                outStream.PutNextEntry(new ZipEntry("0.json"));
                outStream.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(r)));

                for (int i = 0; i < r.Page; i++)
                {
                    byte[] bs = _redisRead.HashGet(REDIS_TEMP, i.ToString());
                    if (bs != null && bs.Length > 0)
                    {
                        outStream.PutNextEntry(new ZipEntry(i.ToString() + ".raw"));
                        outStream.Write(bs);
                    }
                }
            }
        }

        byte[] __grayImage(byte[] bytes, OCR_TYPE type = OCR_TYPE.ACCORD)
        {
            byte[] buf = new byte[] { };
            if (bytes == null || bytes.Length == 0) return buf;
            try
            {
                var m2 = new MemoryStream();
                switch (type)
                {
                    case OCR_TYPE.ACCORD:
                        var image = (Bitmap)Bitmap.FromStream(new MemoryStream(bytes));
                        var gfilter = new AForge.Imaging.Filters.Grayscale(0.1125, 0.6154, 0.0521);
                        //var gfilter = new AForge.Imaging.Filters.Grayscale(0.2125, 0.7154, 0.0721);
                        //var gfilter = new AForge.Imaging.Filters.Grayscale(0.9125, 0.7154, 0.0721); //White
                        var ifilter = new AForge.Imaging.Filters.Invert();
                        var thfilter = new AForge.Imaging.Filters.BradleyLocalThresholding();
                        var bmp = gfilter.Apply(image);
                        thfilter.ApplyInPlace(bmp);
                        ifilter.ApplyInPlace(bmp);
                        bmp.Save(m2, System.Drawing.Imaging.ImageFormat.Jpeg);
                        buf = m2.ToArray();

                        //var v = (Bitmap)bmp.Clone();
                        //__replaceColor(v, System.Drawing.Color.Black, System.Drawing.Color.Blue);
                        //v.Save(m2, System.Drawing.Imaging.ImageFormat.Jpeg);
                        //buf = m2.ToArray();
                        break;
                    case OCR_TYPE.OPENCV:
                        //var src = Mat.FromImageData(bytes);
                        //Mat gray = new Mat(src.Size(), MatType.CV_8UC1);
                        //Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2HLS);
                        ////Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2HSV);
                        ////Cv2.CvtColor(src, gray, ColorConversionCodes.BGRA2GRAY);
                        //Mat dst = gray.Threshold(120, 255, ThresholdTypes.Binary);
                        //dst.WriteToStream(m2);
                        ////buf = __grayImage(m2.ToArray(), OCR_TYPE.ACCORD);
                        //buf = m2.ToArray();
                        break;
                }
            }
            catch (Exception ex)
            {
            }
            return buf;
        }

        byte[] __ocrProcess(byte[] bytes, OCR_TYPE type = OCR_TYPE.ACCORD)
        {
            byte[] buf = new byte[] { };
            if (bytes == null || bytes.Length == 0) return buf;
            try
            {
                switch (type)
                {
                    case OCR_TYPE.OPENCV:
                        var src = Mat.FromImageData(bytes, ImreadModes.AnyColor);
                        Mat gray = new Mat(src.Size(), MatType.CV_8UC1);
                        //Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2HLS);
                        //Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2HSV);
                        Cv2.CvtColor(src, gray, ColorConversionCodes.BGRA2GRAY);
                        Mat dst = gray.Threshold(150, 255, ThresholdTypes.Binary);
                        var m2 = new MemoryStream();
                        dst.WriteToStream(m2);
                        buf = m2.ToArray();
                        break;
                    case OCR_TYPE.TESSERACT:
                        //string path = Path.Combine(_environment.WebRootPath, "tessdata");
                        string path = "./wwwroot/tessdata";
                        using (var engine = new TesseractEngine(path, "vie", EngineMode.Default))
                        using (var img = Pix.LoadFromMemory(bytes))
                        using (var page = engine.Process(img))
                        {
                            //string s = page.GetText();
                            var rs = page.GetSegmentedRegions(PageIteratorLevel.TextLine);
                            buf = __drawRectangle((Bitmap)Bitmap.FromStream(new MemoryStream(bytes)), rs.ToArray());
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
            }
            return buf;
        }

        byte[] __drawRectangle(Bitmap image, System.Drawing.Rectangle[] rectangles)
        {
            var brush = new SolidBrush(System.Drawing.Color.FromArgb(50, System.Drawing.Color.Red));
            using (Graphics g = Graphics.FromImage(image))
                for (int i = 0; i < rectangles.Length; i++)
                    g.FillRectangle(brush, rectangles[i]);
            var ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            var buf = ms.ToArray();
            return buf;
        }

        void __saveToJpeg(Stream image, string destination)
        {
            if (image == null)
            {
                return;
            }

            image.Position = 0;
            var bmpDecoder = new SixLabors.ImageSharp.Formats.Bmp.BmpDecoder();
            var img = bmpDecoder.Decode(SixLabors.ImageSharp.Configuration.Default, image);
            if (img == null)
            {
                return;
            }
            var width = (int)(img.Width * 0.5D);
            var height = (int)(img.Height * 0.5D);
            img.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new SixLabors.ImageSharp.Size(width, height)
            }));

            using var ms = new MemoryStream();
            img.Save(ms, SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance);
            img.Dispose();

            //File.WriteAllBytes(destination, ms.ToArray());
        }
    }
}
