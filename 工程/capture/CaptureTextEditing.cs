using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

sealed partial class CaptureSelection
{
    void ApplyInputFont()
    {
        if (input == null)
            return;
        var old = input.Font;
        input.Font = MarkFont(active);
        old.Dispose();
        input.ForeColor = active.Color;
        sizeButton.Text = active.FontSize + " px";
        boldButton.Checked = active.Bold;
        colorButton.ForeColor = active.Color;
        ResizeInput();
    }

    void ResizeInput()
    {
        if (input == null)
            return;
        var size = TextRenderer.MeasureText(input.Text + " ", input.Font, new Size(active.TextWidth, 100000), TextFlags);
        int height = Math.Min(Math.Max(input.Font.Height + 2, size.Height + 2), Math.Max(1, region.Bottom - active.Start.Y));
        input.Size = new Size(active.TextWidth, height);
        textFrame.Size = new Size(input.Width + 8, input.Height + 8);
    }

    void EditText()
    {
        textFrame = new Panel
        {
            BackColor = SystemColors.Window,
            Location = new Point(active.Start.X - 4, active.Start.Y - 4)
        };
        textFrame.Paint += delegate (object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(SystemColors.WindowFrame))
                e.Graphics.DrawRectangle(pen, 0, 0, textFrame.Width - 1, textFrame.Height - 1);
        };
        input = new CaptureTextInput
        {
            Multiline = true,
            AcceptsReturn = true,
            WordWrap = true,
            BorderStyle = BorderStyle.None,
            Location = new Point(4, 4),
            BackColor = textFrame.BackColor,
            ForeColor = active.Color,
            Font = MarkFont(active),
            Text = active.Text ?? ""
        };
        input.TextChanged += delegate
        {
            ResizeInput();
        };
        textFrame.Controls.Add(input);
        Controls.Add(textFrame);
        ResizeInput();
        textFrame.BringToFront();
        input.Focus();
        input.SelectionStart = input.TextLength;
        PositionToolbar();
        Invalidate();
    }

    void DisposeInput()
    {
        if (input == null)
            return;
        var font = input.Font;
        Controls.Remove(textFrame);
        textFrame.Dispose();
        font.Dispose();
        input = null;
        textFrame = null;
    }

    void CommitText()
    {
        if (input == null)
            return;
        string text = input.Text;
        DisposeInput();
        if (!String.IsNullOrWhiteSpace(text))
        {
            active.Text = text;
            if (!editingExisting)
                marks.Add(active);
            editable = active;
            Record(pending);
        }
        else if (editingExisting)
        {
            marks.Remove(active);
            editable = null;
            Record(pending);
        }
        else if (pending != null)
            Restore(pending);
        active = null;
        pending = null;
        editingExisting = false;
        SyncStyle();
        Invalidate();
    }

    void CancelText()
    {
        if (input == null)
            return;
        DisposeInput();
        active = null;
        if (pending != null)
            Restore(pending);
        pending = null;
        editingExisting = false;
        Invalidate();
    }
}
