param([string]$OutputRoot = "$PSScriptRoot\.build\releases")
$ErrorActionPreference='Stop'
$inputs = Join-Path $PSScriptRoot package
if (!(Test-Path $inputs)) { $inputs=Split-Path $PSScriptRoot }
$native = Join-Path $PSScriptRoot 'native\target\release'
$managed = Join-Path $PSScriptRoot '.build\managed'
if ((Get-Item -LiteralPath "$native\pecofence.exe").VersionInfo.ProductName -ne '桌面美化分区') {
    throw '主程序缺少公开版的名称和图标资源。请安装 Windows SDK 后重新构建。'
}
$build = Join-Path $OutputRoot ([Guid]::NewGuid().ToString('N'))
$stage = Join-Path $build '迁移包'
$payload = Join-Path $stage PecoFenceNative
New-Item -Type Directory $payload -Force | Out-Null
foreach ($item in 'Everything','使用说明.txt','BuildCapture.ps1') { Copy-Item -LiteralPath (Join-Path $inputs $item) -Destination $stage -Recurse }
foreach ($item in 'config','licenses','WebView2Loader.dll') { Copy-Item -LiteralPath (Join-Path "$inputs\PecoFenceNative" $item) -Destination $payload -Recurse }
foreach ($file in 'pecofence.exe','pecofence-cli.exe','pecofence-watchdog.exe') {Copy-Item -LiteralPath "$native\$file" -Destination $payload}
Copy-Item "$managed\FenceCapture.exe" $payload
foreach ($file in '一键安装.exe','安装到D盘.exe') {Copy-Item "$managed\一键安装.exe" (Join-Path $stage $file)}
$source = New-Item -Type Directory "$stage\工程" -Force
foreach ($item in 'capture','installer','tests','Build-Managed.ps1','Build-Native.ps1','Test-Managed.ps1','Package.ps1','README.md') { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $item) -Destination $source -Recurse }
$nativeSource = New-Item -Type Directory "$source\native" -Force
foreach ($item in 'Cargo.toml','Cargo.lock','rust-toolchain.toml','LICENSE','NOTICE','.cargo','crates','extras','locales','ui','third_party','vendor','tools','skills') { Copy-Item -LiteralPath "$PSScriptRoot\native\$item" -Destination $nativeSource -Recurse -Force }
New-Item -Type Directory "$nativeSource\scripts" -Force | Out-Null
Copy-Item "$PSScriptRoot\native\scripts\test-pc-status.ps1","$PSScriptRoot\native\scripts\cli-smoke.ps1" "$nativeSource\scripts"
& "$stage\一键安装.exe" --check
if ($LASTEXITCODE) {throw '迁移包校验失败。'}
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tests\verify-install.ps1" -Package $stage
if ($LASTEXITCODE) {throw "隔离安装校验失败。"}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath=Join-Path $build '桌面美化迁移包.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zipPath)
$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    foreach ($file in Get-ChildItem $stage -File -Recurse -Force) {
        $relative=$file.FullName.Substring($stage.Length+1).Replace('\','/')
        $entry=$archive.GetEntry($relative)
        if (!$entry) { $entry=$archive.GetEntry($relative.Replace('/','\')) }
        if (!$entry) { throw "ZIP 缺少文件 $relative" }
        $stream=$entry.Open()
        $sha=[Security.Cryptography.SHA256]::Create()
        try {$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
        if ($hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "ZIP 文件内容不匹配 $relative" }
    }
} finally {$archive.Dispose()}
Write-Output $zipPath
