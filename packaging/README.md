# File Renamer MSIX 패키징

현재 제출 후보는 **1.0.2.0 x64**입니다: `artifacts/msix/Release/Dohi.FileRenamerDohi_1.0.2.0_x64.msix`.

처음 출시한다면 [초보자용 배포 준비 가이드](STORE-PUBLISHING-GUIDE.ko.md)를 먼저 보세요. 지금 해야 할 일, Store 입력 순서, 소개문·심사 메모 초안, 다음 앱에 재사용할 점검표를 담았습니다.

2026-11-18에 [그림과 실제 화면을 넣은 따라 하기 가이드](../output/store-submission/1.0.2.0/처음부터-따라하기.html), [복사용 Store 문구](../output/store-submission/1.0.2.0/Store-입력문구.txt), 스크린샷 PNG 4장을 준비했습니다. [개인정보 안내](https://file-renamer-dohi.poisedcarnation32.chatgpt.site/privacy/)와 [지원 페이지](https://file-renamer-dohi.poisedcarnation32.chatgpt.site/support/)도 공개 배포했습니다. 앱의 Store 제출·출시는 별도로 진행해야 합니다. 자료의 검증 범위와 남은 일은 [최신 상태](../output/store-submission/1.0.2.0/링크-및-상태.txt)에 있습니다.

2026-11-13 전체 WACK 검사는 완료됐으며 최종 판정은 **WARNING**입니다. 24개 중 22개 PASS, 선택 검사 FAIL 1개, DPI 분석기 WARNING 1개입니다. 아래 후속 기록에 원인과 실제 실행 검증을 남겼습니다. Store 업로드 및 심사 제출은 아직 진행하지 않았습니다.

기존 WPF 앱을 자체 포함 배포하여 Microsoft Store 제출용 `.msix`를 생성합니다.
이 프로젝트는 `MakeAppx`를 호출하는 MSBuild `.proj`입니다. Visual Studio의 Windows Application Packaging Project(`.wapproj`) 템플릿이나 패키징 워크로드 없이 빌드할 수 있습니다.
기존 단일 EXE 배포는 루트 README의 명령으로 계속 생성할 수 있습니다.

## 등록된 제품

2026-11-10에 제공된 Partner Center 제품 ID 화면 기준입니다.

| 항목 | 값 |
| --- | --- |
| Package/Identity/Name | `Dohi.FileRenamerDohi` |
| Package/Identity/Publisher | `CN=E48A1617-7247-4DD8-BB45-93E40961D6D4` |
| PublisherDisplayName | `Dohi` |
| Package Family Name | `Dohi.FileRenamerDohi_0xzf0d7sark16` |
| Store ID | `9PF0VRZBLG81` |
| 표시 이름 | `File Renamer_Dohi` |

표시 이름은 기존 Store 안내에 기록된 예약 이름을 사용합니다. Partner Center의 예약 이름이 달라지면 `Package.appxmanifest`의 두 DisplayName을 함께 수정하세요.
Identity의 Name과 Publisher는 업데이트 시 유지해야 합니다.

## 빌드

필요 환경: Windows x64, .NET 8 이상을 빌드할 수 있는 .NET SDK, Windows PowerShell 5.1, 최초 복원 시 NuGet 연결.
SDK 도구 버전은 `tools/WindowsSdk.csproj`에 고정되어 있으며 저장소의 `.packages/`에 복원됩니다. 시스템 SDK 설치나 관리자 권한은 필요하지 않습니다.

저장소 루트에서 실행합니다.

```powershell
dotnet test FileRenamer.sln -c Release
dotnet msbuild packaging/FileRenamer.Package.proj /t:Build
```

결과:

```text
artifacts/msix/Release/Dohi.FileRenamerDohi_1.0.0.0_x64.msix
artifacts/msix/Release/Dohi.FileRenamerDohi_1.0.0.0_x64.msix.sha256
```

후속 버전은 더 큰 번호로 빌드합니다. Store용 버전은 `1.0.1.0`처럼 네 부분으로 쓰며 마지막 자리는 `0`입니다.

```powershell
dotnet msbuild packaging/FileRenamer.Package.proj /t:Build /p:PackageVersion=1.0.1.0
```

PowerShell에서 직접 실행할 수도 있습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Build-Msix.ps1 -Version 1.0.1.0 -Architecture x64
```

`-Architecture arm64` 또는 `/p:PackageArchitecture=arm64`로 ARM64 패키지를 따로 생성할 수 있습니다. 해당 기기에서 설치와 실행을 검증한 뒤 제출하세요.
지원 OS는 Windows 10 빌드 19041 이상이며 매니페스트에서 설정합니다. `MaxVersionTested`는 패키지 선언값으로, 모든 해당 OS에서 테스트를 완료했다는 뜻은 아닙니다.

빌드마다 `artifacts/msix/work/<고유 ID>/layout`에 새 배포 디렉터리를 생성합니다.
기존 파일이 섞이지 않도록 하며 MakeAppx의 스키마·의미 검증을 통과한 후 최종 경로에 복사합니다. 로그는 같은 작업 폴더의 `makeappx.log`에 남습니다.
작업 폴더는 문제 진단용으로 보존되므로 필요 없어진 빌드의 폴더는 삭제해도 됩니다.

## 아이콘과 런타임

`Assets/`에는 기존 `src/FileRenamer.App/Assets/app.ico`에서 만든 44×44, 150×150, 50×50 PNG가 있습니다.
원본 아이콘 변경 시 다음 명령으로 재생성하고 패키지를 다시 빌드합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/New-PackageAssets.ps1
```

MSIX 안에는 .NET 및 WPF 런타임을 포함합니다. 단일 파일과 트리밍은 사용하지 않습니다.
WPF 데스크톱 실행을 위해 `runFullTrust` 권한을 선언합니다. 관리자 권한을 요청하는 설정은 아닙니다.

## Store 업로드와 설치 검증

생성된 MSIX는 **서명하지 않은 Store 제출용**입니다. Partner Center는 `.msix` 업로드를 지원하고, 인증 과정에서 Microsoft가 서명합니다. `.msixupload`로 확장자를 바꿀 필요는 없습니다.

1. Partner Center에서 `File Renamer_Dohi`의 제출을 시작합니다.
2. 패키지 항목에 Release `.msix`를 업로드하고 제품 식별자·표시 이름 검증 결과를 확인합니다.
3. 한국어 설명, 실제 화면 캡처, 지원 정보, 가격·지역, 연령 등급 등을 입력합니다.
4. 설치 후 파일 추가, 드래그 앤 드롭, 네 가지 이름 변경 방식, 충돌 검사, 되돌리기, 설정 저장을 검증하고 Windows App Certification Kit 검사를 진행합니다.
5. 검증과 제출 정보 작성이 끝나면 심사를 요청합니다.

로컬 설치 검증에는 Publisher와 Subject가 정확히 같은 코드 서명 인증서로 패키지 **복사본**에 서명하고, 테스트 PC에서 해당 인증서를 신뢰해야 합니다. Store 제출용 원본은 보관하세요.
인증서를 준비한 뒤 Windows SDK의 `signtool.exe sign /fd SHA256`으로 서명하고 `signtool.exe verify /pa`로 확인합니다. 신뢰된 서명 패키지는 `Add-AppxPackage -Path <서명한-MSIX-경로>`로 설치할 수 있습니다.
빌드 스크립트는 인증서를 생성하거나 신뢰 저장소를 변경하거나 앱을 설치하지 않습니다.

패키지 설치 테스트와 WACK 검사는 MSIX 파일 생성 성공만으로 완료되지 않습니다. 기존 EXE 실행과 패키지 설치 후 실행을 구분해 확인하세요.

### 재현 가능한 로컬 테스트

다음 명령은 Release 원본을 보존하고 `artifacts/msix/Test/`에 서명된 복사본, 공개 인증서, 검증용 메타데이터를 만듭니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/New-TestPackage.ps1
```

인증서는 코드 서명 전용이며 7일간 유효합니다. 개인 키는 내보낼 수 없도록 생성하고 서명이 끝나면 사용자 인증서 저장소에서 개인 키와 함께 제거합니다. 따라서 재서명하려면 새 테스트 인증서를 생성해야 합니다.

**관리자 PowerShell**에서 저장소 루트로 이동한 뒤 설치합니다. 이 명령은 테스트 인증서를 `LocalMachine\TrustedPeople`에 추가하고 현재 사용자에게 앱을 설치합니다. 기존 앱이 있으면 덮어쓰지 않고 중단합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Install-TestPackage.ps1
```

설치 기록은 `artifacts/msix/Test/installed.json`, 로그는 `install.log`에 남습니다.
시작 메뉴의 `File Renamer_Dohi`로 실행하여 임시 파일의 변경·되돌리기와 설정 보존을 점검하세요.

WACK가 없다면 [공식 Windows SDK 설치 프로그램](https://learn.microsoft.com/en-us/windows/apps/windows-sdk/downloads)에서 **Windows App Certification Kit** 구성 요소를 설치합니다. 이어서 관리자 PowerShell에서 실행합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Test-Wack.ps1
```

검사 결과는 `artifacts/msix/Wack/<실행 시각-고유 ID>/`에 저장됩니다. `report.xml`은 원본 보고서, `summary.json`은 전체 판정과 항목별 결과, `diagnostics/`는 상세 진단 로그입니다. 검사 중 앱 창이 자동으로 열리고 닫힐 수 있습니다.

한글 사용자 폴더에서 TAEF가 `0x80070459` 오류로 로그를 만들지 못하는 문제를 피하도록, 검사 작업 경로는 `%ProgramData%\FileRenamer\Wack\<실행 시각-고유 ID>`를 사용합니다. WACK의 `-dataFilePath`와 `-temppath` 옵션으로 이 경로를 지정하고 완료 후 결과를 저장소로 복사합니다. 필요하면 스크립트의 `-WorkingRoot`로 다른 영문 절대 경로를 지정할 수 있습니다. 작업 폴더는 진단을 위해 보관됩니다.

WACK 명령의 종료 코드가 `0`이어도 보고서가 `FAIL`일 수 있습니다. 스크립트는 XML의 `OVERALL_RESULT=PASS`, `PARTIAL_RUN=FALSE`, 테스트 항목 존재 여부까지 확인하며, 통과하지 못하면 오류를 반환합니다.

테스트를 마치면 관리자 PowerShell에서 설치한 테스트 앱과 이번에 추가한 인증서 신뢰를 제거합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Install-TestPackage.ps1 -Uninstall
```

이 명령은 테스트 앱의 로컬 설정도 제거합니다. 실제 배포·사용 중인 앱에는 실행하지 마세요. 서명본·공개 인증서·로그는 보관하며 Release 원본에는 영향을 주지 않습니다.

심사 시 `runFullTrust` 사용 목적의 예시:

> 이 앱은 사용자가 선택하거나 끌어다 놓은 로컬 파일의 이름을 일괄 변경하는 WPF 데스크톱 앱입니다. 파일 이름 변경, 충돌 검사 및 되돌리기를 위해 데스크톱 프로세스로 실행됩니다.

## 로컬 검증 기록 (2026-11-10)

Windows 11 빌드 26200에서 x64 `1.0.0.0` 패키지를 테스트 인증서로 서명하고 실제 설치하여 확인했습니다.

| 검사 | 결과 |
| --- | --- |
| 기존 Core 테스트 | 125개 통과 |
| MakeAppx 및 패키지 내용 | 통과; Store 식별자·런타임·로고 확인 |
| 테스트 서명 | 암호학적 검증 및 Windows 신뢰 검증 통과 |
| 설치 결과 | `Status: Ok`, 예상 Package Family Name 일치 |
| 설치된 패키지 실행 | AUMID로 실행 성공 |
| 한글 파일 선택·찾아 바꾸기·되돌리기 | 실제 파일명과 파일 내용 보존 확인 |
| 대소문자만 변경·되돌리기 | 실제 파일명 변경 및 확장자 `.TXT` 유지 확인 |
| 템플릿·순번 미리보기 | 정상 |
| 중복 이름 차단 | 충돌 표시 및 적용 비활성화 확인 |
| 다크 모드·입력값 저장 | 종료 후 재실행 시 유지 확인 |
| 드래그 앤 드롭 | 이번 설치 검사에서는 수동 검증하지 않음 |
| WACK | 도구 시스템 설치 승인 대기; 아직 실행하지 않음 |
| 테스트 앱·인증서 제거 | 관리자 확인 창 취소로 미완료; 2026-11-10 07:40 재확인 시 앱과 임시 신뢰 등록이 남아 있음 |

실행 로그와 상세 결과는 `artifacts/msix/Test/install.log`, `ui-smoke.json`, `verification.json`에 있습니다. WACK를 통과한 상태 또는 Store 인증이 완료된 상태로 간주하지 마세요.

## 인증 검사 후속 기록 (2026-11-13)

- WACK `10.0.26100.9457` 설치를 확인하고 `1.0.0.0`에 전체 검사를 실행했습니다.
- 첫 실행은 한글 사용자 경로에서 TAEF의 로그 생성 오류 `0x80070459`가 발생하여 실제 테스트가 수행되지 않았습니다. 영문 작업 경로를 지정하도록 `Test-Wack.ps1`을 보완한 뒤 전체 검사를 다시 실행했습니다.
- 재검사 보고서: `artifacts/msix/Wack/20261113-001311-77169764/report.xml`. 전체 판정은 `WARNING`, 부분 실행은 `FALSE`입니다. 24개 중 22개 PASS, 선택 검사인 차단된 실행 파일 검사 1개 FAIL, DPI 검사 1개 WARNING입니다.
- 차단된 실행 파일 검사는 포함된 .NET/WPF 런타임 및 앱 호스트의 프로세스 시작 API/문자열 참조를 표시했습니다. `src/`에서 직접 프로세스를 시작하는 코드는 발견되지 않았습니다. Microsoft의 [Desktop Bridge 검사 안내](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-desktop-bridge-app-tests#required-versus-optional-tests)에 따르면 선택 검사는 Store 심사의 필수 통과 판정에 사용되지 않습니다. 이 결과는 기록에 남기며 런타임 파일을 임의로 제거하지 않습니다.
- DPI 경고를 해결하기 위해 `src/FileRenamer.App/app.manifest`에 기존 WPF의 System DPI 동작과 `asInvoker` 실행 권한을 명시했습니다. 실제 배포 EXE에서 매니페스트를 추출하여 포함 여부를 확인했습니다.
- 수정본 `artifacts/msix/Release/Dohi.FileRenamerDohi_1.0.1.0_x64.msix` 생성과 MakeAppx 검증을 완료했고 Core 테스트 125개가 통과했습니다. SHA256은 `7618424e34415959c0d3183a511949bff5f6d23a28ccb05b0af13f26840bc166`입니다.
- 수정본의 테스트 서명 및 메타데이터는 `artifacts/msix/Test-1.0.1.0/`에 별도로 보관했습니다. `New-TestPackage.ps1`과 `Install-TestPackage.ps1`의 `-TestRoot` 옵션으로 버전별 테스트 폴더를 지정할 수 있습니다.
- 최초 관리자 승인 취소 후 사용자가 준비 완료를 알려 1.0.1.0 설치와 전체 검사를 진행했습니다. `20261113-002352-d0498801` 보고서에서도 DPI 분석기 경고가 남았습니다.
- 최종 수정본 **1.0.2.0**에는 `PerMonitorV2`와 `true/pm`을 명시했습니다. MakeAppx 검증 후 기존 앱 데이터를 유지하며 테스트 앱을 업데이트했습니다. 현재 설치된 테스트 앱의 상태는 `Ok`입니다.
- 최종 Store 제출 후보: `artifacts/msix/Release/Dohi.FileRenamerDohi_1.0.2.0_x64.msix`. SHA256: `36c341c5887ed710c52d440cd47002684d12aadff4699929493ef5e9ec0b83c9`. 서명 사본과 비교한 원본 항목 469개가 동일하며 서명도 암호학적으로 검증했습니다.
- 최종 전체 검사: `artifacts/msix/Wack/20261113-003056-f17ce17b/report.xml`, 상세 요약은 같은 폴더의 `summary.json`입니다. `PARTIAL_RUN=FALSE`, `OVERALL_RESULT=WARNING`; 필수 13개 중 12개 PASS와 DPI WARNING 1개, 선택 11개 중 10개 PASS와 차단된 실행 파일 FAIL 1개입니다. 엄격한 `Test-Wack.ps1`은 WARNING도 오류로 반환하므로 실행 래퍼의 상태는 `Failed`로 남습니다. 검사 미실행을 뜻하지 않습니다.
- DPI 분석기는 설치 경로의 EXE에서 `IsPerMonitorV2DPIAwarenessSet()` 호출 시 `HRESULT E_FAIL`을 반환했습니다. SHA256이 동일한 EXE를 작업 폴더 및 공백이 있는 일반 폴더로 복사하면 같은 분석기가 `true`를 반환했습니다. 설치 경로에 따른 도구 분석 실패임을 재현했으며 세부 원인까지 확정한 것은 아닙니다. 원본 보고서의 WARNING 판정은 변경하지 않았습니다.
- 실제 설치 앱의 시작 및 응답성, 프로세스의 per-monitor DPI 상태, 메인 창의 `PerMonitorV2` 컨텍스트를 확인했습니다: `artifacts/msix/Test-1.0.2.0/startup-dpi.json`. 다중 모니터 간 이동 시의 시각적 검증과 드래그 앤 드롭은 이번 검사 범위에 포함하지 않았습니다.
- 검사용 서명 사본·설치 영수증·확인 기록은 `artifacts/msix/Test-1.0.2.0/`에 있습니다. 테스트 앱과 임시 신뢰 인증서는 재검사를 위해 유지합니다. 이전 버전 인증서도 남아 있으므로 후속 정리 시 각 버전의 설치 영수증을 확인하세요. 아직 Store 인증이 완료된 상태로 간주하지 마세요.

## 공식 참고 문서

- [명령줄로 MSIX 구성 및 생성](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion)
- [MakeAppx 사용법](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool)
- [앱 패키지 요구 사항](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [테스트 인증서로 패키지 서명](https://learn.microsoft.com/en-us/windows/msix/package/sign-app-package-using-signtool)
