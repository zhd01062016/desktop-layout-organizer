using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

static class ImageExport
{
    internal static SaveFileDialog CreateDialog(string fileName, string bitmapLabel)
    {
        return new SaveFileDialog
        {
            Filter = "PNG 图片|*.png|JPEG 图片|*.jpg|" + bitmapLabel + "|*.bmp",
            FileName = fileName,
            AddExtension = true,
            DefaultExt = "png"
        };
    }

    internal static void Save(Image image, SaveFileDialog dialog)
    {
        ImageFormat format;
        switch (dialog.FilterIndex)
        {
            case 1:
                format = ImageFormat.Png;
                break;
            case 2:
                format = ImageFormat.Jpeg;
                break;
            case 3:
                format = ImageFormat.Bmp;
                break;
            default:
                throw new InvalidOperationException("未知的图片格式。");
        }

        image.Save(dialog.FileName, format);
    }
}
