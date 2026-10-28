<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <b>한국어</b> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="버전"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="플랫폼"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="라이선스"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="빌드"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="문서"></a>
</p>

## 소개

Windows를 다시 설치하기 전 드라이버를 백업하고, 오래된 프로그램에 필요한 런타임을 찾고, 쌓인 업데이트 캐시를 정리하는 일은 여기저기 도구를 오가게 만듭니다. WinTools는 이런 자주 하는 작업을 한 데스크톱 앱에 모았습니다.

WPF 앱이며 백그라운드에 상주하거나 시작 프로그램에 등록되지 않습니다. 업데이트 검색, 런타임 다운로드, WSL 온라인 배포판 목록을 요청할 때만 네트워크에 연결합니다. 창을 닫으면 앱이 종료됩니다.

모듈 10개: **정리, 최적화, 기능 관리, 업데이트 관리, 시작 관리, 구성 요소 관리, 런타임, 앱 관리, 드라이버 관리, 백업**.

## 요구 사항

- Windows 10 1809(17763) 이상 또는 Windows 11, x64
- 폴더 버전(frame)은 .NET 10 런타임을 포함하므로 별도 설치가 필요 없습니다
- 관리자 권한 필요(매니페스트에 선언됨). DISM, 드라이버, 서비스, 이미지 작업이 여기에 의존
- 일부 기능은 Windows 기본 구성 요소(DISM, Windows Update Agent, `pnputil`, WIMGAPI)를 호출합니다. 축소된 이미지에서는 제거되었을 수 있습니다

## 다운로드

