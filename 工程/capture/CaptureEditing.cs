using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

sealed class CaptureTextInput : TextBox
{
    internal bool Composing;
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x10d)
            Composing = true;
        if (m.Msg == 0x10e)
            Composing = false;
        base.WndProc(ref m);
    }
}

sealed partial class CaptureSelection
{
    const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
    sealed class EditState
    {
        internal List<Mark> Marks;
        internal int Editable;
    }

    readonly Stack<EditState> undoStates = new Stack<EditState>(), redoStates = new Stack<EditState>();
    Mark editable, originalMark;
    EditState pending;
    Panel textFrame;
    bool editingExisting;
    int handleIndex = -1, fontSize = 18;
    bool bold;
    float uiScale = 1;
    ToolStripButton colorButton, boldButton, undoButton, redoButton;
    ToolStripDropDownButton widthButton, sizeButton;
    static Mark CloneMark(Mark m)
    {
        var n = new Mark
        {
            Kind = m.Kind,
            Start = m.Start,
            End = m.End,
            Width = m.Width,
            Color = m.Color,
            Text = m.Text,
            FontSize = m.FontSize,
            Bold = m.Bold,
            TextWidth = m.TextWidth
        };
        n.Points.AddRange(m.Points);
        return n;
    }

    EditState Snapshot()
    {
        var state = new EditState
        {
            Marks = new List<Mark>(),
            Editable = marks.IndexOf(editable)
        };
        foreach (var m in marks)
            state.Marks.Add(CloneMark(m));
        return state;
    }

    void Restore(EditState state)
    {
        marks.Clear();
        foreach (var m in state.Marks)
            marks.Add(CloneMark(m));
        editable = state.Editable >= 0 && state.Editable < marks.Count ? marks[state.Editable] : null;
        SyncStyle();
        Invalidate();
    }

    void Record(EditState state)
    {
        undoStates.Push(state);
        redoStates.Clear();
        UpdateActions();
    }

    void UpdateActions()
    {
        if (undoButton != null)
        {
            undoButton.Enabled = undoStates.Count > 0;
            redoButton.Enabled = redoStates.Count > 0;
        }
    }

    void Undo()
    {
        RestorePrevious(undoStates, redoStates);
    }

    void Redo()
    {
        RestorePrevious(redoStates, undoStates);
    }

    void RestorePrevious(Stack<EditState> source, Stack<EditState> destination)
    {
        CommitText();
        if (source.Count == 0)
            return;
        destination.Push(Snapshot());
        Restore(source.Pop());
        UpdateActions();
    }

