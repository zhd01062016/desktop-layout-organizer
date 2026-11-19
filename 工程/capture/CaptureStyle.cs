using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

static class CaptureStyle
{
    internal static readonly Color Surface = SystemColors.Control, Ink = SystemColors.ControlText, Blue = SystemColors.Highlight, Border = SystemColors.ControlDark;
    internal static GraphicsPath Round(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        path.AddRectangle(bounds);
        return path;
    }
}

sealed partial class CaptureSelection
{
    [DllImport("Shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(Point point, uint flags);
    void UpdateToolbarScale(Point screen)
    {
        uint x = 96, y = 96;
        try
        {
            GetDpiForMonitor(MonitorFromPoint(screen, 2), 0, out x, out y);
        }
        catch (DllNotFoundException)
        {
        }

        uiScale = Math.Max(1, x / 96f);
    }

    void BuildToolbar()
    {
        bar.GripStyle = ToolStripGripStyle.Hidden;
        bar.Dock = DockStyle.None;
        bar.AutoSize = true;
        bar.ShowItemToolTips = true;
        bar.RenderMode = ToolStripRenderMode.System;
        bar.LayoutStyle = ToolStripLayoutStyle.Flow;
        bar.Padding = new Padding(4, 3, 4, 3);
        bar.Font = new Font("Microsoft YaHei UI", 9);
        string[] labels =
        {
            "选区",
            "矩形",
            "椭圆",
            "直线",
            "箭头",
            "画笔",
            "荧光笔",
            "马赛克",
            "文字"
        };
        foreach (Tool kind in Enum.GetValues(typeof(Tool)))
        {
            Tool current = kind;
            toolButtons.Add(kind, Button(labels[(int)kind], labels[(int)kind], delegate
            {
                SelectTool(current);
            }));
        }

        bar.Items.Add(new ToolStripSeparator());
        colorButton = Button("颜色", "标注颜色", delegate
        {
            using (var chooser = new ColorDialog
            {
                Color = ink,
                FullOpen = true
            }

            )
                if (chooser.ShowDialog(this) == DialogResult.OK)
                {
                    ink = chooser.Color;
                    ChangeStyle(delegate (Mark m)
                    {
                        if (m.Kind != Tool.Mosaic)
                            m.Color = ink;
                    });
                }
        });
        widthButton = new ToolStripDropDownButton("粗细");
        foreach (int value in new[]
        {
            2,
            3,
            6,
            10
        }

        )
        {
            int width = value;
            widthButton.DropDownItems.Add(value + " px", null, delegate
            {
                stroke = width;
                ChangeStyle(delegate (Mark m)
                {
                    if (m.Kind != Tool.Text)
                        m.Width = width;
                });
            });
        }

        bar.Items.Add(widthButton);
        sizeButton = new ToolStripDropDownButton("18 px");
        foreach (int value in new[]
        {
            14,
            18,
            24,
            32,
            48
        }

        )
        {
            int size = value;
            sizeButton.DropDownItems.Add(size + " px", null, delegate
            {
                fontSize = size;
                ChangeStyle(delegate (Mark m)
                {
                    if (m.Kind == Tool.Text)
                        m.FontSize = size;
                });
            });
        }

        bar.Items.Add(sizeButton);
        boldButton = Button("加粗", "文字加粗", delegate
        {
            bold = !bold;
            ChangeStyle(delegate (Mark m)
            {
                if (m.Kind == Tool.Text)
                    m.Bold = bold;
            });
        });
        undoButton = Button("撤销", "撤销  Ctrl+Z", Undo);
        redoButton = Button("重做", "重做  Ctrl+Y", Redo);
        bar.Items.Add(new ToolStripSeparator());
        Button("贴图", "置顶贴图  Ctrl+T", delegate
        {
            Finish(true);
        });
        Button("保存", "另存为  Ctrl+S", Save);
        Button("复制", "复制并结束  Enter", delegate
        {
            Finish(false);
        });
        Button("取消", "取消  Esc", Close);
        Button("历史", "24小时内最近十张截图  , / .", delegate
        {
            ShowHistory(1);
        });
        Button("识字", "识别选区文字并复制", Recognize);
        Controls.Add(bar);
        bar.Visible = false;
        toolButtons[Tool.Select].Checked = true;
        SyncStyle();
        UpdateActions();
    }

    ToolStripButton Button(string text, string tip, Action action)
    {
        var button = new ToolStripButton(text)
        {
            ToolTipText = tip,
            DisplayStyle = ToolStripItemDisplayStyle.Text
        };
        button.Click += delegate
        {
            action();
        };
        bar.Items.Add(button);
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeInput();
            bar.Font.Dispose();
        }

        base.Dispose(disposing);
    }
}
