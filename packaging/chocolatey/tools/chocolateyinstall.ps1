$ErrorActionPreference = 'Stop'
$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"

# The official portable zip from GitHub Releases (64-bit only), checked by SHA-256.
$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  unzipLocation  = $toolsDir
  url64bit       = 'https://github.com/SOVIS-TEAM/SoundVisualizer-Windows/releases/download/SoundVisualizer_v2.3.1/SoundVisualizer_v2.3.1.zip'
  checksum64     = 'c91b801d0931807dc68d9c1bb84f8ec22c0aa70ff18f6fb0336bcffb1159fb05'
  checksumType64 = 'sha256'
}
Install-ChocolateyZipPackage @packageArgs

# SoundVisualizer is a GUI app: the shim should not wait for it to exit.
New-Item -Path (Join-Path $toolsDir 'SoundVisualizer.exe.gui') -ItemType File -Force | Out-Null

$shortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'SoundVisualizer.lnk'
Install-ChocolateyShortcut -ShortcutFilePath $shortcut -TargetPath (Join-Path $toolsDir 'SoundVisualizer.exe') -WorkingDirectory $toolsDir
