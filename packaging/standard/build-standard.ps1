# GitHub 릴리스에 올리는 기본 버전 zip(SoundVisualizer_v버전.zip)을 만든다(#64).
# 예전부터의 구성 그대로다: SoundVisualizer.exe 옆에 그래픽·AI 엔진 DLL 7개와 AIModel 폴더, 라이선스 글(LICENSE, THIRD_PARTY.md,
# Fonts/Pretendard-LICENSE.txt). 압축을 모두 푼 뒤 실행해야 한다. exe 하나로 켜지는 포터블 zip 은 packaging/portable/build-portable.ps1 이다.
# 앱 안의 업데이트(ReleaseParser)와 Chocolatey 패키지는 이 이름의 zip 을 받는다.
#
# 사용 예:
#   ./packaging/standard/build-standard.ps1                 # 자산 이름의 버전은 SoundVisualizer.csproj 의 Version
#   ./packaging/standard/build-standard.ps1 -AssetVersion 2.4 # 2.4.0 을 "v2.4" 로 쓸 때
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
$assetName = "SoundVisualizer_v$AssetVersion.zip"
if ($assetName -notmatch '^SoundVisualizer_v\d{1,4}(\.\d{1,5}){1,3}\.zip$') { throw "앱 안의 업데이트가 받지 않는 자산 이름입니다: $assetName" }

$out = Join-Path $root $OutDir
$stage = Join-Path $out "standard-stage"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

Write-Host "게시: $project"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 실패 ($LASTEXITCODE)" }

# 기본 버전은 실행 파일 옆 파일이 있어야 켜진다. 빠졌으면 묶지 않는다.
$needed = @("SoundVisualizer.exe", "wpfgfx_cor3.dll", "PresentationNative_cor3.dll", "onnxruntime.dll", "AIModel/yamnet.onnx", "AIModel/yamnet.data", "AIModel/silero_vad.onnx", "AIModel/yamnet_class_map.csv")
foreach ($n in $needed) { if (-not (Test-Path (Join-Path $stage $n))) { throw "게시 폴더에 $n 이 없습니다(포터블 설정이 들어갔는지 확인)." } }

Get-ChildItem -Path $stage -Include *.pdb, *.lib -Recurse | Remove-Item -Force
Copy-Item (Join-Path $root "LICENSE") $stage
Copy-Item (Join-Path $root "THIRD_PARTY.md") $stage

$zip = Join-Path $out $assetName
if (Test-Path $zip) { Remove-Item -Force $zip }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in (Get-ChildItem -Path $stage -File -Recurse | Sort-Object FullName)) {
        $name = $f.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f.FullName, $name, [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}

$exe = Join-Path $stage "SoundVisualizer.exe"
$hash = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
Write-Host "만듦: $zip"
Write-Host "  크기 $((Get-Item $zip).Length) B, SHA-256 $hash, 항목 $((Get-ChildItem -Path $stage -File -Recurse).Count)개"
Write-Host "  exe $((Get-Item $exe).VersionInfo.ProductVersion)"
