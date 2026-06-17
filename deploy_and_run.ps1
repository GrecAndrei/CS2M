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

# Wipe stale deploy (PDB, IDB, cached files from prior broken builds)
Get-ChildItem -LiteralPath $TargetDir -File -ErrorAction SilentlyContinue | Remove-Item -Force

# Copy only the three CS2M assemblies. ILRepack merges the satellite DLLs
# (LiteNetLib, MessagePack, 0Harmony, System.*) into CS2M.dll, so they are
# not needed at runtime. Shipping them in the mod folder makes the game's
# mod manager treat each one as a separate "mod" entry, which produces
# spurious warnings ("in-game assembly ... should NOT be shipped with mod")
# and a dynamic-assembly NotSupportedException during GetModAssets that
# can leave ModInfo.assembly null, preventing OnLoad from being invoked.
$cs2mDlls = @('CS2M.dll', 'CS2M.API.dll', 'CS2M.BaseGame.dll')
foreach ($name in $cs2mDlls) {
    Copy-Item (Join-Path $BuildDir $name) -Destination $TargetDir -Force
}
Write-Host "Copied 3 CS2M assemblies"

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
