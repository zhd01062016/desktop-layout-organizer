using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

static class CaptureHistory
{
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PecoFenceNative", "CaptureHistory");
    const int Limit = 10;
    static DateTime CapturedAt(string path)
    {
        string name = Path.GetFileName(path);
        DateTime time;
        if (name.Length >= 29 && DateTime.TryParseExact(name.Substring(8, 21), "yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time))
            return time;
        return File.GetLastWriteTimeUtc(path);
    }

    internal static void CleanupDirectory(string folder, DateTime nowUtc)
    {
        if (!Directory.Exists(folder))
            return;
        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "capture-*.png").Concat(Directory.GetFiles(folder, "capture-*.png.tmp")).ToArray();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (string path in files)
        {
            try
            {
                if (CapturedAt(path) <= nowUtc.AddHours(-24))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    internal static void Cleanup()
    {
        CleanupDirectory(Folder, DateTime.UtcNow);
    }

    internal static void InstallCleanup()
    {
        string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string name = "PecoFence Capture History " + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
        string command = "\\\"" + exe + "\\\" --cleanup-history";
        using (var process = Process.Start(new ProcessStartInfo("schtasks.exe", "/Create /F /SC MINUTE /MO 15 /RL LIMITED /IT /TN \"" + name + "\" /TR \"" + command + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
        {
            var output = process.StandardOutput.ReadToEndAsync();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("无法创建截图历史自动清理任务：" + error + output.Result);
        }

        Cleanup();
    }

    internal static string[] Entries()
    {
        Cleanup();
        if (!Directory.Exists(Folder))
            return new string[0];
        DateTime cutoff = DateTime.UtcNow.AddHours(-24);
        return Directory.GetFiles(Folder, "capture-*.png").Where(x => CapturedAt(x) > cutoff).OrderByDescending(x => x, StringComparer.Ordinal).Take(Limit).ToArray();
    }

    internal static void Remember(Bitmap image)
    {
        string staging = null;
        try
        {
            Directory.CreateDirectory(Folder);
            Cleanup();
            string path = Path.Combine(Folder, "capture-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N") + ".png");
            staging = path + ".tmp";
            image.Save(staging, ImageFormat.Png);
            File.Move(staging, path);
            foreach (string old in Directory.GetFiles(Folder, "capture-*.png").OrderByDescending(x => x, StringComparer.Ordinal).Skip(Limit))
                File.Delete(old);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
        }
        finally
        {
            if (staging != null)
            {
                try
                {
                    File.Delete(staging);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
