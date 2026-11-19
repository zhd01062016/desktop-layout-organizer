using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

static partial class Installer
{
    const string InstallRoot = @"D:\home_bug";
    static readonly string Destination = Path.Combine(InstallRoot, "PecoFenceNative");
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Length == 2 && args[0] == "--configure-search") {
                ConfigureSearchService(args[1]);
                return 0;
            }
            string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PecoFenceNative");
            var json = new JavaScriptSerializer
            {
                MaxJsonLength = 4194304
            };
            var config = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(source, @"config\config.json")));
            ValidatePackage(source);
            if (args.Length == 1 && args[0] == "--check")
            {
                Console.WriteLine("Package files and configuration checked.");
                return 0;
            }

            CheckDestination();
            EnsureDependencies();
            // Creo must not inherit a tool runtime locale such as C.UTF-8.
            if (System.Globalization.CultureInfo.InstalledUICulture.Name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PRO_LANG", "chs", EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("PRO_LANG", "chs");
            }
            InstallFiles(source, json, config);
            Run(Path.Combine(Destination, "FenceCapture.exe"), "--install-history-cleanup");
            string cli = Path.Combine(Destination, "pecofence-cli.exe");
            StartDesktop(cli, json);
            ApplyLayout(cli, json);
            Run(cli, "settings set autostart true");
            Run(cli, "snapshot save migrated-layout");
            CreateShortcuts(Destination,
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            Console.WriteLine("安装完成。桌面分区已启用，搜索快捷键为 Alt + Q。");
            Console.WriteLine("新文件会自动归类、滚动定位并选中。");
            Console.WriteLine("在桌面直接打字可唤起搜索。按 Esc 关闭搜索。");
            Console.WriteLine("安装位置 " + Destination);
            Console.WriteLine("按回车关闭此窗口。");
            Console.ReadLine();
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine(error.Message);
            Console.WriteLine("请将此消息发给 Codex。按回车关闭。");
            Console.ReadLine();
            return 1;
        }
    }

    static void ValidatePackage(string source)
    {
        foreach (string file in new[]
        {
            "pecofence.exe",
            "pecofence-cli.exe",
            "FenceCapture.exe",
            "pecofence-watchdog.exe",
            "WebView2Loader.dll"
        }

        )
            if (!File.Exists(Path.Combine(source, file)))
                throw new IOException(file);
    }

    static void CheckDestination()
    {
        if (!Environment.Is64BitOperatingSystem || Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new InvalidOperationException("需要 Windows 10/11 64 位系统。");
        if (Process.GetProcessesByName("pecofence").Length > 0)
            throw new InvalidOperationException("请先从托盘菜单退出当前 PecoFence，再运行安装。");
        if (Directory.Exists(Destination))
            throw new InvalidOperationException("安装目录已存在。请让 Codex 检查并迁移现有配置。");
    }

    static void InstallFiles(string source, JavaScriptSerializer json, Dictionary<string, object> config)
    {
        string staging = Destination + ".installing-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(Destination));
        try
        {
            PrepareFiles(source, staging, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), json, config);
            Directory.Move(staging, Destination);
        }
        finally
        {
            // Only this installation attempt owns this GUID-named staging directory.
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }

    static void PrepareFiles(string source, string staging, string desktop, JavaScriptSerializer json, Dictionary<string, object> config)
    {
        Copy(source, staging);
        var settings = (Dictionary<string, object>)config["settings"];
        settings["desktopPath"] = desktop;
        settings["autostart"] = false;
        foreach (Dictionary<string, object> layout in (IEnumerable)config["layouts"])
            foreach (Dictionary<string, object> fence in (IEnumerable)layout["fences"])
                if ((string)fence["kind"] == "folderPortal")
                {
                    string title = (string)fence["title"];
                    if (Path.GetFileName(title) != title || title == "." || title == "..")
                        throw new IOException("无效的分区名");
                    string folder = Path.Combine(desktop, title);
                    Directory.CreateDirectory(folder);
                    ((Dictionary<string, object>)fence["source"])["path"] = folder;
                }

        File.WriteAllText(Path.Combine(staging, @"config\config.json"), json.Serialize(config), new UTF8Encoding(false));
        Run(Path.Combine(staging, "pecofence-cli.exe"), "config check \"" + Path.Combine(staging, @"config\config.json") + "\"");
    }

    static void StartDesktop(string cli, JavaScriptSerializer json)
    {
        var process = Process.Start(new ProcessStartInfo(Path.Combine(Destination, "pecofence.exe"), "--portable") { UseShellExecute = false, CreateNoWindow = true });
        bool ready = false;
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(350);
            if (process.HasExited)
                throw new InvalidOperationException("PecoFence 启动失败。");
            try
            {
                var state = json.Deserialize<Dictionary<string, object>>(Run(cli, "--ascii --timeout 500 status"));
                if (Convert.ToInt32(state["pid"]) == process.Id)
                {
                    ready = true;
                    break;
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (!ready)
            throw new InvalidOperationException("程序尚未就绪，请让 Codex 检查。");
    }

    static void ApplyLayout(string cli, JavaScriptSerializer json)
    {
        // Reuse the app's monitor detection, then map the saved layout to this screen.
        var screens = json.Deserialize<ArrayList>(Run(cli, "--ascii monitor list"));
        Dictionary<string, object> primary = null;
        foreach (Dictionary<string, object> screen in screens)
            if (Convert.ToBoolean(screen["primary"]))
                primary = screen;
        if (primary == null)
            throw new InvalidOperationException("未检测到主显示器。");
        var area = (Dictionary<string, object>)primary["workArea"];
        double scale = Math.Min(Convert.ToDouble(area["w"]) / 2560, Convert.ToDouble(area["h"]) / 1380);
        int width = Math.Max(220, (int)(490 * scale));
        int tall = Math.Max(160, (int)(380 * scale));
        int shortHeight = Math.Max(100, (int)(180 * scale));
        int gap = Math.Max(10, (int)(20 * scale));
        int right = Convert.ToInt32(area["x"]) + Convert.ToInt32(area["w"]) - gap;
        int top = Convert.ToInt32(area["y"]) + gap;
        string[] ids =
        {
            "4d2bb303",
            "204e6a5a",
            "2a39796b",
            "11f0d551",
            "4d3bf9d5"
        };
        for (int i = 0; i < ids.Length; i++)
        {
            int column = i == 0 ? 0 : (i == 1 || i == 3 ? 1 : 2);
            int row = i < 3 ? 0 : 1;
            int x = Math.Max(Convert.ToInt32(area["x"]), right - (3 - column) * width - (2 - column) * gap);
            int y = top + row * (shortHeight + gap);
            Run(cli, "fence move " + ids[i] + " --rect " + x + "," + y + "," + width + "," + (i == 0 ? tall : shortHeight));
        }
    }

    static string Run(string file, string arguments)
    {
        using (var process = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
        {
            var output = process.StandardOutput.ReadToEndAsync();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(error);
            return output.Result;
        }
    }

    static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(source))
            Copy(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
