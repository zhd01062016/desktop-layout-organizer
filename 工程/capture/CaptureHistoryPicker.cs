using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class CaptureHistoryPicker : Form
{
    sealed class Card : Control
    {
        internal readonly string Path;
        internal Bitmap Thumbnail;
        internal Size ImageSize;
        internal bool Selected;
        bool hover;
        internal Card(string path)
        {
            Path = path;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = CaptureStyle.Surface;
            try
            {
                using (var source = Image.FromFile(path))
                {
                    ImageSize = source.Size;
                    Thumbnail = new Bitmap(420, 250);
                    using (var g = Graphics.FromImage(Thumbnail))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        float scale = Math.Min(420f / source.Width, 250f / source.Height);
                        g.DrawImage(source, (420 - source.Width * scale) / 2, (250 - source.Height * scale) / 2, source.Width * scale, source.Height * scale);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is OutOfMemoryException || ex is ExternalException))
                    throw;
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(SystemColors.Window))
            using (var path = CaptureStyle.Round(new Rectangle(1, 1, Width - 3, Height - 3), 10))
            {
                g.FillPath(bg, path);
                using (var pen = new Pen(hover || Selected ? CaptureStyle.Blue : CaptureStyle.Border, hover || Selected ? 2 : 1))
                    g.DrawPath(pen, path);
            }

            var frame = new Rectangle(10, 10, Width - 20, Height - 58);
            var saved = g.Save();
            g.SetClip(frame);
            using (var a = new SolidBrush(Color.FromArgb(246, 247, 249)))
            using (var b = new SolidBrush(Color.FromArgb(233, 236, 240)))
                for (int y = frame.Top; y < frame.Bottom; y += 12)
                    for (int x = frame.Left; x < frame.Right; x += 12)
                        g.FillRectangle(((x - frame.Left) / 12 + (y - frame.Top) / 12) % 2 == 0 ? a : b, x, y, 12, 12);
            if (Thumbnail != null)
            {
                float scale = Math.Min((float)frame.Width / Thumbnail.Width, (float)frame.Height / Thumbnail.Height);
                g.DrawImage(Thumbnail, frame.Left + (frame.Width - Thumbnail.Width * scale) / 2, frame.Top + (frame.Height - Thumbnail.Height * scale) / 2, Thumbnail.Width * scale, Thumbnail.Height * scale);
            }

            g.Restore(saved);
            string label = Thumbnail == null ? "图片不可用" : (hover ? "点击复制原图" : File.GetLastWriteTime(Path).ToString("MM-dd  HH:mm"));
            TextRenderer.DrawText(g, label, Font, new Rectangle(12, Height - 43, Width - 24, 22), CaptureStyle.Ink, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (Thumbnail != null)
                TextRenderer.DrawText(g, ImageSize.Width + " × " + ImageSize.Height, Font, new Rectangle(12, Height - 23, Width - 24, 18), Color.FromArgb(126, 135, 148), TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Thumbnail != null)
                Thumbnail.Dispose();
            base.Dispose(disposing);
        }
    }

    readonly FlowLayoutPanel grid = new FlowLayoutPanel();
    readonly List<Card> cards = new List<Card>();
    int selected;
    internal CaptureHistoryPicker(string[] entries, int direction)
    {
        Text = "截图历史";
        Font = new Font("Microsoft YaHei UI", 9);
        BackColor = CaptureStyle.Surface;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(800, Math.Max(250, screen.Width - 48)), Math.Min(660, Math.Max(220, screen.Height - 90)));
        var title = new Label
        {
            Text = "截图历史（24小时内）",
            Font = new Font("Microsoft YaHei UI", 9),
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(20, 12, 0, 0)
        };
        var help = new Label
        {
            Text = "点击图片复制原图  ·  Esc 返回截图",
            ForeColor = Color.FromArgb(120, 130, 145),
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(22, 4, 0, 0)
        };
        grid.Dock = DockStyle.Fill;
        grid.AutoScroll = true;
        grid.Padding = new Padding(12);
        Controls.Add(grid);
        Controls.Add(help);
        Controls.Add(title);
        foreach (var path in entries)
        {
            if (cards.Count == 10)
                break;
            var card = new Card(path)
            {
                Font = Font,
                Margin = new Padding(6)
            };
            int index = cards.Count;
            card.Click += delegate
            {
                Copy(index);
            };
            cards.Add(card);
            grid.Controls.Add(card);
        }

        if (cards.Count == 0)
            grid.Controls.Add(new Label { Text = "暂无截图记录\n完成截图后，24小时内最近十张图片会显示在这里。", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(20) });
        selected = direction < 0 ? Math.Max(0, cards.Count - 1) : 0;
        Resize += delegate
        {
            LayoutCards();
        };
        Shown += delegate
        {
            LayoutCards();
            Highlight();
        };
    }

    void LayoutCards()
    {
        int available = Math.Max(180, grid.ClientSize.Width - 40);
        int columns = Math.Max(1, Math.Min(3, available / 220));
        int width = available / columns - 12;
        foreach (var card in cards)
            card.Size = new Size(width, (int)(width * .60) + 58);
    }

    void Highlight()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].Selected = i == selected;
            cards[i].Invalidate();
        }

        if (cards.Count > 0)
            grid.ScrollControlIntoView(cards[selected]);
    }

    void Copy(int index)
    {
        if (index < 0 || index >= cards.Count || cards[index].Thumbnail == null)
            return;
        try
        {
            using (var source = Image.FromFile(cards[index].Path))
            using (var image = new Bitmap(source))
                Clipboard.SetDataObject(image, true, 5, 80);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is ExternalException || ex is OutOfMemoryException))
                throw;
            MessageBox.Show(this, "无法复制这张图片：" + ex.Message, "截图历史");
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys key)
    {
        if (key == Keys.Escape)
        {
            Close();
            return true;
        }

        if (cards.Count > 0)
        {
            if (key == Keys.Oemcomma || key == Keys.Right)
            {
                selected = (selected + 1) % cards.Count;
                Highlight();
                return true;
            }

            if (key == Keys.OemPeriod || key == Keys.Left)
            {
                selected = (selected + cards.Count - 1) % cards.Count;
                Highlight();
                return true;
            }

            if (key == Keys.Enter)
            {
                Copy(selected);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, key);
    }
}
