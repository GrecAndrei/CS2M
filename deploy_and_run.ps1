$GameDir = "g:\Games\Cities.Skylines.II.v1.5.3f1\Cities.Skylines.II.v1.5.3f1\game"
$BuildDir = "$GameDir\CS2M\CS2M\bin\Debug\net472"
$DistDir = "$GameDir\CS2M\dist\Mods\CS2M"
$TargetDir = "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CS2M"
$GameExe = "$GameDir\Cities2.exe"

Write-Host "Deploying CS2M to $TargetDir..."

# Create target dir
If (!(Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
}

# Copy DLL
Copy-Item "$BuildDir\CS2M.dll" -Destination $TargetDir -Force
Copy-Item "$BuildDir\CS2M.API.dll" -Destination $TargetDir -Force
Copy-Item "$BuildDir\CS2M.BaseGame.dll" -Destination $TargetDir -Force
Write-Host "Copied all DLLs"

# Copy UI
if (Test-Path $DistDir) {
    Copy-Item "$DistDir\*" -Destination $TargetDir -Recurse -Force
    Write-Host "Copied UI assets"
}
else {
    Write-Warning "UI Dist directory not found at $DistDir"
}

# Copy Lang
if (Test-Path "$BuildDir\lang") {
    Copy-Item "$BuildDir\lang" -Destination $TargetDir -Recurse -Force
    Write-Host "Copied Lang assets"
}

Write-Host "Deployment Complete."

# Launch Game
Write-Host "Launching Game in Developer Mode..."
Start-Process $GameExe -ArgumentList "-developerMode"
