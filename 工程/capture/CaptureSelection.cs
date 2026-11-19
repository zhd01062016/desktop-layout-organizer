using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed partial class CaptureSelection : Form
{
    enum Tool
    {
        Select,
        Rectangle,
        Ellipse,
        Line,
        Arrow,
        Pen,
        Highlight,
        Mosaic,
        Text
    }

    enum Drag
    {
        None,
        Create,
        Move,
        Resize,
        Draw,
        MarkMove,
        MarkResize
    }

    [Flags]
    enum Edges
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }

    sealed class Mark
    {
        internal Tool Kind;
        internal Point Start, End;
        internal readonly List<Point> Points = new List<Point>();
        internal Color Color;
        internal int Width;
        internal string Text;
        internal int FontSize = 18, TextWidth = 300;
        internal bool Bold;
    }

    readonly Bitmap desktop;
    readonly ToolStrip bar = new ToolStrip();
    readonly Dictionary<Tool, ToolStripButton> toolButtons = new Dictionary<Tool, ToolStripButton>();
    readonly List<Mark> marks = new List<Mark>();
    readonly List<IntPtr> windows = new List<IntPtr>();
    Tool tool;
    Drag drag;
    Edges edges;
    Rectangle region, initial, hover;
    Point anchor, pointer;
    Color ink = Color.FromArgb(235, 64, 52);
    int stroke = 3;
    Mark active;
    TextBox input;
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    delegate bool EnumCallback(IntPtr handle, IntPtr data);
    [DllImport("user32")]
    static extern bool EnumWindows(EnumCallback callback, IntPtr data);
    [DllImport("user32")]
    static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32")]
    static extern bool IsIconic(IntPtr window);
    [DllImport("user32")]
    static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32")]
    static extern bool ScreenToClient(IntPtr window, ref Point point);
    [DllImport("user32")]
    static extern IntPtr ChildWindowFromPointEx(IntPtr parent, Point point, uint flags);
    public CaptureSelection(Bitmap bitmap, Rectangle screen)
    {
        desktop = bitmap;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = screen;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        Text = "桌面截图";
        BuildToolbar();
        EnumWindows(delegate (IntPtr w, IntPtr unused)
        {
            if (IsWindowVisible(w) && !IsIconic(w))
                windows.Add(w);
            return true;
        }, IntPtr.Zero);
        Shown += delegate
        {
            Activate();
            pointer = PointToClient(Cursor.Position);
            hover = Detect(pointer);
            Invalidate();
        };
    }

    Rectangle Detect(Point point)
    {
        Point absolute = PointToScreen(point);
        foreach (IntPtr w in windows)
        {
            NativeRect r;
            if (!GetWindowRect(w, out r))
                continue;
            Rectangle found = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            if (!found.Contains(absolute))
                continue;
            IntPtr parent = w;
            for (int depth = 0; depth < 16; depth++)
            {
                Point local = absolute;
                if (!ScreenToClient(parent, ref local))
                    break;
                IntPtr child = ChildWindowFromPointEx(parent, local, 7);
                if (child == parent || child == IntPtr.Zero || !GetWindowRect(child, out r))
                    break;
                Rectangle candidate = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                if (!candidate.Contains(absolute))
                    break;
                found = Rectangle.Intersect(found, candidate);
                parent = child;
            }

            return Rectangle.Intersect(ClientRectangle, RectangleToClient(found));
        }

        return Rectangle.Intersect(ClientRectangle, RectangleToClient(Screen.FromPoint(absolute).Bounds));
    }

    static Rectangle Between(Point a, Point b)
    {
        return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    static int Clamp(int value, int min, int max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    bool Selected
    {
        get
        {
            return region.Width > 0 && region.Height > 0;
        }
    }

    Edges HitEdges(Point point)
    {
        Rectangle perimeter = Rectangle.Inflate(region, 5, 5);
        if (!perimeter.Contains(point))
            return Edges.None;
        Edges hit = Edges.None;
        if (Math.Abs(point.X - region.Left) <= 5)
            hit |= Edges.Left;
        else if (Math.Abs(point.X - region.Right) <= 5)
            hit |= Edges.Right;
        if (Math.Abs(point.Y - region.Top) <= 5)
            hit |= Edges.Top;
        else if (Math.Abs(point.Y - region.Bottom) <= 5)
            hit |= Edges.Bottom;
        return hit;
    }

    void PositionToolbar()
    {
        if (!Selected || drag != Drag.None)
        {
            bar.Hide();
            return;
        }

        Point screen = PointToScreen(new Point(region.Right - 1, region.Bottom - 1));
        Rectangle monitor = RectangleToClient(Screen.FromPoint(screen).Bounds);
        UpdateToolbarScale(screen);
        bar.MaximumSize = new Size(Math.Max(200, monitor.Width - 16), 0);
        bar.Show();
        int y = region.Bottom + 5;
        if (y + bar.Height > monitor.Bottom)
            y = region.Top - bar.Height - 5;
        if (y < monitor.Top)
            y = region.Bottom - bar.Height - 4;
        bar.Location = new Point(Clamp(region.Right - bar.Width, monitor.Left, monitor.Right - bar.Width), Clamp(y, monitor.Top, monitor.Bottom - bar.Height));
    }

    void DrawMark(Graphics g, Mark mark)
    {
        Rectangle box = Between(mark.Start, mark.End);
        using (var pen = new Pen(mark.Color, mark.Width))
        {
            switch (mark.Kind)
            {
                case Tool.Rectangle:
                    g.DrawRectangle(pen, box);
                    break;
                case Tool.Ellipse:
                    if (box.Width > 0 && box.Height > 0)
                        g.DrawEllipse(pen, box);
                    break;
                case Tool.Arrow:
                case Tool.Line:
                    if (mark.Kind == Tool.Arrow)
                        pen.EndCap = LineCap.ArrowAnchor;
                    g.DrawLine(pen, mark.Start, mark.End);
                    break;
                case Tool.Text:
                    if (String.IsNullOrEmpty(mark.Text))
                        break;
                    using (var font = MarkFont(mark))
                        TextRenderer.DrawText(g, mark.Text, font, new Rectangle(mark.Start, new Size(mark.TextWidth, Math.Max(1, region.Bottom - mark.Start.Y))), mark.Color, TextFlags);
                    break;
                case Tool.Mosaic:
                    int tile = 6 + mark.Width * 2;
                    for (int y = box.Top; y < box.Bottom; y += tile)
                        for (int x = box.Left; x < box.Right; x += tile)
                            using (var brush = new SolidBrush(desktop.GetPixel(x, y)))
                                g.FillRectangle(brush, x, y, Math.Min(tile, box.Right - x), Math.Min(tile, box.Bottom - y));
                    break;
                case Tool.Highlight:
                case Tool.Pen:
                    if (mark.Kind == Tool.Highlight)
                    {
                        pen.Color = Color.FromArgb(100, mark.Color);
                        pen.Width *= 4;
                    }

                    pen.StartCap = pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    if (mark.Points.Count > 1)
                        g.DrawLines(pen, mark.Points.ToArray());
                    else
                        g.DrawLine(pen, mark.Start, new Point(mark.Start.X + 1, mark.Start.Y));
                    break;
            }
        }
    }

    void DrawMarks(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var mark in marks)
            if (input == null || mark != active)
                DrawMark(g, mark);
        if (active != null && input == null)
            DrawMark(g, active);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.DrawImageUnscaled(desktop, 0, 0);
        using (var shade = new SolidBrush(Color.FromArgb(85, Color.Black)))
            g.FillRectangle(shade, ClientRectangle);
        Rectangle focus = Selected ? region : hover;
        if (focus.Width <= 0 || focus.Height <= 0)
            return;
        var state = g.Save();
        g.SetClip(focus);
        g.DrawImageUnscaled(desktop, 0, 0);
        DrawMarks(g);
        g.Restore(state);
        DrawEditing(g);
        g.DrawRectangle(Pens.DeepSkyBlue, focus.X, focus.Y, focus.Width - 1, focus.Height - 1);
        if (Selected && tool == Tool.Select && drag == Drag.None)
            foreach (int x in new[]
            {
                focus.Left,
                focus.Left + focus.Width / 2,
                focus.Right - 1
            }

            )
                foreach (int y in new[]
                {
                    focus.Top,
                    focus.Top + focus.Height / 2,
                    focus.Bottom - 1
                }

                )
                    if (x != focus.Left + focus.Width / 2 || y != focus.Top + focus.Height / 2)
                        g.FillRectangle(Brushes.DeepSkyBlue, x - 2, y - 2, 5, 5);
        if (!Selected || drag == Drag.Create || drag == Drag.Resize)
            DrawLens(g, focus);
    }

    void DrawLens(Graphics g, Rectangle focus)
    {
        if (!ClientRectangle.Contains(pointer))
            return;
        Rectangle monitor = RectangleToClient(Screen.FromPoint(PointToScreen(pointer)).Bounds);
        const int side = 100, info = 40;
        int left = pointer.X + 22, top = pointer.Y + 22;
        if (left + side > monitor.Right)
            left = pointer.X - side - 22;
        if (top + side + info > monitor.Bottom)
            top = pointer.Y - side - info - 22;
        Rectangle source = Rectangle.Intersect(ClientRectangle, new Rectangle(pointer.X - 10, pointer.Y - 10, 21, 21));
        g.FillRectangle(Brushes.Black, left, top, side, side + info);
        var old = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(desktop, new Rectangle(left, top, side, side), source, GraphicsUnit.Pixel);
        g.InterpolationMode = old;
        g.DrawLine(Pens.OrangeRed, left + 50, top + 43, left + 50, top + 57);
        g.DrawLine(Pens.OrangeRed, left + 43, top + 50, left + 57, top + 50);
        Color pixel = desktop.GetPixel(pointer.X, pointer.Y);
        string label = focus.Width + " × " + focus.Height + "\n" + ColorTranslator.ToHtml(Color.FromArgb(pixel.R, pixel.G, pixel.B));
        TextRenderer.DrawText(g, label, Font, new Rectangle(left + 3, top + side, side - 3, info), Color.White, TextFormatFlags.NoPadding);
    }

    Bitmap RenderSelection()
    {
        CommitText();
        Bitmap result = desktop.Clone(region, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.TranslateTransform(-region.X, -region.Y);
            DrawMarks(g);
        }

        return result;
    }

    public Bitmap PinnedImage { get; private set; }
    public Point PinLocation { get; private set; }

    void Finish(bool pin)
    {
        if (!Selected)
            return;
        try
        {
            if (pin)
            {
                PinnedImage = RenderSelection();
                PinLocation = PointToScreen(region.Location);
                CaptureHistory.Remember(PinnedImage);
            }
            else
            {
                using (var result = RenderSelection())
                {
                    Clipboard.SetDataObject(result, true, 5, 80);
                    CaptureHistory.Remember(result);
                }
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "截图");
        }
    }

    void ShowHistory(int direction)
    {
        using (var history = new CaptureHistoryPicker(CaptureHistory.Entries(), direction))
        {
            if (history.ShowDialog(this) == DialogResult.OK)
                Close();
        }
    }

    async void Recognize()
    {
        if (!Selected || !bar.Enabled)
            return;
        bar.Enabled = false;
        try
        {
            string text;
            using (var image = RenderSelection())
                text = await CaptureText.Read(image);
            if (IsDisposed)
                return;
            if (String.IsNullOrWhiteSpace(text))
                MessageBox.Show(this, "选区内没有识别到文字。", "文字识别");
            else
            {
                Clipboard.SetText(text);
                Close();
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                MessageBox.Show(this, ex.Message, "文字识别");
        }
        finally
        {
            if (!IsDisposed)
                bar.Enabled = true;
        }
    }

    void Save()
    {
        if (!Selected)
            return;
        using (var dialog = ImageExport.CreateDialog("截图_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), "BMP 图片"))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                using (var result = RenderSelection())
                {
                    ImageExport.Save(result, dialog);
                    CaptureHistory.Remember(result);
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存截图");
            }
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys data)
    {
        if (input != null && !((CaptureTextInput)input).Composing && data == (Keys.Control | Keys.Enter))
        {
            CommitText();
            Focus();
            return true;
        }

        if (input != null && !((CaptureTextInput)input).Composing && data == Keys.Escape)
        {
            CancelText();
            Focus();
            return true;
        }

        if (input != null)
            return base.ProcessCmdKey(ref msg, data);
        switch (data)
        {
            case Keys.Oemcomma:
                ShowHistory(1);
                return true;
            case Keys.OemPeriod:
                ShowHistory(-1);
                return true;
            case Keys.C:
                if (ClientRectangle.Contains(pointer))
                {
                    Color sample = desktop.GetPixel(pointer.X, pointer.Y);
                    try
                    {
                        Clipboard.SetText(String.Format("#{0:X2}{1:X2}{2:X2}", sample.R, sample.G, sample.B));
                        Close();
                    }
                    catch (ExternalException ex)
                    {
                        MessageBox.Show(this, ex.Message, "取色");
                    }
                }

                return true;
            case Keys.Escape:
                Close();
                return true;
            case Keys.Enter:
            case Keys.Control | Keys.C:
                Finish(false);
                return true;
            case Keys.Control | Keys.T:
                Finish(true);
                return true;
            case Keys.Control | Keys.S:
                Save();
                return true;
            case Keys.Control | Keys.Z:
                Undo();
                return true;
            case Keys.Control | Keys.Y:
                Redo();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, data);
        }
    }
}
