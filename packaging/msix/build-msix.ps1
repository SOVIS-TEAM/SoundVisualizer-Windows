# Microsoft Store 에 올릴 MSIX 패키지를 만든다.
# 릴리스 zip 과 같은 방식(win-x64, 자체 포함, 단일 파일)으로 게시한 뒤, 그 폴더에 패키지 정보와 타일 그림을 넣어 묶는다.
# 스토어가 서명하므로 여기서는 서명하지 않는다. 직접 설치해 보려면 따로 시험용 인증서로 서명해야 한다.
#
# 사용 예:
#   ./packaging/msix/build-msix.ps1 -IdentityName "12345SOVIS.SoundVisualizer" -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"
# 값은 Partner Center 의 앱 > 제품 관리 > 제품 ID(Product identity)에 있다. 비워 두면 묶는 시험용 값을 쓴다.
param(
    [string]$IdentityName = "SOVIS.SoundVisualizer",
    [string]$Publisher = "CN=SOVIS",
    [string]$PublisherDisplayName = "SOVIS Team",
    [string]$Version = "",
    [string]$OutDir = "artifacts"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$project = Join-Path $root "SoundVisualizer/SoundVisualizer.csproj"

if (-not $Version) {
    # 앱 버전(SoundVisualizer.csproj 의 Version)에 네 번째 자리 0 을 붙인다. 스토어는 네 번째 자리를 0 으로 둬야 한다.
    [xml]$proj = Get-Content -Raw -Encoding UTF8 $project
    $appVersion = @($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
    if (-not $appVersion) { throw "SoundVisualizer.csproj 에서 Version 을 찾지 못했습니다." }
    $Version = "$appVersion.0"
}
if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw "MSIX 버전은 a.b.c.0 꼴이어야 합니다: $Version" }

$out = Join-Path $root $OutDir
$stage = Join-Path $out "msix-stage"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

Write-Host "게시: $project"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 실패 ($LASTEXITCODE)" }

# 릴리스 zip 과 같게: 디버그 파일은 빼고, 라이선스와 제3자 고지는 함께 넣는다(AGPL, THIRD_PARTY.md).
Get-ChildItem -Path $stage -Include *.pdb, *.lib -Recurse | Remove-Item -Force
Copy-Item (Join-Path $root "LICENSE") $stage
Copy-Item (Join-Path $root "THIRD_PARTY.md") $stage
Copy-Item -Recurse (Join-Path $PSScriptRoot "Assets") (Join-Path $stage "Assets")
$manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot "AppxManifest.xml")
$manifest = $manifest.Replace('$IDENTITY_NAME$', $IdentityName).Replace('$PUBLISHER$', $Publisher).Replace('$PUBLISHER_DISPLAY_NAME$', $PublisherDisplayName).Replace('$VERSION$', $Version)
[System.IO.File]::WriteAllText((Join-Path $stage "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))

$kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits/10/bin"
$makeappx = Get-ChildItem -Path $kits -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe 를 찾지 못했습니다. Windows SDK 가 필요합니다." }

$package = Join-Path $out "SoundVisualizer_$Version.msix"
Write-Host "묶기: $($makeappx.FullName) -> $package"
& $makeappx.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw "makeappx 실패 ($LASTEXITCODE)" }
Write-Host "만듦: $package"
