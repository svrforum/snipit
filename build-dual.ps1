param([string]$Dotnet = "dotnet", [string]$Output = (Join-Path $PSScriptRoot "dist-dual"))
$ErrorActionPreference = "Stop"
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $Output -Force | Out-Null
foreach ($edition in @("WPF", "WinUI")) {
    $project = if ($edition -eq "WPF") { "src/WinCapture.csproj" } else { "src.WinUI/SnipIt.WinUI.csproj" }
    $folder = Join-Path $Output $edition
    & $Dotnet publish (Join-Path $PSScriptRoot $project) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableSmokeTests=false -o $folder
    if ($LASTEXITCODE -ne 0) { throw "$edition publish failed" }
    $asset = if ($edition -eq "WPF") { "SnipIt.exe" } else { "SnipIt-WinUI.exe" }
    Copy-Item -LiteralPath (Join-Path $folder "SnipIt.exe") -Destination (Join-Path $Output $asset) -Force
}
$checksums = foreach ($asset in @("SnipIt.exe", "SnipIt-WinUI.exe")) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $Output $asset) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $asset"
}
$checksums | Set-Content -LiteralPath (Join-Path $Output "SHA256SUMS.txt") -Encoding ascii
@'
SnipIt.exe: existing WPF edition (keeps the legacy update asset name).
SnipIt-WinUI.exe: WinUI preview edition.
Publish both executables and SHA256SUMS.txt together. Each edition selects its own update asset.
These local builds do not create or publish a GitHub release.
'@ | Set-Content -LiteralPath (Join-Path $Output "EDITIONS.txt") -Encoding utf8
