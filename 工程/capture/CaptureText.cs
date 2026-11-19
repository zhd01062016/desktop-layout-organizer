using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

static class CaptureText
{
    internal static async Task<string> Read(Bitmap image)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine == null)
            throw new InvalidOperationException("请在 Windows 语言设置中安装对应的文字识别语言包。");
        double ratio = Math.Min(1, (double)OcrEngine.MaxImageDimension / Math.Max(image.Width, image.Height));
        using (var sized = new Bitmap(image, Math.Max(1, (int)(image.Width * ratio)), Math.Max(1, (int)(image.Height * ratio))))
        using (var bytes = new MemoryStream())
        {
            sized.Save(bytes, ImageFormat.Png);
            bytes.Position = 0;
            using (var input = bytes.AsRandomAccessStream())
            {
                var decoder = await BitmapDecoder.CreateAsync(input);
                using (var pixels = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore))
                {
                    var result = await engine.RecognizeAsync(pixels);
                    return String.Join(Environment.NewLine, result.Lines.Select(line => line.Text));
                }
            }
        }
    }
}
