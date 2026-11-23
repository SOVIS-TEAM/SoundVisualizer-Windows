$ErrorActionPreference = 'Stop'
$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"

# The official portable zip from GitHub Releases (64-bit only), checked by SHA-256.
$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  unzipLocation  = $toolsDir
  url64bit       = 'https://github.com/SOVIS-TEAM/SoundVisualizer-Windows/releases/download/SoundVisualizer_v2.1.1/SoundVisualizer_v2.1.1.zip'
  checksum64     = '384ac741db567e1e1f340f254e2b894f89df9f0f2ad43209ea94b82486ae5840'
  checksumType64 = 'sha256'
}
Install-ChocolateyZipPackage @packageArgs

# SoundVisualizer is a GUI app: the shim should not wait for it to exit.
New-Item -Path (Join-Path $toolsDir 'SoundVisualizer.exe.gui') -ItemType File -Force | Out-Null

$shortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'SoundVisualizer.lnk'
Install-ChocolateyShortcut -ShortcutFilePath $shortcut -TargetPath (Join-Path $toolsDir 'SoundVisualizer.exe') -WorkingDirectory $toolsDir
