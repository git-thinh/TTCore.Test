using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Tesseract;
using System.Drawing;
using System;
using OpenCvSharp;
using PdfLibCore;
using Microsoft.AspNetCore.SignalR;
using PDF2Image.Hubs;
using PDF2Image.Models;

namespace PDF2Image
{
    public enum OCR_TYPE
    {
        ACCORD = 0,
        OPENCV = 1,
        TESSERACT = 2
    }

    public class Ocr
    {
        public static void __PDF2Image(IHubContext<ImageHub> hubContext, byte[][] _cache, byte[] bytes, int size = 100, int maxPage = 100000)
        {
            try
            {
                using (var pdfDocument = new PdfDocument(bytes))
                {
                    int total = pdfDocument.Pages.Count;
                    int i = 0;
                    byte[] buf = null;
                    foreach (var page in pdfDocument.Pages)
                    {
                        using (page)
                        {
                            var pageWidth = (int)(size * page.Size.Width / 96F);
                            var pageHeight = (int)(size * page.Size.Height / 96F);
                            using (var bitmap = new PdfiumBitmap(pageWidth, pageHeight, false))
                            {
                                page.Render(bitmap);
                                //SaveToJpeg(bitmap.AsBmpStream(196D, 196D), Path.Combine(destination, $"{i++}.jpeg"));
                                //bitmap.Image.Save($"{i}.jpeg");
                                //var ms = new MemoryStream();
                                //var img = bitmap.Image;
                                //img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                                var raw = bitmap.Image.ToByteArray(System.Drawing.Imaging.ImageFormat.Jpeg);
                                buf = raw;


                                _cache[i] = buf;
                                var o = new ImageMessage()
                                {
                                    id = i,
                                    w = pageWidth,
                                    h = pageHeight,
                                    total = total,
                                    quality = 0,
                                    size = buf.Length
                                };
                                hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", o).GetAwaiter();
                            }
                        }
                        i++;
                        if (i > maxPage) break;
                    }
                }
            }
            catch (Exception ex) { 
            }
        }

        public static byte[] __grayImage(byte[] bytes, OCR_TYPE type = OCR_TYPE.ACCORD)
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
                        break;
                    case OCR_TYPE.OPENCV:
                        var src = Mat.FromImageData(bytes);
                        Mat gray = new Mat(src.Size(), MatType.CV_8UC1);
                        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2HLS);
                        //Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2HSV);
                        //Cv2.CvtColor(src, gray, ColorConversionCodes.BGRA2GRAY);
                        Mat dst = gray.Threshold(120, 255, ThresholdTypes.Binary);
                        dst.WriteToStream(m2);
                        //buf = __grayImage(m2.ToArray(), OCR_TYPE.ACCORD);
                        buf = m2.ToArray();
                        break;
                }
            }
            catch (Exception ex)
            {
            }
            return buf;
        }

        public static byte[] __ocrProcess(byte[] bytes, OCR_TYPE type = OCR_TYPE.ACCORD)
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

        public static byte[] __drawRectangle(Bitmap image, System.Drawing.Rectangle[] rectangles)
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
                Size = new SixLabors.ImageSharp.Size(width, height)
            }));

            using var ms = new MemoryStream();
            img.Save(ms, JpegFormat.Instance);
            img.Dispose();

            //File.WriteAllBytes(destination, ms.ToArray());
        }
    }
}