    static Font MarkFont(Mark m)
    {
        return new Font("Microsoft YaHei UI", m.FontSize, m.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }

    Rectangle MarkBounds(Mark m)
    {
        if (m.Kind == Tool.Text)
        {
            using (var font = MarkFont(m))
            {
                var size = TextRenderer.MeasureText(m.Text ?? " ", font, new Size(m.TextWidth, 100000), TextFlags);
                return new Rectangle(m.Start, new Size(Math.Min(m.TextWidth, Math.Max(1, size.Width)), Math.Max(1, size.Height)));
            }
        }

        if (m.Kind == Tool.Pen || m.Kind == Tool.Highlight)
        {
            int l = m.Start.X, t = m.Start.Y, r = l, b = t;
            foreach (var p in m.Points)
            {
                l = Math.Min(l, p.X);
                r = Math.Max(r, p.X);
                t = Math.Min(t, p.Y);
                b = Math.Max(b, p.Y);
            }

            return Rectangle.FromLTRB(l, t, r + 1, b + 1);
        }

        var box = Between(m.Start, m.End);
        return new Rectangle(box.Location, new Size(Math.Max(1, box.Width), Math.Max(1, box.Height)));
    }

    Point[] Handles(Mark m)
    {
        if (m.Kind == Tool.Line || m.Kind == Tool.Arrow)
            return new[]
            {
                m.Start,
                m.End
            };
        if (m.Kind != Tool.Rectangle && m.Kind != Tool.Ellipse && m.Kind != Tool.Mosaic)
            return new Point[0];
        var b = MarkBounds(m);
        int x = b.Left + b.Width / 2, y = b.Top + b.Height / 2;
        return new[]
        {
            new Point(b.Left, b.Top),
            new Point(x, b.Top),
            new Point(b.Right, b.Top),
            new Point(b.Right, y),
            new Point(b.Right, b.Bottom),
            new Point(x, b.Bottom),
            new Point(b.Left, b.Bottom),
            new Point(b.Left, y)
        };
    }

    int HitHandle(Point p)
    {
        if (editable == null)
            return -1;
        var handles = Handles(editable);
        for (int i = 0; i < handles.Length; i++)
            if (Math.Abs(p.X - handles[i].X) <= 7 * uiScale && Math.Abs(p.Y - handles[i].Y) <= 7 * uiScale)
                return i;
        return -1;
    }

    bool HitMark(Point p)
    {
        return editable != null && Rectangle.Inflate(MarkBounds(editable), (int)(5 * uiScale), (int)(5 * uiScale)).Contains(p);
    }

    void DrawEditing(Graphics g)
    {
        if (editable == null || input != null || tool == Tool.Select)
            return;
        var state = g.Save();
        g.SetClip(region);
        using (var pen = new Pen(Color.FromArgb(70, 126, 222), 1))
        {
            pen.DashStyle = DashStyle.Dash;
            g.DrawRectangle(pen, MarkBounds(editable));
            pen.DashStyle = DashStyle.Solid;
            int side = Math.Max(6, (int)(6 * uiScale));
            foreach (var p in Handles(editable))
            {
                var b = new Rectangle(p.X - side / 2, p.Y - side / 2, side, side);
                g.FillRectangle(Brushes.White, b);
                g.DrawRectangle(pen, b);
            }
        }

        g.Restore(state);
    }

    void SelectTool(Tool next)
    {
        CommitText();
        tool = next;
        foreach (var pair in toolButtons)
            pair.Value.Checked = pair.Key == tool;
        SyncStyle();
        Cursor = tool == Tool.Text ? Cursors.IBeam : Cursors.Cross;
        PositionToolbar();
        Invalidate();
    }

    void SyncStyle()
    {
        var m = editable;
        if (m != null)
        {
            ink = m.Color;
            stroke = m.Width;
            if (m.Kind == Tool.Text)
            {
                fontSize = m.FontSize;
                bold = m.Bold;
            }
        }

        bool text = tool == Tool.Text || (m != null && m.Kind == Tool.Text && tool != Tool.Select);
        if (sizeButton == null)
            return;
        sizeButton.Visible = boldButton.Visible = text;
        widthButton.Visible = !text;
        colorButton.Enabled = tool != Tool.Mosaic && (m == null || m.Kind != Tool.Mosaic);
        sizeButton.Text = fontSize + " px";
        boldButton.Checked = bold;
        colorButton.ForeColor = ink;
        widthButton.Text = "粗细";
    }

    void ChangeStyle(Action<Mark> change)
    {
        if (input != null)
        {
            change(active);
            ApplyInputFont();
            Invalidate();
            return;
        }

        if (editable != null && tool != Tool.Select)
        {
            var before = Snapshot();
            change(editable);
            Record(before);
            Invalidate();
        }

        SyncStyle();
        PositionToolbar();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right)
        {
            Close();
            return;
        }

        if (e.Button != MouseButtons.Left)
            return;
        CommitText();
        anchor = pointer = e.Location;
        initial = region;
        if (Selected && tool != Tool.Select && editable != null && (HitHandle(pointer) >= 0 || HitMark(pointer)))
        {
            pending = Snapshot();
            originalMark = CloneMark(editable);
            handleIndex = HitHandle(pointer);
            drag = handleIndex >= 0 ? Drag.MarkResize : Drag.MarkMove;
        }
        else
        {
            edges = Selected && tool == Tool.Select ? HitEdges(pointer) : Edges.None;
            if (edges != Edges.None)
                drag = Drag.Resize;
            else if (!Selected || !region.Contains(pointer))
                drag = Drag.Create;
            else if (tool == Tool.Select)
                drag = Drag.Move;
            else
            {
                pending = Snapshot();
                editable = null;
                active = new Mark
                {
                    Kind = tool,
                    Start = pointer,
                    End = pointer,
                    Width = stroke,
                    Color = ink,
                    FontSize = fontSize,
                    Bold = bold,
                    TextWidth = Math.Max(1, Math.Min(360, region.Right - pointer.X))
                };
                active.Points.Add(pointer);
                if (tool == Tool.Text)
                {
                    editingExisting = false;
                    EditText();
                    return;
                }

                drag = Drag.Draw;
            }
        }

        Capture = true;
        bar.Hide();
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        pointer = e.Location;
        if (drag == Drag.None)
        {
            if (!Selected)
                hover = Detect(pointer);
            Cursor = tool != Tool.Select && HitHandle(pointer) >= 0 ? Cursors.SizeAll : tool != Tool.Select && HitMark(pointer) ? Cursors.SizeAll : tool == Tool.Text ? Cursors.IBeam : Cursors.Cross;
            Invalidate();
            return;
        }

        int dx = pointer.X - anchor.X, dy = pointer.Y - anchor.Y;
        if (drag == Drag.Create)
            region = Rectangle.Intersect(ClientRectangle, Between(anchor, pointer));
        else if (drag == Drag.Move)
            region.Location = new Point(Clamp(initial.X + dx, 0, Width - initial.Width), Clamp(initial.Y + dy, 0, Height - initial.Height));
        else if (drag == Drag.Resize)
            region = Rectangle.FromLTRB((edges & Edges.Left) != 0 ? Clamp(initial.Left + dx, 0, initial.Right - 1) : initial.Left, (edges & Edges.Top) != 0 ? Clamp(initial.Top + dy, 0, initial.Bottom - 1) : initial.Top, (edges & Edges.Right) != 0 ? Clamp(initial.Right + dx, initial.Left + 1, Width) : initial.Right, (edges & Edges.Bottom) != 0 ? Clamp(initial.Bottom + dy, initial.Top + 1, Height) : initial.Bottom);
        else if (drag == Drag.Draw && active != null)
        {
            active.End = new Point(Clamp(pointer.X, region.Left, region.Right - 1), Clamp(pointer.Y, region.Top, region.Bottom - 1));
            if (active.Kind == Tool.Pen || active.Kind == Tool.Highlight)
                active.Points.Add(active.End);
        }
        else if (drag == Drag.MarkMove)
        {
            var b = MarkBounds(originalMark);
            dx = Clamp(dx, region.Left - b.Left, region.Right - b.Right);
            dy = Clamp(dy, region.Top - b.Top, region.Bottom - b.Bottom);
            editable.Start = new Point(originalMark.Start.X + dx, originalMark.Start.Y + dy);
            editable.End = new Point(originalMark.End.X + dx, originalMark.End.Y + dy);
            editable.Points.Clear();
            foreach (var p in originalMark.Points)
                editable.Points.Add(new Point(p.X + dx, p.Y + dy));
        }
        else if (drag == Drag.MarkResize)
        {
            var p = new Point(Clamp(pointer.X, region.Left, region.Right - 1), Clamp(pointer.Y, region.Top, region.Bottom - 1));
            if (editable.Kind == Tool.Line || editable.Kind == Tool.Arrow)
            {
                if (handleIndex == 0)
                    editable.Start = p;
                else
                    editable.End = p;
            }
            else
            {
                var b = MarkBounds(originalMark);
                int l = b.Left, t = b.Top, r = b.Right, bt = b.Bottom;
                if (handleIndex == 0 || handleIndex == 6 || handleIndex == 7)
                    l = Math.Min(p.X, r - 1);
                if (handleIndex <= 2)
                    t = Math.Min(p.Y, bt - 1);
                if (handleIndex >= 2 && handleIndex <= 4)
                    r = Math.Max(p.X, l + 1);
                if (handleIndex >= 4 && handleIndex <= 6)
                    bt = Math.Max(p.Y, t + 1);
                editable.Start = new Point(l, t);
                editable.End = new Point(r, bt);
            }
        }

        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (drag == Drag.None || e.Button != MouseButtons.Left)
            return;
        OnMouseMove(e);
        if (drag == Drag.Create && (region.Width < 2 || region.Height < 2))
            region = Detect(e.Location);
        if (drag == Drag.Draw && active != null)
        {
            marks.Add(active);
            editable = active;
            active = null;
            Record(pending);
        }

        if ((drag == Drag.MarkMove || drag == Drag.MarkResize) && pointer != anchor)
            Record(pending);
        pending = null;
        drag = Drag.None;
        Capture = false;
        PositionToolbar();
        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (tool != Tool.Select && editable != null && editable.Kind == Tool.Text && HitMark(e.Location))
        {
            drag = Drag.None;
            Capture = false;
            pending = Snapshot();
            active = editable;
            editingExisting = true;
            EditText();
            return;
        }

        if (Selected && tool == Tool.Select && region.Contains(e.Location))
            Finish(false);
    }
}
