param(
  [string]$Configuration = "Release",
  [string]$OutputDir = "artifacts/package",
  [string]$Version = "0.0.2"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$pluginProj = Join-Path $root "src/SkyFlatCampaignManager/SkyFlatCampaignManager.csproj"
$out = Join-Path $root $OutputDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

dotnet publish $pluginProj -c $Configuration -r win-x64 --self-contained false -o (Join-Path $out "publish")

$pluginName = "Sky Flat Campaign Manager"
$stage = Join-Path $out $pluginName
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item (Join-Path $out "publish/SkyFlatCampaignManager.dll") $stage
Copy-Item (Join-Path $out "publish/SkyFlatCampaignManager.Core.dll") $stage
if (Test-Path (Join-Path $out "publish/SkyFlatCampaignManager.pdb")) {
  Copy-Item (Join-Path $out "publish/SkyFlatCampaignManager.pdb") $stage
}

$zip = Join-Path $out "SkyFlatCampaignManager-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Get-FileHash $zip -Algorithm SHA256 | ForEach-Object {
  "$($_.Hash)  $(Split-Path $_.Path -Leaf)" | Set-Content -Path ($zip + ".sha256")
}

Write-Host "Package: $zip"
Get-ChildItem $stage | ForEach-Object { Write-Host " - $($_.Name)" }

# Content verification
$required = @("SkyFlatCampaignManager.dll", "SkyFlatCampaignManager.Core.dll")
foreach ($r in $required) {
  if (-not (Test-Path (Join-Path $stage $r))) { throw "Missing required package file: $r" }
}
