# MSIX 패키지 (Microsoft Store)

Microsoft Store 에 올리는 Windows 판 패키지입니다. 스토어가 서명하므로 코드 서명 인증서는 필요 없습니다.

## 만들기

- GitHub: Actions > **msix** > Run workflow. 끝나면 실행 결과의 Artifacts 에서 `msix` 를 내려받습니다.
- 내 PC: Windows SDK(makeappx)가 있으면 저장소 맨 위에서 실행합니다.

  ```powershell
  ./packaging/msix/build-msix.ps1 -IdentityName "<Name>" -Publisher "<CN=...>"
  ```

결과는 `artifacts/SoundVisualizer_<버전>.0.msix` 입니다. 버전은 `SoundVisualizer/SoundVisualizer.csproj` 의 `Version` 에 `.0` 을 붙인 값입니다.

## 스토어 값

Partner Center 에서 앱 이름을 예약한 뒤 **제품 관리 > 제품 ID** 에 나오는 세 값을 씁니다. 지금 값(스토어 ID 9NGH48VCRFW2)은 저장소 변수에 들어 있습니다.

| 패키지 정보 | 넣는 곳 |
|---|---|
| Package/Identity/Name | `-IdentityName`, 또는 저장소 변수 `MSIX_IDENTITY_NAME` |
| Package/Identity/Publisher | `-Publisher`, 또는 저장소 변수 `MSIX_PUBLISHER` |
| Package/Properties/PublisherDisplayName | `-PublisherDisplayName`, 또는 저장소 변수 `MSIX_PUBLISHER_DISPLAY_NAME` (기본 SOVIS Team) |

저장소 변수를 넣어 두면 Run workflow 에서 입력하지 않아도 됩니다. 값을 넣지 않으면 묶이는지만 보는 시험용 값으로 만듭니다.

## 알아 둘 점

- 내용은 릴리스 zip 과 같습니다: win-x64 자체 포함 단일 파일, AI 모델, 글꼴 라이선스, `LICENSE`, `THIRD_PARTY.md`.
- 패키지로 설치하면 설치 폴더가 읽기 전용이라 설정 파일은 `%LocalAppData%\SoundVisualizer\settings.json` 에 씁니다(`AppPaths`). zip 판은 지금처럼 실행 파일 옆에 씁니다.
- 직접 설치해 보려면 시험용 인증서로 서명해야 합니다. 스토어에 올릴 파일은 서명하지 않습니다.
