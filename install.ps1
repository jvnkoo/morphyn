$sourceBinary = "morphyn-windows-x64.exe"
if (!(Test-Path $sourceBinary)) {
    $sourceBinary = "morphyn.exe"
}

$installDir = "$env:USERPROFILE\Morphyn"
$destName = "morphyn.exe"

if (!(Test-Path $installDir)) {
    New-Item -ItemType Directory -Force -Path $installDir
}

Copy-Item $sourceBinary -Destination "$installDir\$destName"

$oldPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($oldPath -notlike "*$installDir*") {
    $newPath = "$oldPath;$installDir"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    $env:Path += ";$installDir"
}

Write-Host "Success! Installed to $installDir" -ForegroundColor Green
Write-Host "Restart your terminal and type 'morphyn' to start." -ForegroundColor Cyan