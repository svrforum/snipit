param(
    [Parameter(Mandatory=$true)][string]$SnipItExe,
    [string]$DotnetPath = "dotnet"
)
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('SnipIt-update-e2e-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$originalLog = $env:SNIPIT_FIXTURE_LOG
$worker = $null
$parent = $null
try {
    $fixture = Join-Path $PSScriptRoot 'UpdateFixture\UpdateFixture.csproj'
    & $DotnetPath publish $fixture -c Release -p:Version=1.0.0 -o (Join-Path $testRoot 'old') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fixture 1 build failed' }
    & $DotnetPath publish $fixture -c Release -p:Version=2.0.0 -o (Join-Path $testRoot 'new') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fixture 2 build failed' }
    $install = Join-Path $testRoot '설치 경로'
    $staging = Join-Path $testRoot 'download'
    New-Item -ItemType Directory $install,$staging | Out-Null
    $target = Join-Path $install 'SnipIt.exe'
    $payload = Join-Path $staging 'SnipIt.update.exe'
    $helper = Join-Path $staging 'UpdateHost.exe'
    Copy-Item (Join-Path $testRoot 'old\UpdateFixture.exe') $target
    Copy-Item (Join-Path $testRoot 'new\UpdateFixture.exe') $payload
    Copy-Item -LiteralPath $SnipItExe -Destination $helper
    $hash = (Get-FileHash $payload -Algorithm SHA256).Hash
    $env:SNIPIT_FIXTURE_LOG = Join-Path $testRoot 'launches.txt'
    $parent = Start-Process -FilePath $target -WindowStyle Hidden -PassThru
    $ticks = $parent.StartTime.ToUniversalTime().Ticks
    $start = [Diagnostics.ProcessStartInfo]::new($helper)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($arg in @('--apply-update', [string]$parent.Id, [string]$ticks, $target, $payload, $hash)) { $start.ArgumentList.Add($arg) }
    $worker = [Diagnostics.Process]::Start($start)
    if (!$worker.WaitForExit(30000)) { throw 'Updater helper timed out' }
    if ($worker.ExitCode -ne 0) { throw "Updater helper exit code $($worker.ExitCode)" }
    if ((Get-FileHash $target -Algorithm SHA256).Hash -ne $hash) { throw 'Installed executable hash mismatch' }
    $launches = @(Get-Content -LiteralPath $env:SNIPIT_FIXTURE_LOG)
    if ($launches.Count -ne 2 -or $launches[0] -ne '1' -or $launches[1] -ne '2') { throw 'Expected old process then new process launch' }
    if (!(Test-Path -LiteralPath (Join-Path $staging 'helper.ready'))) { throw 'Helper readiness handshake was not completed' }
    if (@(Get-ChildItem -LiteralPath $install -Filter '*.previous-*').Count -ne 1) { throw 'Backup not retained' }
    Write-Output 'PASS REAL helper startup, parent exit wait, Unicode/space path, executable swap, backup, and new process restart'
}
finally {
    foreach ($process in @($worker,$parent)) {
        if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
        if ($process) { $process.Dispose() }
    }
    $env:SNIPIT_FIXTURE_LOG = $originalLog
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\SnipIt-update-e2e-'
    if ($resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
