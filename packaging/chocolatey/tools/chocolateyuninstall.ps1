$ErrorActionPreference = 'Stop'

# The package folder itself is removed by Chocolatey; only the Start menu shortcut needs cleaning up.
$shortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'SoundVisualizer.lnk'
if (Test-Path $shortcut) {
  Remove-Item $shortcut -Force
}
