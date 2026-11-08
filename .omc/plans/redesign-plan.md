# Redesign Plan: 최종안 (A 기반 + B 규칙 패널·변경 강조 + D 폴더 열, 일반/다크 모드)

Status: approved by user 2026-10-23. Lead = plan & verify; **designer** = XAML visuals; **WPF dev** = C# wiring.
Work happens in this worktree (`develop-2`). Do **not** commit — the lead integrates.

## Design reference (source of truth)
- Canvas: https://claude.ai/artifact/6DYyNsPmt2UnmDmFCKhc4X (artboards "최종안 – 일반 모드 / 다크 모드 / 설정 창")
- Local copies (read these): scratchpad `design/project/Final.dc.html` (main window, `theme` light|dark — the color tokens in its `renderVals()` are the palette) and `design/project/Settings.dc.html`.
  Full path prefix: `C:\Users\최경태\AppData\Local\Temp\claude\C--Users-----orca-workspaces-file-renamer-tool-develop-2\323fa4c7-8f70-4d60-bf86-b5cd1770e99b\scratchpad\`

Layout summary:
- Header: app title (left), 설정 button (right).
- Left panel (card, ~320px): 변경 방식 2×2 segmented radio → inputs of the selected mode → rule error (red) → bottom: settings summary (날짜 sample, 회사명, 이름, 화면 모드) + 편집 link opening settings.
  - 찾아 바꾸기: 찾을 글자, 바꿀 글자 (+ hint "비워 두면 찾은 글자를 지웁니다."), 정규식 사용 checkbox.
  - 템플릿: template TextBox + variable chips ({날짜} {회사명} {이름} {파일명}, each showing its current value muted) that insert at the caret.
  - 대소문자: 4 option radios (전체 소문자 / 전체 대문자 / 첫 글자만 대문자 / 단어마다 첫 글자 대문자).
  - 순번: 기본 이름, 시작 번호 + 자릿수, 구분자 4-way segmented (공백 / _ / - / 없음).
- Right card: toolbar (파일 추가 accent, 선택 삭제, 전체 비우기, ▲ ▼ icon buttons, then count chips 변경 N / 그대로 N / 충돌 N) → DataGrid columns `# | 현재 이름 (diff) | → | 새 이름 (diff) | 상태 chip | 폴더 (mono, ellipsis, tooltip full path)`. Empty-state hint "여기에 파일이나 폴더를 끌어다 놓으세요" overlay. Row drag reorder kept.
- Footer: conflict icon + ConflictMessage (only when present), 되돌리기, Accent apply button showing ApplyText.
- Settings window: 화면 모드 (two radio cards with a mini preview: 일반 모드 / 다크 모드), 날짜 형식 + live sample, 회사명 / 이름 side by side, 확인 / 취소.
- No ComboBox anywhere (dark theme); use radios/segments.

## Already in place (lead draft — owners below may revise)
- Core: `AppTheme { Light, Dark }`, `AppSettings.Theme`, `NameDiff.Compute(old,new) -> (Old, New)` of `DiffSegment(Text, DiffKind Same|Removed|Added)`; tests pass. **Core is done; don't change its API.**
- `Themes/Light.xaml`, `Themes/Dark.xaml` (brush keys below), `Themes/Styles.xaml` (styles below), `App.xaml` merges [0]=theme, [1]=Styles.
- `App.ApplyTheme(AppTheme)` swaps dictionary 0 and sets the dark title bar; `App.ApplyTitleBar(Window)` for new windows (call on SourceInitialized).
- `IndexConverter` (int == ConverterParameter → bool or Visibility, two-way for radios), `DiffText.Segments` attached property on TextBlock (draws strike/added highlight with theme brushes).
- `FileRow`: Path, CurrentName, Folder, Index, NewName, StatusText, Kind (`RowKind None|Changed|Unchanged|Conflict`), IsConflict, OldSegments, NewSegments.

Brush keys: BgBrush SurfaceBrush LineBrush TextBrush MutedBrush AccentBrush AccentHoverBrush OnAccentBrush LinkBrush InputBgBrush InputBorderBrush SegBgBrush HoverBrush PressedBrush DelBgBrush DelFgBrush AddBgBrush AddFgBrush OkBgBrush OkFgBrush SameBgBrush SameFgBrush BadBgBrush BadFgBrush RowBadBrush RowBadSelBrush RowSelBrush DisabledBgBrush DisabledFgBrush ScrollThumbBrush.
Style keys: WindowStyle Card FieldLabel Muted GhostButton IconButton AccentButton LinkButton ChipButton SegmentRadio SegmentGroup OptionRadio Chip UiFont MonoFont; implicit Button TextBox CheckBox ScrollBar DataGrid DataGridColumnHeader DataGridCell. All colors via DynamicResource.

## File ownership (don't edit the other owner's files; ask the lead via your report if the contract needs to change)
- **designer**: `Themes/Light.xaml`, `Themes/Dark.xaml`, `Themes/Styles.xaml`, `MainWindow.xaml`, `SettingsWindow.xaml`.
- **WPF dev**: `MainViewModel.cs`, `MainWindow.xaml.cs`, `SettingsWindow.xaml.cs`, `App.xaml`, `App.xaml.cs`, `FileRow.cs`, `Converters.cs`, `DiffText.cs`, `RelayCommand.cs`, csproj, README.md (UI section).

