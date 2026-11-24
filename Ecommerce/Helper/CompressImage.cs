
using System.Drawing;
using System.Drawing.Imaging;

namespace EcommerceWebApplication.Helper
{
    public static class ImageHelper
    {
        public static byte[] CompressImage(Image image, int maxWidth, int maxHeight, long quality)
        {
            int newWidth = image.Width;
            int newHeight = image.Height;

            if (image.Width > maxWidth || image.Height > maxHeight)
            {
                double widthRatio = (double)maxWidth / image.Width;
                double heightRatio = (double)maxHeight / image.Height;
                double ratio = Math.Min(widthRatio, heightRatio);

                newWidth = (int)(image.Width * ratio);
                newHeight = (int)(image.Height * ratio);
            }

            using (var resizedBitmap = new Bitmap(image, newWidth, newHeight))
            using (var ms = new MemoryStream())
            {
                var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                resizedBitmap.Save(ms, jpegEncoder, encoderParams);
                return ms.ToArray();
            }
        }
        private static Bitmap ResizeImage(Image image, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var width = (int)(image.Width * ratio);
            var height = (int)(image.Height * ratio);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent); // ⭐ IMPORTANT
            graphics.DrawImage(image, 0, 0, width, height);

            return bitmap;
        }

        public static byte[] CompressPng(Image image, int maxWidth, int maxHeight)
        {
            using var resized = ResizeImage(image, maxWidth, maxHeight);
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png); // ✅ transparency safe
            return ms.ToArray();
        }
    }
}
