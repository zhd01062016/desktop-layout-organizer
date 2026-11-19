param([string]$OutputDirectory = "$PSScriptRoot\.build\managed", [string]$ContractsArchive)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw '请通过 Windows PowerShell 5.1 运行此脚本。' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$cache = Join-Path $PSScriptRoot '.build\contracts-10.0.22621.2428'
$expected = 'DBEBA07806DACDB38A8F6E4677E2B06C52D79AEAB58E0F5B38191BD41314E416'
if (!$ContractsArchive) { $ContractsArchive = Join-Path (Split-Path $PSScriptRoot) 'sdk-contracts.zip' }
if (!(Test-Path $ContractsArchive)) {
    $ContractsArchive = Join-Path $PSScriptRoot '.build\contracts.zip'
    New-Item -Type Directory (Split-Path $ContractsArchive) -Force | Out-Null
    Invoke-WebRequest -UseBasicParsing 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.contracts/10.0.22621.2428/microsoft.windows.sdk.contracts.10.0.22621.2428.nupkg' -OutFile $ContractsArchive
}
if ((Get-FileHash $ContractsArchive).Hash -ne $expected) { throw 'Windows SDK Contracts 校验失败。' }
Expand-Archive -LiteralPath $ContractsArchive -DestinationPath $cache -Force
$refs = @('System.Drawing.dll','System.Windows.Forms.dll')
foreach ($name in 'System.Runtime.WindowsRuntime','System.Runtime','System.Runtime.InteropServices.WindowsRuntime') {
    $refs += [Reflection.Assembly]::LoadWithPartialName($name).Location
}
$refs += (Get-ChildItem "$cache\ref\netstandard2.0\*.winmd").FullName
$flags = $refs | ForEach-Object { "/reference:$_" }
New-Item -Type Directory $OutputDirectory -Force | Out-Null
$capture = (Get-ChildItem "$PSScriptRoot\capture\*.cs").FullName
& $compiler /nologo /target:winexe /platform:x64 $flags "/out:$OutputDirectory\FenceCapture.exe" $capture
if ($LASTEXITCODE) { throw '截图组件编译失败。' }
$installer = (Get-ChildItem "$PSScriptRoot\installer\*.cs").FullName
& $compiler /nologo /target:exe /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll "/out:$OutputDirectory\一键安装.exe" $installer
if ($LASTEXITCODE) { throw '安装器编译失败。' }
