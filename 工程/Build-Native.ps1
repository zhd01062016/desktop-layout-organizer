param([switch]$Test)
$ErrorActionPreference = 'Stop'
$localCargo = Join-Path $PSScriptRoot '.build\cargo'
if (Test-Path "$localCargo\bin\rustup.exe") {
    $env:CARGO_HOME = $localCargo
    $env:RUSTUP_HOME = Join-Path $PSScriptRoot '.build\rustup'
    $env:Path = "$localCargo\bin;$env:Path"
}
Push-Location (Join-Path $PSScriptRoot native)
try {
    $packages = @('-p','pecofence','-p','pecofence-cli','-p','pecofence-watchdog')
    & cargo +1.95.0 build --release --locked @packages
    if ($LASTEXITCODE) { throw 'Rust release build failed' }
    if ($Test) {
        & cargo +1.95.0 test --locked --workspace
        if ($LASTEXITCODE) { throw 'Rust tests failed' }
    }
} finally { Pop-Location }
