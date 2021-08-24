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
using System.Drawing.Imaging;

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
        public static void __PDF2Image(IHubContext<ImageHub> hubContext, byte[][] _cache, byte[] bytes, 
            int size = 100, int maxPage = 5)
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
                                _cache[i] = buf;
                                var o = new ImageMessage()
                                {
                                    id = i,
                                    w = w,
                                    h = h,
                                    total = total,
                                    quality = 0,
                                    size = raw.Length,
                                    size_min = buf.Length
                                };
                                hubContext.Clients.All.SendAsync("IMAGE_MESSAGE", o).GetAwaiter();
                            }
                        }
                        i++;
                        if (i > maxPage) break;
                    }
                }
            }
            catch (Exception ex)
            {
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

        static void __replaceColor(Bitmap bmp, System.Drawing.Color oldColor, System.Drawing.Color newColor)
        {
            try
            {
                var lockedBitmap = new LockBitmap(bmp);
                lockedBitmap.LockBits();

                for (int y = 0; y < lockedBitmap.Height; y++)
                {
                    for (int x = 0; x < lockedBitmap.Width; x++)
                    {
                        if (lockedBitmap.GetPixel(x, y) == oldColor)
                        {
                            lockedBitmap.SetPixel(x, y, newColor);
                        }
                    }
                }
                lockedBitmap.UnlockBits();
            }
            catch (Exception ex) { 
            }
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

    public class LockBitmap
    {
        Bitmap source = null;
        IntPtr Iptr = IntPtr.Zero;
        BitmapData bitmapData = null;

        public byte[] Pixels { get; set; }
        public int Depth { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public LockBitmap(Bitmap source)
        {
            this.source = source;
        }

        /// <summary>
        /// Lock bitmap data
        /// </summary>
        public void LockBits()
        {
            try
            {
                // Get width and height of bitmap
                Width = source.Width;
                Height = source.Height;

                // get total locked pixels count
                int PixelCount = Width * Height;

                // Create rectangle to lock
                var rect = new System.Drawing.Rectangle(0, 0, Width, Height);

                // get source bitmap pixel format size
                Depth = System.Drawing.Bitmap.GetPixelFormatSize(source.PixelFormat);

                // Check if bpp (Bits Per Pixel) is 8, 24, or 32
                if (Depth != 8 && Depth != 24 && Depth != 32)
                {
                    throw new ArgumentException("Only 8, 24 and 32 bpp images are supported.");
                }

                // Lock bitmap and return bitmap data
                bitmapData = source.LockBits(rect, ImageLockMode.ReadWrite,
                                             source.PixelFormat);

                // create byte array to copy pixel values
                int step = Depth / 8;
                Pixels = new byte[PixelCount * step];
                Iptr = bitmapData.Scan0;

                // Copy data from pointer to array
                System.Runtime.InteropServices.Marshal.Copy(Iptr, Pixels, 0, Pixels.Length);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Unlock bitmap data
        /// </summary>
        public void UnlockBits()
        {
            try
            {
                // Copy data from byte array to pointer
                System.Runtime.InteropServices.Marshal.Copy(Pixels, 0, Iptr, Pixels.Length);

                // Unlock bitmap data
                source.UnlockBits(bitmapData);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Get the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public System.Drawing.Color GetPixel(int x, int y)
        {
            var clr = System.Drawing.Color.Empty;

            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (i > Pixels.Length - cCount)
                throw new IndexOutOfRangeException();

            if (Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                byte a = Pixels[i + 3]; // a
                clr = System.Drawing.Color.FromArgb(a, r, g, b);
            }
            if (Depth == 24) // For 24 bpp get Red, Green and Blue
            {
                byte b = Pixels[i];
                byte g = Pixels[i + 1];
                byte r = Pixels[i + 2];
                clr = System.Drawing.Color.FromArgb(r, g, b);
            }
            if (Depth == 8)
            // For 8 bpp get color value (Red, Green and Blue values are the same)
            {
                byte c = Pixels[i];
                clr = System.Drawing.Color.FromArgb(c, c, c);
            }
            return clr;
        }

        /// <summary>
        /// Set the color of the specified pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="color"></param>
        public void SetPixel(int x, int y, System.Drawing.Color color)
        {
            // Get color components count
            int cCount = Depth / 8;

            // Get start index of the specified pixel
            int i = ((y * Width) + x) * cCount;

            if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
                Pixels[i + 3] = color.A;
            }
            if (Depth == 24) // For 24 bpp set Red, Green and Blue
            {
                Pixels[i] = color.B;
                Pixels[i + 1] = color.G;
                Pixels[i + 2] = color.R;
            }
            if (Depth == 8)
            // For 8 bpp set color value (Red, Green and Blue values are the same)
            {
                Pixels[i] = color.B;
            }
        }
    }
}
