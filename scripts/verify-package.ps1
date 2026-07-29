param([Parameter(Mandatory=$true)][string]$ZipPath)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
  $names = $zip.Entries | ForEach-Object { $_.FullName }
  $names | ForEach-Object { Write-Host $_ }
  if (-not ($names | Where-Object { $_ -like "*SkyFlatCampaignManager.dll" })) { throw "DLL missing" }
  if (-not ($names | Where-Object { $_ -like "*SkyFlatCampaignManager.Core.dll" })) { throw "Core DLL missing" }
  if ($names | Where-Object { $_ -like "*.campaign.json" }) { throw "Campaign state must not be packaged" }
  Write-Host "Package content OK"
}
finally { $zip.Dispose() }
