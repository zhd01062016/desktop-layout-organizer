using System;
using System.IO;
using System.Runtime.InteropServices;

static partial class Installer
{
    static void CreateShortcuts(string destination, string desktop, string programs)
    {
        string executable = Path.Combine(destination, "pecofence.exe");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        try
        {
            foreach (string directory in new[] { desktop, programs })
            {
                Directory.CreateDirectory(directory);
                dynamic shortcut = shell.CreateShortcut(Path.Combine(directory, "桌面美化分区.lnk"));
                try
                {
                    shortcut.TargetPath = executable;
                    shortcut.Arguments = "--portable";
                    shortcut.WorkingDirectory = destination;
                    shortcut.IconLocation = executable + ",0";
                    shortcut.Description = "启动桌面美化分区";
                    shortcut.Save();
                }
                finally { Marshal.FinalReleaseComObject(shortcut); }
            }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}