| 빌드 | 설명 | 링크 |
| --- | --- | --- |
| 포터블 | 런타임 포함, 압축 해제 후 실행 | [WinTools-*-green-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| 폴더 버전 | .NET 10 런타임 포함(설치 불필요) | [WinTools-*-frame-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | `main` 푸시마다 자동 재빌드 | [Beta 프리릴리스](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

정식 버전에는 Certum 클라우드 서명과 타임스탬프가 있습니다. `Get-AuthenticodeSignature`로 확인할 수 있으며, 각 릴리스에 `SHA256SUMS.txt`가 함께 제공됩니다.

## 기능

### 시스템 개요

시스템 버전과 빌드 번호, CPU 및 메모리 사용률, 디스크별 남은 공간, 최근 활동. 모든 데이터는 로컬에서 읽습니다.

<details>
<summary>스크린샷</summary>

![시스템 개요](../public/screenshots/dashboard.png)

</details>

### 정리

디스크 단위로 검사하며 시스템 임시 폴더, 업데이트 캐시, 로그와 덤프, 브라우저 캐시, 개발 도구 캐시(npm, pip, NuGet, Gradle, Docker), 150개 이상 앱의 캐시 폴더를 대상으로 합니다.

결과는 분류별로 묶이고 크기와 안전 등급을 표시합니다. 사용 중인 파일은 건너뛰고 기록하며 전체 작업을 멈추지 않습니다. 규칙은 `src/WinTools.Infrastructure/Rules/`의 JSON이라 경로 추가에 코드 수정이 필요 없습니다.

<details>
<summary>스크린샷</summary>

![정리](../public/screenshots/cleanup.png)

</details>

### 최적화

개인 정보 및 원격 측정, 성능, 파일 탐색기, 시스템 동작, 네트워크, 업데이트, 게임.

모든 항목이 검사, 적용, 되돌리기를 지원하므로 변경 전에 현재 상태를 확인할 수 있습니다. 추천 항목은 영향이 분명한 것만 포함합니다.

<details>
<summary>스크린샷</summary>

![최적화](../public/screenshots/optimize.png)

</details>

### 기능 관리

- **시스템 기능**: DISM API로 모든 선택적 기능을 나열하고 용도별로 묶어 설명과 현재 상태를 표시합니다. 변경은 한 번에 적용합니다.
- **에디션 채널**: 현재 SKU, 버전, 정품 인증 상태를 읽습니다. 전환은 기본 제공 `changepk`와 범용 키를 사용하며 정품 인증은 하지 않습니다.

<details>
<summary>스크린샷</summary>

![시스템 기능](../public/screenshots/features-systemfeatures.png)

</details>

### 업데이트 관리

- **설치된 업데이트**: 목록 조회와 개별 제거.
- **설치 가능한 업데이트**: 수동으로 Windows Update를 검색해 분류, KB, 크기, 날짜를 표시하고 일괄 설치와 결과 요약을 제공합니다.
- **기능 플래그**: `FeatureManagement\Overrides`를 ID 단위로 관리합니다. 상태, 우선 순위, 변형을 다루며 가져오기, 내보내기, 초기화를 지원합니다.

<details>
<summary>스크린샷</summary>

![설치 가능한 업데이트](../public/screenshots/updates-available.png)

</details>

### 시작 관리

한 페이지에 모았습니다. 실제 프로그램 아이콘을 표시하고 사용 상태는 스위치로 조작합니다. 서비스는 행 안에서 시작 유형(자동, 자동(지연), 수동, 사용 안 함)을 바꿀 수 있습니다.

<details>
<summary>스크린샷</summary>

![시작 관리](../public/screenshots/startup-services.png)

</details>

### 구성 요소 관리

- **보안 센터**: 14개 설정. 실시간 보호, 클라우드 보호, 동작 모니터링, PUA, 네트워크 보호, 제어된 폴더 액세스, 메모리 무결성, UAC, SmartScreen, 방화벽 프로필 3종, 제외 항목.
- **시스템 구성 요소**: 기본 앱(터미널, 계산기, 메모장, 그림판, 사진 등)의 설치·복구·제거, WSL 배포판, 로컬 패키지로 Android 하위 시스템 설치, 스토어 복구.

<details>
<summary>스크린샷</summary>

![보안 센터](../public/screenshots/components-securitycenter.png)

</details>

### 런타임

설치된 .NET과 Visual C++ 런타임을 나열합니다. 설치 가능한 버전은 필요할 때 가져옵니다. .NET은 공식 `releases-index.json`을 읽고 SHA512를 검증하며, VC++는 `aka.ms` 링크를 사용하고 Authenticode를 검증합니다. 둘 다 자동 설치되며 제거도 가능합니다.

<details>
<summary>스크린샷</summary>

![런타임](../public/screenshots/runtimes-dotnet.png)

</details>

### 앱 관리

아이콘, 버전, 예상 크기와 설치 정보를 확인할 수 있습니다. 일반 프로그램은 자체 제거 프로그램을 실행하고, Appx 패키지는 Windows 기본 패키지 관리 도구로 처리합니다.

<details>
<summary>스크린샷</summary>

![앱 관리](../public/screenshots/apps-programs.png)

</details>

### 드라이버 관리

DriverStore 전체를 관리합니다. 원하는 수만큼 패키지를 선택해 백업, 삭제하거나 폴더에서 설치할 수 있습니다. 일괄 작업은 항목별 진행률과 결과 요약을 보여 줍니다.

<details>
<summary>스크린샷</summary>

![드라이버 관리](../public/screenshots/drivers-systemdrivers.png)

</details>

### 백업

원본 볼륨은 드롭다운에서 선택합니다. 이미지 형식(wim / esd / swm)에 따라 확장자와 압축 방식이 바뀌고, 분할 크기는 swm에만 적용됩니다. 캡처 전에 기본 제외 항목을 보여 줍니다. 적용 시 이미지를 확인하고 `bcdboot`로 부팅을 복구할 수 있습니다.

<details>
<summary>스크린샷</summary>

![백업](../public/screenshots/backup-capture.png)

</details>

## 빌드

.NET 10 SDK가 필요합니다.

```powershell
# 디버그 빌드
dotnet build src/WinTools.App/WinTools.App.csproj

# Release 빌드
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# 두 가지 패키지를 artifacts/publish/{portable,framework} 에 생성
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

아키텍처, 모듈 구현, 규칙 형식, 지역화, 디버깅 방법은 **[wtools.opkg.cn](https://wtools.opkg.cn/)** 또는 [docs/guide/development.md](../guide/development.md)를 참고하세요.

## 참고 프로젝트

| 프로젝트 | 참고한 부분 |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | 최적화 항목 그룹화, 선택 후 적용 흐름 |
| [Optimizer](https://github.com/hellzerg/optimizer) | 최적화 분류, 위험 안내 문구, 시작 항목과 서비스 구성 |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | 중국어 환경의 최적화 목록과 정보 밀도 |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | DISM 사용법과 다국어 리소스 구성 |
| [Geek Uninstaller](https://geekuninstaller.com/) | 설치된 프로그램 목록의 항목 선정 |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | 기능 플래그 데이터 구조와 3상태 처리 |

정리 규칙은 개인이 정리한 캐시 경로 목록에서 시작해 이후 계속 보강했습니다. 현재는 `tools/rules/*.txt`로 관리하고 `tools/Generate-CleanupRules.ps1`로 생성합니다.

## 면책

- 이 도구는 레지스트리, 서비스 시작 유형, 시스템 기능, 드라이버를 수정합니다. 사용 전에 복원 지점을 만들거나 중요한 데이터를 백업하세요.
- 이 도구 사용으로 인한 데이터 손실이나 시스템 손상에 대해 작성자는 책임지지 않습니다.
- 소프트웨어 라이선스나 정품 인증을 우회하는 용도, 기타 현지 법령을 위반하는 용도로 사용하지 마세요.

## 라이선스

[AGPL-3.0](../../LICENSE) © WinTools

### 서드파티 구성 요소

| 구성 요소 | 라이선스 | 용도 |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | UI 글꼴, TTF로 포함 |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | MVVM 소스 생성기 |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | 종속성 주입 |
| [VitePress](https://vitepress.dev/) | MIT | 문서 사이트 전용 |
