using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class PasteWindow : Form
{
    Bitmap picture;
    double zoom;
    Rectangle fullBounds;
    bool thumbnail, magnifier, rgb, dragging, hovering, passThrough;
    Point dragCursor, dragOrigin, pointer;
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly NotifyIcon recovery = new NotifyIcon();
    readonly ToolTip tips = new ToolTip();

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    public PasteWindow(Image image)
    {
        picture = new Bitmap(image);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        TopMost = true;
        Text = "桌面贴图";
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        zoom = Math.Min(1.0, Math.Min((double)area.Width / image.Width, (double)area.Height / image.Height));
        ClientSize = ScaledSize(zoom);
        Location = new Point(Math.Max(area.Left, Math.Min(Cursor.Position.X - Width / 2, area.Right - Width)),
                             Math.Max(area.Top, Math.Min(Cursor.Position.Y - Height / 2, area.Bottom - Height)));
        menu.ShowImageMargin = false;
        menu.Opening += delegate { BuildMenu(); };
        ContextMenuStrip = menu;
        recovery.Icon = SystemIcons.Application;
        recovery.Text = "桌面贴图 · 单击恢复鼠标操作";
        recovery.MouseClick += delegate { SetPassThrough(false); Activate(); };
        tips.SetToolTip(this, "拖动移动 · 滚轮缩放 · Ctrl + 滚轮调透明度 · 右键菜单");
        tips.InitialDelay = 900;
        tips.AutoPopDelay = 2500;
    }

    void Add(string label, Action command)
    {
        menu.Items.Add(label, null, delegate { command(); });
    }

    void BuildMenu()
    {
        while (menu.Items.Count > 0) menu.Items[0].Dispose();
        Add("复制图片    Ctrl+C", CopyImage);
        Add("保存图片…    Ctrl+S", SaveImage);
        Add("替换为剪贴板图片    Ctrl+V", ReplaceImage);
        menu.Items.Add(new ToolStripSeparator());
        Add("原始尺寸    0", delegate { ZoomAt(1, CenterPoint()); });
        Add(thumbnail ? "展开缩略图" : "缩略图    Shift+双击", ToggleThumbnail);
        Add("顺时针转 90°    1", delegate { Transform(RotateFlipType.Rotate90FlipNone); });
        Add("逆时针转 90°    2", delegate { Transform(RotateFlipType.Rotate270FlipNone); });
        Add("水平翻转    3", delegate { Transform(RotateFlipType.RotateNoneFlipX); });
        Add("垂直翻转    4", delegate { Transform(RotateFlipType.RotateNoneFlipY); });
        menu.Items.Add(new ToolStripSeparator());
        Add(TopMost ? "取消置顶" : "置顶", delegate { TopMost = !TopMost; });
        Add("鼠标穿透（单击托盘图标恢复）", delegate { SetPassThrough(true); });
        Add("关闭    Esc", Close);
    }

    void SetPassThrough(bool enabled)
    {
        int flags = GetWindowLong(Handle, -20);
        if (enabled) {
            // A layered transparent window passes hit-testing across process boundaries.
            if (Opacity == 1) Opacity = .99;
            flags = GetWindowLong(Handle, -20);
        }
        SetWindowLong(Handle, -20, enabled ? flags | 0x80020 : flags & ~0x20);
        passThrough = enabled;
        recovery.Visible = enabled;
        if (enabled) recovery.ShowBalloonTip(2000, Text, "单击此托盘图标恢复鼠标操作。", ToolTipIcon.Info);
    }

    Point CenterPoint() { return new Point(Width / 2, Height / 2); }

    Size ScaledSize(double scale)
    {
        return new Size(Math.Max(1, (int)Math.Round(picture.Width * scale)),
                        Math.Max(1, (int)Math.Round(picture.Height * scale)));
    }

    void ZoomAt(double value, Point anchor)
    {
        if (thumbnail) ToggleThumbnail();
        double next = Math.Min(32760.0 / Math.Max(picture.Width, picture.Height),
                               Math.Max(.02, Math.Min(32, value)));
        Point fixedPoint = PointToScreen(anchor);
        Size size = ScaledSize(next);
        Bounds = new Rectangle(fixedPoint.X - (int)Math.Round((double)anchor.X * size.Width / Width),
                               fixedPoint.Y - (int)Math.Round((double)anchor.Y * size.Height / Height),
                               size.Width, size.Height);
        zoom = next;
        Invalidate();
    }

    void ToggleThumbnail()
    {
        if (thumbnail) {
            ClientSize = fullBounds.Size;
        } else {
            fullBounds = Bounds;
            double shrink = Math.Min(1, 96.0 / Math.Max(Width, Height));
            ClientSize = new Size(Math.Max(1, (int)(Width * shrink)), Math.Max(1, (int)(Height * shrink)));
        }
        thumbnail = !thumbnail;
        Invalidate();
    }

    void Transform(RotateFlipType operation)
    {
        if (thumbnail) ToggleThumbnail();
        Point middle = PointToScreen(CenterPoint());
        picture.RotateFlip(operation);
        ClientSize = ScaledSize(zoom);
        Location = new Point(middle.X - Width / 2, middle.Y - Height / 2);
        Invalidate();
    }

    Point ImagePoint()
    {
        return new Point(Math.Max(0, Math.Min(picture.Width - 1, (int)((long)pointer.X * picture.Width / Width))),
                         Math.Max(0, Math.Min(picture.Height - 1, (int)((long)pointer.Y * picture.Height / Height))));
    }

    string PixelText(Point px)
    {
        Color c = picture.GetPixel(px.X, px.Y);
        return rgb ? String.Format("RGB({0}, {1}, {2})", c.R, c.G, c.B)
                   : String.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.InterpolationMode = Width > picture.Width ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(picture, ClientRectangle, 0, 0, picture.Width, picture.Height, GraphicsUnit.Pixel);
        if (hovering && Width > 2 && Height > 2)
            g.DrawRectangle(Pens.DeepSkyBlue, 0, 0, Width - 1, Height - 1);
        if (!magnifier || !hovering || thumbnail) return;
        Point px = ImagePoint();
        const int box = 77;
        int x = Math.Max(0, Math.Min(pointer.X + 15, Width - box - 2));
        int y = pointer.Y + 110 < Height ? pointer.Y + 15 : Math.Max(0, pointer.Y - 108);
        g.FillRectangle(Brushes.Black, x, y, box + 2, box + 22);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(picture, new Rectangle(x+1, y+1, box, box),
                    new Rectangle(px.X-5, px.Y-5, 11, 11), GraphicsUnit.Pixel);
        g.DrawRectangle(Pens.White, x+36, y+36, 7, 7);
        TextRenderer.DrawText(g, PixelText(px), Font, new Point(x, y+box+1), Color.White, TextFormatFlags.NoPadding);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || e.Clicks > 1) return;
        Activate();
        dragging = true;
        dragCursor = Cursor.Position;
        dragOrigin = Location;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        pointer = e.Location;
        if (dragging) {
            Point cursor = Cursor.Position;
            Location = new Point(dragOrigin.X + cursor.X - dragCursor.X, dragOrigin.Y + cursor.Y - dragCursor.Y);
        }
        if (magnifier) Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
        Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) dragging = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left) return;
        if ((ModifierKeys & Keys.Shift) != 0) ToggleThumbnail();
        else Close();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovering = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovering = false; Invalidate(); }
    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); magnifier = false; Invalidate(); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        double steps = (double)e.Delta / SystemInformation.MouseWheelScrollDelta;
        if ((ModifierKeys & Keys.Control) != 0) Opacity = Math.Max(.1, Math.Min(1, Opacity + .05 * steps));
        else ZoomAt(zoom * Math.Pow(1.1, steps), e.Location);
    }

    void CopyImage()
    {
        try { Clipboard.SetDataObject(picture, true, 5, 80); }
        catch (ExternalException ex) { MessageBox.Show(this, ex.Message, Text); }
    }

    void ReplaceImage()
    {
        try {
            using (Image source = FenceCapture.ReadClipboard()) {
                if (source == null) return;
                Bitmap replacement = new Bitmap(source);
                picture.Dispose();
                picture = replacement;
                thumbnail = false;
                Rectangle area = Screen.FromControl(this).WorkingArea;
                zoom = Math.Min(1.0, Math.Min((double)area.Width / picture.Width, (double)area.Height / picture.Height));
                ClientSize = ScaledSize(zoom);
                Invalidate();
            }
        } catch (ExternalException ex) { MessageBox.Show(this, ex.Message, Text); }
    }

    void SaveImage()
    {
        using (var save = ImageExport.CreateDialog("贴图_"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"), "位图")) {
            if(save.ShowDialog(this) != DialogResult.OK) return;
            try { ImageExport.Save(picture, save); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys key)
    {
        switch (key) {
            case Keys.Control | Keys.C: CopyImage(); return true;
            case Keys.Control | Keys.V: ReplaceImage(); return true;
            case Keys.Control | Keys.S: SaveImage(); return true;
            case Keys.Control | Keys.W: case Keys.Shift | Keys.Escape: case Keys.Escape: Close(); return true;
            case Keys.D0: ZoomAt(1,CenterPoint()); return true;
            case Keys.D1: Transform(RotateFlipType.Rotate90FlipNone); return true;
            case Keys.D2: Transform(RotateFlipType.Rotate270FlipNone); return true;
            case Keys.D3: Transform(RotateFlipType.RotateNoneFlipX); return true;
            case Keys.D4: Transform(RotateFlipType.RotateNoneFlipY); return true;
            case Keys.Add: case Keys.Oemplus: case Keys.Shift | Keys.Oemplus: ZoomAt(zoom * 1.1, CenterPoint()); return true;
            case Keys.Subtract: case Keys.OemMinus: ZoomAt(zoom / 1.1, CenterPoint()); return true;
            case Keys.Up: Top--; return true;
            case Keys.Down: Top++; return true;
            case Keys.Left: Left--; return true;
            case Keys.Right: Left++; return true;
        }
        if ((key & Keys.KeyCode) == Keys.Menu) { magnifier = true; Invalidate(); return true; }
        if (magnifier && (key & Keys.KeyCode) == Keys.ShiftKey) { rgb = !rgb; Invalidate(); return true; }
        if (magnifier && (key & Keys.KeyCode) == Keys.C) {
            try { Clipboard.SetText(PixelText(ImagePoint())); }
            catch (ExternalException ex) { MessageBox.Show(this, ex.Message, Text); }
            return true;
        }
        return base.ProcessCmdKey(ref msg,key);
    }

    protected override void WndProc(ref Message m)
    {
        if ((m.Msg == 0x105 || m.Msg == 0x101) && m.WParam.ToInt32() == 0x12) {
            magnifier = false; Invalidate();
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            if (passThrough) recovery.Visible = false;
            recovery.Dispose(); tips.Dispose(); menu.Dispose(); picture.Dispose();
        }
        base.Dispose(disposing);
    }
}