$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '工程'
& "$project\Build-Managed.ps1"
Copy-Item -LiteralPath "$project\.build\managed\FenceCapture.exe" -Destination "$PSScriptRoot\PecoFenceNative\FenceCapture.exe" -Force
