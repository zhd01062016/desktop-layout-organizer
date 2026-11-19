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
    const string EverythingSetupUrl = "https://www.voidtools.com/Everything-1.5.0.1409a.x64-Setup.exe";
    const string WebViewSetupUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    static void WithVerifiedSetup(string url, string publisher, string expectedHash, Action<string> install)
    {
        string path = Path.Combine(Path.GetTempPath(), "desktop-layout-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            using (var client = new System.Net.WebClient())
                client.DownloadFile(url, path);

            if (expectedHash != null)
            {
                using (var hash = System.Security.Cryptography.SHA256.Create())
                using (var file = File.OpenRead(path))
                    if (BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "") != expectedHash)
                        throw new IOException("下载的安装文件校验失败。");
            }

            // PowerShell verifies trust and the signed publisher before the installer is run.
            string script = "$ErrorActionPreference = 'Stop'; " +
                "Import-Module (Join-Path $PSHOME 'Modules\\Microsoft.PowerShell.Security\\Microsoft.PowerShell.Security.psd1'); " +
                "$s = Get-AuthenticodeSignature -LiteralPath '" + path.Replace("'", "''") + "'; " +
                "if ($s.Status -ne 'Valid' -or !$s.SignerCertificate -or " +
                "$s.SignerCertificate.GetNameInfo('SimpleName', $false) -ne '" + publisher + "') " +
                "{ throw '安装程序的数字签名或发布者不匹配。' }";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            string powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            Run(powershell, "-NoProfile -NonInteractive -EncodedCommand " + encoded);
            install(path);
        }
        finally { File.Delete(path); }
    }

    static bool HasWebView()
    {
        const string key = @"\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        foreach (string root in new[]
        {
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node",
            @"HKEY_CURRENT_USER\Software"
        }

        )
        {
            Version version;
            if (Version.TryParse(Registry.GetValue(root + key, "pv", null) as string, out version) && version.Major > 0)
                return true;
        }

        return false;
    }

    static void EnsureDependencies()
    {
        if (!HasWebView())
        {
            Console.WriteLine("正在安装 Microsoft WebView2，请保持网络连接。");
            WithVerifiedSetup(WebViewSetupUrl, "Microsoft Corporation", null,
                path => Run(path, "/silent /install"));
            if (!HasWebView())
                throw new InvalidOperationException("WebView2 安装未完成。");
        }

        string exe = Path.Combine(InstallRoot, @"Everything\Everything.exe");
        if (!File.Exists(exe))
        {
            Console.WriteLine("正在安装 Everything 和索引服务，请确认 Windows 权限提示。");
            WithVerifiedSetup(EverythingSetupUrl, "voidtools PTY LTD",
                "F4141ABFB80F799C467B1E94409CB13CB1FDE6663EB473C5EA3CE876B6F183E4", path =>
            {
                var setup = new ProcessStartInfo(path, "/S -install-options \"-app-data -disable-run-as-admin -install-service -install-start-menu-shortcuts\" /D=" + Path.GetDirectoryName(exe))
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                using (var child = Process.Start(setup))
                {
                    child.WaitForExit();
                    if (child.ExitCode != 0 || !File.Exists(exe))
                        throw new InvalidOperationException("Everything 安装未完成。");
                }
            });
        }

        string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
        var configure = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,
            "--configure-search " + sid) { UseShellExecute = true, Verb = "runas" };
        using (var child = Process.Start(configure)) {
            child.WaitForExit();
            if (child.ExitCode != 0) throw new InvalidOperationException("Search service configuration failed.");
        }
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
            if (key != null) {
                key.DeleteValue("Everything", false);
                key.DeleteValue("Everything 1.5a", false);
            }
        }
    }

    static void ConfigureSearchService(string userSid)
    {
        var sid = new System.Security.Principal.SecurityIdentifier(userSid);
        string expected = Path.Combine(InstallRoot, @"Everything\Everything.exe");
        string command = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Everything (1.5a)", "ImagePath", "") as string;
        if (command == null || !command.StartsWith("\"" + expected + "\"", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected search service path.");
        string sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        Run(sc, "config \"Everything (1.5a)\" start= demand");
        string descriptor = Run(sc, "sdshow \"Everything (1.5a)\"").Trim();
        var security = new System.Security.AccessControl.RawSecurityDescriptor(descriptor);
        var acl = security.DiscretionaryAcl;
        bool granted = false;
        foreach (System.Security.AccessControl.GenericAce entry in acl) {
            var ace = entry as System.Security.AccessControl.CommonAce;
            if (ace != null && ace.SecurityIdentifier == sid && ace.AceQualifier == System.Security.AccessControl.AceQualifier.AccessAllowed
                && (ace.AccessMask & 0x34) == 0x34) granted = true;
        }
        if (!granted) acl.InsertAce(acl.Count, new System.Security.AccessControl.CommonAce(
            System.Security.AccessControl.AceFlags.None, System.Security.AccessControl.AceQualifier.AccessAllowed, 0x34, sid, false, null));
        Run(sc, "sdset \"Everything (1.5a)\" \"" + security.GetSddlForm(System.Security.AccessControl.AccessControlSections.All) + "\"");
    }
}
