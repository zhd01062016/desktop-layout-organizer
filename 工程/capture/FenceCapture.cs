using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class FenceCapture
{
    [DllImport("user32")]
    static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "--cleanup-history" || args[0] == "--install-history-cleanup"))
        {
            try
            {
                if (args[0] == "--install-history-cleanup")
                    CaptureHistory.InstallCleanup();
                else
                    CaptureHistory.Cleanup();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.ExitCode = 1;
            }

            return;
        }

        CaptureHistory.Cleanup();
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        Application.EnableVisualStyles();
        try
        {
            if (args.Length == 1 && args[0] == "settings")
            {
                using (var settings = new CaptureKeys())
                    Application.Run(settings);
                return;
            }

            if (args.Length == 1 && args[0] == "pin")
            {
                using (var image = ReadClipboard())
                {
                    if (image == null)
                    {
                        MessageBox.Show("请先复制图片、图片文件或文字。", "桌面贴图");
                        return;
                    }

                    using (var window = new PasteWindow(image))
                        Application.Run(window);
                }

                return;
            }

            bool first;
            Bitmap pin = null;
            Point location = Point.Empty;
            using (var guard = new Mutex(true, @"Local\PecoFenceCaptureSelection", out first))
            {
                if (!first)
                    return;
                var bounds = SystemInformation.VirtualScreen;
                using (var frame = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (var graphics = Graphics.FromImage(frame))
                        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    using (var editor = new CaptureSelection(frame, bounds))
                    {
                        Application.Run(editor);
                        pin = editor.PinnedImage;
                        location = editor.PinLocation;
                    }
                }
            }

            if (pin != null)
                using (pin)
                using (var window = new PasteWindow(pin)
                {
                    Location = location
                }

                )
                    Application.Run(window);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "桌面截图与贴图");
        }
    }

    internal static Image ReadClipboard()
    {
        Image image = Clipboard.GetImage();
        if (image != null)
            return image;
        if (Clipboard.ContainsFileDropList())
            foreach (string path in Clipboard.GetFileDropList())
            {
                switch (Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".png":
                    case ".bmp":
                    case ".gif":
                    case ".jpeg":
                    case ".jpg":
                    case ".tif":
                    case ".tiff":
                        using (var source = Image.FromFile(path))
                            return new Bitmap(source);
                }
            }

        string text = Clipboard.GetText();
        if (String.IsNullOrWhiteSpace(text))
            return null;
        using (var font = new Font("Microsoft YaHei UI", 13))
        {
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            Size size = TextRenderer.MeasureText(text, font, new Size(720, 4000), flags);
            var card = new Bitmap(Math.Min(720, size.Width) + 24, Math.Min(4000, size.Height) + 24);
            using (var graphics = Graphics.FromImage(card))
            {
                graphics.Clear(Color.White);
                TextRenderer.DrawText(graphics, text, font, new Rectangle(12, 12, card.Width - 24, card.Height - 24), Color.Black, flags);
            }

            return card;
        }
    }
}
