param([string]$Binary = "$PSScriptRoot\.build\managed\FenceCapture.exe")
$ErrorActionPreference='Stop'
$env:PECO_CAPTURE_BINARY = (Resolve-Path $Binary).Path
$env:PECO_TEST_OUTPUT = (New-Item -ItemType Directory -Path "$PSScriptRoot\.build\tests" -Force).FullName
foreach ($suite in 'capture','history','regression','retention') {
    powershell.exe -NoProfile -Sta -ExecutionPolicy Bypass -File "$PSScriptRoot\tests\verify-$suite.ps1"
    if ($LASTEXITCODE) { throw "验证失败 $suite" }
}
