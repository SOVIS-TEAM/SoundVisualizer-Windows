# GitHub 릴리스에 함께 올리는 포터블 zip(SoundVisualizer_v버전_portable.zip)을 만든다(#58, #64).
# 실행 파일 하나에 그래픽·AI 엔진 DLL 과 AI 모델을 넣어(-p:Portable=true), zip 안에서 바로 열거나 exe 만 복사해도 켜진다.
# zip 에는 SoundVisualizer.exe 와 라이선스 글(LICENSE, THIRD_PARTY.md, Fonts/Pretendard-LICENSE.txt)만 넣는다.
# 이름 끝의 _portable 때문에 앱 안의 업데이트(ReleaseParser)는 이 zip 을 받지 않고, 기본 버전 zip(SoundVisualizer_v버전.zip,
# packaging/standard/build-standard.ps1)을 받는다. Microsoft Store 의 MSIX 는 packaging/msix/build-msix.ps1 로 따로 만든다.
#
# 사용 예:
#   ./packaging/portable/build-portable.ps1                 # 자산 이름의 버전은 SoundVisualizer.csproj 의 Version
#   ./packaging/portable/build-portable.ps1 -AssetVersion 2.4 # 2.4.0 을 "v2.4" 로 쓸 때
param(
    [string]$AssetVersion = "",
    [string]$OutDir = "artifacts"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$project = Join-Path $root "SoundVisualizer/SoundVisualizer.csproj"

if (-not $AssetVersion) {
    [xml]$proj = Get-Content -Raw -Encoding UTF8 $project
    $AssetVersion = @($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
    if (-not $AssetVersion) { throw "SoundVisualizer.csproj 에서 Version 을 찾지 못했습니다." }
}
$assetName = "SoundVisualizer_v${AssetVersion}_portable.zip"
if ($assetName -notmatch '^SoundVisualizer_v\d{1,4}(\.\d{1,5}){1,3}_portable\.zip$') { throw "자산 이름이 이상합니다: $assetName" }

$out = Join-Path $root $OutDir
$stage = Join-Path $out "portable-stage"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

Write-Host "게시: $project"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:Portable=true -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 실패 ($LASTEXITCODE)" }

# 실행 파일 옆에 DLL 이나 모델 폴더가 나왔으면 포터블 설정이 듣지 않은 것이다. 그대로 묶으면 exe 하나로는 켜지지 않는다.
$exe = Join-Path $stage "SoundVisualizer.exe"
if (-not (Test-Path $exe)) { throw "SoundVisualizer.exe 가 없습니다." }
$loose = @(Get-ChildItem -Path $stage -Filter *.dll) + @(Get-ChildItem -Path $stage -Directory -Filter AIModel)
if ($loose.Count -gt 0) { throw "실행 파일 밖에 남은 파일이 있습니다(포터블 설정 확인): $(($loose | ForEach-Object Name) -join ', ')" }

# zip 항목: 실행 파일과 라이선스 글. 디버그 파일(*.pdb, *.lib)은 넣지 않는다.
$entries = [ordered]@{
    "SoundVisualizer.exe"           = $exe
    "LICENSE"                       = (Join-Path $root "LICENSE")
    "THIRD_PARTY.md"                = (Join-Path $root "THIRD_PARTY.md")
    "Fonts/Pretendard-LICENSE.txt"  = (Join-Path $root "SoundVisualizer/Fonts/Pretendard-LICENSE.txt")
}
$zip = Join-Path $out $assetName
if (Test-Path $zip) { Remove-Item -Force $zip }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $entries.Keys) {
        if (-not (Test-Path $entries[$name])) { throw "없는 파일: $($entries[$name])" }
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entries[$name], $name, [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}

$version = (Get-Item $exe).VersionInfo.ProductVersion
$hash = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
Write-Host "만듦: $zip"
Write-Host "  크기 $((Get-Item $zip).Length) B, SHA-256 $hash"
Write-Host "  exe $version, $((Get-Item $exe).Length) B"
