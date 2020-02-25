﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PicsDirectoryDisplayWin.lib_ImgIO
{
    public class ImageResizing : IImageResizing, IDisposable
    {
        #region Fields
        private JpegBitmapEncoder _jpegEncoder;
        private Stream _sourceImageStream;
        private BitmapFrame _firstImageBitmapFrame;
        #endregion

        #region Constructors
        public ImageResizing(string sourceImagePath)
            : this(new MemoryStream(File.ReadAllBytes(sourceImagePath)))
        { }

        public ImageResizing(byte[] sourceImageBytes)
            : this(new MemoryStream(sourceImageBytes))
        { }

        public ImageResizing(Stream sourceImageStream)
        {
            _sourceImageStream = sourceImageStream;
            _firstImageBitmapFrame = GetFirstBitmapFrame(_sourceImageStream);
            
            _jpegEncoder = new JpegBitmapEncoder();
            _jpegEncoder.Frames.Add(_firstImageBitmapFrame);
        }
        #endregion

        #region IImageResizer Members
        /// <summary>
        /// Compresses the image source to the supplied quality threshold.
        /// </summary>
        /// <param name="quality"></param>
        /// <returns></returns>
        public ImageResizing Quality(int quality)
        {
            // Seems that 75 or less is the magic threshold 
            // for image compression using the JpegEncoder. 
            // Above this value, file size tends to increase 
            // over the original image file size.
            if (quality <= 75)
            {
                _jpegEncoder.QualityLevel = quality;
            }

            // Make this method chainable
            return this;
        }


        public void ResizeIfNeeded(string ImagePath)
        {
            int AssumedSize_Inch=0; // = Convert.ToInt32(ConfigurationManager.AppSettings["A5ImageQuality"]);

            if (Globals.PrintSelection == Globals.PrintSize.A5)
            {
                AssumedSize_Inch = Convert.ToInt32(ConfigurationManager.AppSettings["A5ImageQuality"]);
            }
            else if (Globals.PrintSelection == Globals.PrintSize.A4)
            {
                AssumedSize_Inch = Convert.ToInt32(ConfigurationManager.AppSettings["A4ImageQuality"]);
            }
            else if (Globals.PrintSelection == Globals.PrintSize.Passport)
            {
                AssumedSize_Inch = Convert.ToInt32(ConfigurationManager.AppSettings["PassportImageQuality"]);
            }
            else if (Globals.PrintSelection == Globals.PrintSize.Postcard)
            {
                AssumedSize_Inch = Convert.ToInt32(ConfigurationManager.AppSettings["A5ImageQuality"]);
            }
            else if (Globals.PrintSelection == Globals.PrintSize.pdf)
            {
                //No Resize needed, return
                return;
            }
            double newImageShorterSide;
            //Assume printer will print at 300 dpi.
            double DPI300Width = _firstImageBitmapFrame.PixelWidth / 300;
            double DPI300Hght = _firstImageBitmapFrame.PixelHeight / 300;
            
            //Resize only images whose shorter side is greater than 8 inches
            if (DPI300Hght < DPI300Width && DPI300Hght > AssumedSize_Inch)
            {
                newImageShorterSide = AssumedSize_Inch * 300;
                Resize(0, Convert.ToInt32(newImageShorterSide)).Quality(100).Save(ImagePath,true);
            }
            else if (DPI300Width < DPI300Hght && DPI300Width > AssumedSize_Inch)
            {
                newImageShorterSide = AssumedSize_Inch * 300;
                Resize(Convert.ToInt32(newImageShorterSide),0).Quality(100).Save(ImagePath, true);
            }

        }


        /// <summary>
        /// Resizes the image source using the supplied width and height.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public ImageResizing Resize(int width, int height)
        {
            var resizedBitmapFrame = Resize(_firstImageBitmapFrame, width, height);

            _jpegEncoder.Frames.Clear();
            _jpegEncoder.Frames.Add(resizedBitmapFrame);

            // Make this method chainable
            return this;
        }

        /// <summary>
        /// Saves the modified image source to the supplied destination path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dispose"></param>
        public void Save(string path, bool dispose = true)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                _jpegEncoder.Save(fs);
            }

            if (dispose)
            {
                Dispose();
            }
        }

        /// <summary>
        /// Encodes the modified image to an image stream,
        /// using the current jpeg encoder.
        /// </summary>
        /// <returns></returns>
        public MemoryStream ToStream()
        {
            var memStream = new MemoryStream();
            _jpegEncoder.Save(memStream);
            return memStream;
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _sourceImageStream.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

        #region Private Helpers
        /// <summary>
        /// The image resizing method.
        /// </summary>
        /// <param name="bitmapFrame"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private static BitmapFrame Resize(BitmapFrame bitmapFrame, int width, int height)
        {
            double scaleWidth, scaleHeight;

            // Resize proportionally to the width
            if (height == 0)
            {
                scaleWidth = width;
                scaleHeight = (((double)width / bitmapFrame.PixelWidth) * bitmapFrame.PixelHeight);
            }
            // Resize proportionally to the height
            else if (width == 0)
            {
                scaleHeight = height;
                scaleWidth = (((double)height / bitmapFrame.PixelHeight) * bitmapFrame.PixelWidth);
            }
            // Resize using the supplied width and height
            else
            {
                scaleWidth = width;
                scaleHeight = height;
            }

            // Create the scale transform
            var scaleTransform = new ScaleTransform(scaleWidth / bitmapFrame.PixelWidth, scaleHeight / bitmapFrame.PixelHeight, 0, 0);

            // Transform the bitmap frame
            var transformedBitmap = new TransformedBitmap(bitmapFrame, scaleTransform);

            return BitmapFrame.Create(transformedBitmap);
        }

        /// <summary>
        /// Reads out the first <see cref="BitmapFrame"/> from the supplied image stream.
        /// </summary>
        /// <param name="imageStream"></param>
        /// <returns></returns>
        private static BitmapFrame GetFirstBitmapFrame(Stream imageStream)
        {
            var bitmapDecoder = BitmapDecoder.Create(imageStream,
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            return bitmapDecoder.Frames[0];
        }
        #endregion
    }
}
