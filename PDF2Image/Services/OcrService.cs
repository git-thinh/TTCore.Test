using Docnet.Core;
using Docnet.Core.Converters;
using Docnet.Core.Models;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenCvSharp;
using PDF2Image.Hubs;
using PDF2Image.Models;
using Redis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        #region [ MAIN ]

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
        const string REDIS_RAW = "RAW";
        const string REDIS_ERROR = "ERROR";

        int QUEUE_COUNTER = 0;
        readonly LibFixture _fixture;

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
                    {
                        string file = keys[0];
                        await _zipRawBook(file);
                        await _redisWrite.HashDeleteAsync(REDIS_PDF, file);
                    }

                    _isRunning = false;
                }
                await Task.Delay(500, stoppingToken);
            }

            //await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogDebug($"{ServiceName} is stopping.");
        }
        
        #endregion

        public async Task ZipRawPDF(byte[] bs, string fileName, string category = "", string fullPath = "")
        {
            int i = 0, total = 0;
            var r = new RawBook();
            var _error = new StringBuilder();
            fileName = Path.GetFileNameWithoutExtension(fileName);

            try
            {
                using (var doc = DocLib.Instance.GetDocReader(bs, new PageDimensions(1080, 1920)))
                {
                    total = doc.GetPageCount();

                    r.Page = total;
                    r.Categories = category;
                    r.FileName = fileName;
                    r.Path = fullPath;
                }
            }
            catch (Exception e0)
            {
                total = 0;
                string err = "ERROR_PDF_INFO: " + Environment.NewLine +
                    fileName + Environment.NewLine +
                    i.ToString() + Environment.NewLine +
                    e0.Message + Environment.NewLine + e0.StackTrace + Environment.NewLine + Environment.NewLine;
                _error.Append(err);
                await _hubContext.Clients.All.SendAsync("RAW_ERROR", err);
            }

            if (total > 0)
            {
                await _hubContext.Clients.All.SendAsync("RAW_FILE", r);

                await _redisWrite.KeyDeleteAsync(REDIS_TEMP);
                await _redisWrite.KeyDeleteAsync(REDIS_RAW);
                await _redisWrite.HashDeleteAsync(REDIS_ERROR, fileName);

                for (i = 0; i < total; i++)
                {
                    try
                    {
                        var pdf = DocLib.Instance.Split(bs, i, i);
                        if (pdf != null)
                            await _redisWrite.HashSetAsync(REDIS_TEMP, i.ToString(), pdf);
                    }
                    catch (Exception e2)
                    {
                        string err = "ERROR_PDF_SPLIT: " + Environment.NewLine +
                            fileName + Environment.NewLine +
                            i.ToString() + Environment.NewLine +
                            e2.Message + Environment.NewLine + e2.StackTrace + Environment.NewLine + Environment.NewLine;
                        _error.Append(err);
                        await _hubContext.Clients.All.SendAsync("RAW_ERROR", err);
                    }
                }

                var tran = new NaiveTransparencyRemover(255, 255, 255);
                for (i = 0; i < total; i++)
                {
                    try
                    {
                        if (_redisRead.HashExists(REDIS_TEMP, i.ToString()))
                        {
                            byte[] pdfBuffer = _redisRead.HashGet(REDIS_TEMP, i.ToString());
                            if (pdfBuffer != null && pdfBuffer.Length > 0)
                            {
                                using (var doc = DocLib.Instance.GetDocReader(pdfBuffer, new PageDimensions(1080, 1920)))
                                using (var page = doc.GetPageReader(0))
                                {
                                    if (page != null)
                                    {
                                        int w = page.GetPageWidth();
                                        int h = page.GetPageHeight();

                                        //var temp = page.GetImage();
                                        //var temp = page.GetImage(new NaiveTransparencyRemover(120, 120, 0));
                                        var temp = page.GetImage(tran);
                                        //var temp = page.GetImage(RenderFlags.RenderAnnotations | RenderFlags.Grayscale);

                                        using var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                                        AddBytes(bitmap, temp);
                                        w = bitmap.Width;
                                        h = bitmap.Height;

                                        //var characters = page.GetCharacters();
                                        //DrawRectangles(bitmap, characters);

                                        //SaveToJpeg(bitmap.AsBmpStream(196D, 196D), Path.Combine(destination, $"{i++}.jpeg"));

                                        //var gray = __grayImage(raw, OCR_TYPE.ACCORD);
                                        //var buf = __ocrProcess(gray, OCR_TYPE.TESSERACT);

                                        var raw = bitmap.ToByteArray(System.Drawing.Imaging.ImageFormat.Png);
                                        var buf = TTCore.WebPImage.Convert.StreamToWebP(new MemoryStream(raw), 60);
                                        //var buf = raw;
                                        var img = new ImageMessage()
                                        {
                                            id = i,
                                            w = w,
                                            h = h,
                                            total = total,
                                            quality = 0,
                                            size = raw.Length,
                                            size_min = buf.Length
                                        };
                                        if (raw != null)
                                        {
                                            await _redisWrite.HashSetAsync(REDIS_RAW, i.ToString(), buf);
                                            await _hubContext.Clients.All.SendAsync("RAW_PROCESS", img);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e2)
                    {
                        string err = "ERROR_PDF_2_IMAGE: " + Environment.NewLine +
                            fileName + Environment.NewLine +
                            i.ToString() + Environment.NewLine +
                            e2.Message + Environment.NewLine + e2.StackTrace + Environment.NewLine + Environment.NewLine;
                        _error.Append(err);
                        await _hubContext.Clients.All.SendAsync("RAW_ERROR", err);
                    }
                }

                try
                {
                    __zip(_rawPath, r, _rawPassword);
                    await _hubContext.Clients.All.SendAsync("RAW_DONE", new { File = fileName, Queue = QUEUE_COUNTER });
                }
                catch (Exception e3)
                {
                    string err = "ERROR_IMAGE_ZIP: " + Environment.NewLine +
                        fileName + Environment.NewLine +
                        e3.Message + Environment.NewLine + e3.StackTrace + Environment.NewLine + Environment.NewLine;
                    _error.Append(err);
                    await _hubContext.Clients.All.SendAsync("RAW_ERROR", err);
                }
            }

            if (_error.Length > 0)
            {
                _redisWrite.HashSet(REDIS_ERROR, fileName, _error.ToString());
                _error.Clear();
            }

            await _redisWrite.KeyDeleteAsync(REDIS_TEMP);
        }

        async Task _zipRawBook(string file)
        {
            var bs = File.ReadAllBytes(file);
            string fileName = Path.GetFileNameWithoutExtension(file);
            string category = Path.GetFileName(Path.GetDirectoryName(file));
            await ZipRawPDF(bs, fileName, category, file);
        }

        #region [ METHOD ]

        static void AddBytes(Bitmap bmp, byte[] rawBytes)
        {
            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);

            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            var pNative = bmpData.Scan0;

            Marshal.Copy(rawBytes, 0, pNative, rawBytes.Length);
            bmp.UnlockBits(bmpData);
        }

        static void DrawRectangles(Bitmap bmp, IEnumerable<Character> characters)
        {
            var pen = new Pen(System.Drawing.Color.Red);

            using var graphics = Graphics.FromImage(bmp);

            foreach (var c in characters)
            {
                var rect = new System.Drawing.Rectangle(c.Box.Left, c.Box.Top, c.Box.Right - c.Box.Left, c.Box.Bottom - c.Box.Top);
                graphics.DrawRectangle(pen, rect);
            }
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
                    byte[] bs = _redisRead.HashGet(REDIS_RAW, i.ToString());
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

        #endregion
    }

    public sealed class LibFixture : IDisposable
    {
        public LibFixture()
        {
            Lib = DocLib.Instance;
        }

        public void Dispose()
        {
            Lib.Dispose();
        }

        public IDocLib Lib { get; }
    }

    public static class ImageExtensions
    {
        public static byte[] ToByteArray(this System.Drawing.Image image,
            System.Drawing.Imaging.ImageFormat format = null)
        {
            using var ms = new MemoryStream();
            image.Save(ms, format ?? System.Drawing.Imaging.ImageFormat.Jpeg);
            return ms.ToArray();
        }
    }
}
