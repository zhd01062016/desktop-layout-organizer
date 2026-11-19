param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
$Package=(Resolve-Path -LiteralPath $Package).Path
$stage=Join-Path ([IO.Path]::GetTempPath()) ('pecofence-install-test-'+[Guid]::NewGuid().ToString('N'))
$desktop=Join-Path $stage Desktop
$installed=Join-Path $stage PecoFenceNative
$original=(Get-FileHash "$Package\PecoFenceNative\config\config.json").Hash
try {
    New-Item -Type Directory $desktop -Force | Out-Null
    $assembly=[Reflection.Assembly]::LoadFile("$Package\一键安装.exe")
    $json=New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $json.MaxJsonLength=4194304
    $config=$json.DeserializeObject([IO.File]::ReadAllText("$Package\PecoFenceNative\config\config.json"))
    $method=$assembly.GetType('Installer').GetMethod('PrepareFiles',[Reflection.BindingFlags]'NonPublic,Static')
    $method.Invoke($null,[object[]]@([string]"$Package\PecoFenceNative",[string]$installed,[string]$desktop,$json.PSObject.BaseObject,$config.PSObject.BaseObject))
    $result=Get-Content "$installed\config\config.json" -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($result.settings.desktopPath -ne $desktop) {throw 'Desktop binding'}
    foreach($layout in $result.layouts){foreach($f in $layout.fences){if($f.kind -eq 'folderPortal' -and $f.source.path -ne (Join-Path $desktop $f.title)){throw 'Portal location'}}}
    if (Test-Path (Join-Path $desktop '文件管理器.lnk')) {throw 'Unexpected desktop shortcut'}
    if ((Get-FileHash "$Package\PecoFenceNative\config\config.json").Hash -ne $original){throw 'Package template was modified'}
    $shortcuts=$assembly.GetType('Installer').GetMethod('CreateShortcuts',[Reflection.BindingFlags]'NonPublic,Static')
    $programs=Join-Path $stage Programs
    $shortcuts.Invoke($null,[object[]]@([string]$installed,[string]$desktop,[string]$programs)) | Out-Null
    $shell=New-Object -ComObject WScript.Shell
    foreach($folder in @($desktop,$programs)) {
        $shortcut=$shell.CreateShortcut((Join-Path $folder '桌面美化分区.lnk'))
        if($shortcut.TargetPath -ne (Join-Path $installed 'pecofence.exe') -or $shortcut.Arguments -ne '--portable' -or $shortcut.WorkingDirectory -ne $installed){throw 'Startup shortcut target or arguments incorrect'}
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
    }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    $env:LOCALAPPDATA=Join-Path $stage local
    $env:APPDATA=Join-Path $stage roaming
    $env:PECOFENCE_INSTANCE=[Guid]::NewGuid().ToString('N')
    $app=Start-Process "$installed\pecofence.exe" -ArgumentList '--no-hide-icons --exit-after 10000' -WindowStyle Hidden -PassThru
    try {
        $deadline=[DateTime]::UtcNow.AddSeconds(8)
        do {
            Start-Sleep -Milliseconds 200
            try { $info=& "$installed\pecofence-cli.exe" --ascii --timeout 500 status 2>$null } catch { $info=$null }
        } until($info -or $app.HasExited -or [DateTime]::UtcNow -gt $deadline)
        $status=$info | ConvertFrom-Json
        if ($status.pid -ne $app.Id -or $status.instance -ne $env:PECOFENCE_INSTANCE -or $status.configPath -ne "$installed\config\config.json" -or $status.fenceCount -lt 5) {throw 'Isolated desktop instance failed'}
        $rules=& "$installed\pecofence-cli.exe" --ascii rule list
        if ($LASTEXITCODE -or @($rules | ConvertFrom-Json).Count -lt 1) {throw 'Sorting rules unavailable'}
        if (!$app.WaitForExit(17000) -or $app.ExitCode) {throw 'Isolated desktop shutdown failed'}
    } finally {
        if(!$app.HasExited){$app.Kill();$app.WaitForExit()};$app.Dispose()
    }
    Write-Output 'PASS: direct executable startup uses local configuration; desktop and Start menu shortcuts verified'
    Write-Output 'PASS: isolated portable startup, scoped IPC, migrated fences and rules, clean shutdown'
    Write-Output 'PASS: isolated install staging, CLI validation, desktop folder bindings, source configuration integrity'
} finally {
    if(Test-Path -LiteralPath $stage) {
        $resolved=(Resolve-Path -LiteralPath $stage).Path
        if([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetTempPath().TrimEnd('\') -or [IO.Path]::GetFileName($resolved) -notmatch '^pecofence-install-test-[a-f0-9]{32}$'){throw 'Unexpected test path'}
        # The watchdog observes the app exit asynchronously and briefly holds its executable.
        foreach ($watchdog in Get-Process -Name pecofence-watchdog -ErrorAction SilentlyContinue) {
            if ($watchdog.Path -eq (Join-Path $installed 'pecofence-watchdog.exe')) {
                if (!$watchdog.WaitForExit(5000)) { throw 'Test watchdog did not exit' }
            }
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