## Binding contract (both sides code against exactly these names)
MainWindow DataContext = `MainViewModel`:
- Collections/rows: `Rows` (FileRow above), `HasNoRows`, `HasRows`.
- Inputs: `ModeIndex` (0 찾아 바꾸기, 1 템플릿, 2 대소문자, 3 순번), `ReplaceFind`, `ReplaceWith`, `UseRegex`, `Template`, `CaseIndex` (0..3), `SeqBaseName`, `SeqStart`, `SeqDigits`, `SeqSepIndex` (0 공백, 1 _, 2 -, 3 없음).
- Output: `ErrorMessage` (rule error), `HasError`, `ChangedCount`, `UnchangedCount`, `ConflictCount`, `ConflictMessage` (e.g. "제안서_v2.pptx — 같은 폴더에 같은 이름의 파일이 있습니다" + " 외 N건"), `HasConflictMessage`, `ApplyText` ("N개 이름 바꾸기" when N>0, else "적용").
- Settings summary: `DateSample` (today in DateFormat), `CompanyName`, `UserName`, `ThemeName` ("일반 모드"/"다크 모드").
- Commands: `RemoveSelectedCommand` (param = FileGrid.SelectedItems), `ClearCommand`, `MoveUpCommand` / `MoveDownCommand` (param = FileGrid.SelectedItem), `ApplyCommand`, `UndoCommand`.
- Status text per row: 변경 / 그대로 / 이름 중복 / 이미 있는 이름 / 잘못된 이름.

Named elements & handlers in MainWindow.xaml (code-behind provides them):
- `x:Name="FileGrid"` DataGrid with `PreviewMouseLeftButtonDown="OnGridMouseDown"`, `PreviewMouseMove="OnGridMouseMove"`, `AllowDrop`, `DragOver="OnDragOver"`, `Drop="OnDrop"`; Window `AllowDrop`, `DragOver`, `Drop`, `Closing="OnClosing"`, `SourceInitialized="OnSourceInitialized"`.
- `Click="OnAddFiles"` (파일 추가), `Click="OnSettings"` (설정 button and 편집 link).
- `x:Name="TemplateBox"` TextBox; variable chips `Click="OnInsertVariable"` with `Tag="{날짜}"` etc. (escape braces in XAML: `Tag="{}{날짜}"`).

SettingsWindow named elements: `DateFormatBox` (TextChanged="OnDateFormatChanged"), `SampleText`, `CompanyBox`, `UserBox`, `LightThemeRadio`, `DarkThemeRadio`, `OkButton` (Click="OnOk", IsDefault), cancel `IsCancel`; Window `SourceInitialized="OnSourceInitialized"`. Theme applies on 확인 (via `MainViewModel.UpdateSettings` → `App.ApplyTheme`) and is saved to settings.json; on startup the saved theme is applied before the main window shows.

## Done
- `dotnet build FileRenamer.sln` 0 errors/warnings introduced; `dotnet test` all green.
- Both themes switch live from settings, persist across restart; diff highlight, folder column, chips, conflict footer, apply text all work.

## Status (2026-10-23, lead wrap-up)
Workers are the Orca Codex sessions, reached with `orca terminal send/show/wait` (see `orca terminal list`). All edits are uncommitted in the `develop-2` worktree.
- [x] Core: AppTheme, NameDiff + tests (lead draft).
- [x] WPF session: C# wiring done. Changed MainViewModel.cs, MainWindow.xaml.cs, SettingsWindow.xaml.cs, App.xaml.cs, FileRow.cs, README.md. Build 0 errors/warnings, 122 tests pass, 32 windowless contract checks pass. No visual check.
- [x] Design session: XAML done. Changed Themes/Light.xaml, Dark.xaml, Styles.xaml, MainWindow.xaml, SettingsWindow.xaml. All 25 palette tokens match in both themes, every color is a DynamicResource, no ComboBox, contract names and handlers kept. Checked 12 offscreen renders. Build 0/0, 122 tests pass.
  - Open contract request: add `FileRow.FileNameStem` (name without extension) so the {파일명} chip shows the real value. It shows CurrentName for now. Assign to the WPF session (FileRow.cs) plus the designer (chip binding) if wanted.
- [x] QA session (2026-11-07): first pass FAIL (silent settings-save failure; {파일명} chip showed CurrentName). Fixed: MainViewModel warns on save failure from 설정 확인; FileRow.FileNameStem = FileNameSplitter.Split(CurrentName).Stem, chip bound to it. Final PASS: build 0/0, 122/122.
- [x] Git session: pushed origin/develop 5783828..2ec8828 (45e4eb6 feat(core), 78e2433 test, 039ec29 feat(app), 2ec8828 docs). Original instruction: after QA passes, send "git 작업 시작" with the file list. It copies the files from develop-2, commits by hand in Conventional Commits (Korean, split by type, no AI attribution) and pushes `origin develop`.
