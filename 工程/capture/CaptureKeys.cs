using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class CaptureKeys : Form
{
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32")]
    static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint ms, out IntPtr result);
    readonly ComboBox[] mods = new ComboBox[2], keys = new ComboBox[2];
    readonly Label status = new Label();
    static readonly string[] ModNames =
    {
        "无",
        "Alt",
        "Ctrl",
        "Ctrl + Alt",
        "Shift",
        "Alt + Shift",
        "Ctrl + Shift",
        "Ctrl + Alt + Shift"
    };
    static ComboBox ListAt(Form owner, int x, int y, int width)
    {
        var list = new ComboBox
        {
            Left = x,
            Top = y,
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        owner.Controls.Add(list);
        return list;
    }

    public CaptureKeys()
    {
        Text = "截图和贴图快捷键";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", 10);
        ClientSize = new Size(420, 210);
        for (int i = 0; i < 2; i++)
        {
            int y = 18 + i * 44;
            Controls.Add(new Label { Text = i == 0 ? "截图" : "贴图", Left = 18, Top = y + 4, AutoSize = true });
            mods[i] = ListAt(this, 85, y, 180);
            mods[i].Items.AddRange(ModNames);
            keys[i] = ListAt(this, 278, y, 115);
            for (int k = 112; k <= 135; k++)
                keys[i].Items.Add((Keys)k);
            for (int k = 65; k <= 90; k++)
                keys[i].Items.Add((Keys)k);
            for (int k = 48; k <= 57; k++)
                keys[i].Items.Add((Keys)k);
        }

        SetPair(0x70, 0x71);
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"config\capture-keys.json");
        if (File.Exists(path))
        {
            string[] pair = File.ReadAllText(path).Trim().Trim('[', ']').Split(',');
            int a, b;
            if (pair.Length == 2 && int.TryParse(pair[0], out a) && int.TryParse(pair[1], out b))
                SetPair(a, b);
        }

        status.SetBounds(18, 109, 385, 46);
        Controls.Add(status);
        var reset = new Button
        {
            Text = "恢复默认",
            Left = 18,
            Top = 163,
            Width = 100,
            Height = 32
        };
        reset.Click += (s, e) =>
        {
            SetPair(0x70, 0x71);
            status.Text = "点击保存后生效。";
        };
        Controls.Add(reset);
        var save = new Button
        {
            Text = "保存",
            Left = 291,
            Top = 163,
            Width = 102,
            Height = 32
        };
        save.Click += (s, e) => SaveKeys();
        Controls.Add(save);
        AcceptButton = save;
    }

    void SetPair(int a, int b)
    {
        int i = 0;
        foreach (int val in new[]
        {
            a,
            b
        }

        )
        {
            if (val >= 0 && val < 2048 && keys[i].Items.Contains((Keys)(val & 255)))
            {
                mods[i].SelectedIndex = val >> 8;
                keys[i].SelectedItem = (Keys)(val & 255);
            }

            i++;
        }
    }

    void SaveKeys()
    {
        int[] pair = new int[2];
        for (int i = 0; i < 2; i++)
        {
            int key = (int)(Keys)keys[i].SelectedItem;
            if (mods[i].SelectedIndex == 0 && key < 112)
            {
                status.Text = "字母或数字需要搭配修饰键。";
                return;
            }

            pair[i] = mods[i].SelectedIndex * 256 + key;
        }

        if (pair[0] == pair[1])
        {
            status.Text = "截图和贴图需要使用不同的快捷键。";
            return;
        }

        var hwnd = FindWindow("PecoFence.Control", "PecoFence");
        IntPtr result;
        if (hwnd == IntPtr.Zero || SendMessageTimeout(hwnd, 0x85fa, (IntPtr)pair[0], (IntPtr)pair[1], 2, 2500, out result) == IntPtr.Zero)
        {
            status.Text = "桌面工具未响应，请启动后重试。";
            return;
        }

        if (result == IntPtr.Zero)
        {
            status.Text = "组合键可能已被占用，或配置无法写入。请选择其他组合后重试。";
            return;
        }

        Close();
    }
}
