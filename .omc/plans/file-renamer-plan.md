# Implementation Plan: File Renamer (WPF)

Source spec: `.omc/specs/deep-interview-file-renamer.md` (acceptance criteria AC 1–18 are the contract).
Status: approved by user 2026-10-23 — roles: lead = plan & supervise, WPF dev agent = implement, QA agent = verify.

## Environment
- .NET SDKs installed: 9.0.308, 10.0.101 (no 8 SDK). Target `net8.0-windows` (Core: `net8.0`). If the net8 targeting pack cannot be restored, fall back to `net9.0[-windows]` and record it in the report.
- Solution file `FileRenamer.sln` at repo root.

## Layout
```
FileRenamer.sln
src/FileRenamer.Core/          (net8.0, no UI deps)
src/FileRenamer.App/           (net8.0-windows, UseWPF, references Core)
tests/FileRenamer.Core.Tests/  (xUnit, references Core)
README.md                      (Korean: usage + build/publish command)
.gitignore                     (bin/ obj/ publish/)
```

## Core API contract (QA tests against this — do not rename without updating plan)
Namespace `FileRenamer.Core`.

```csharp
public enum CaseStyle { Lower, Upper, FirstUpper, TitleCase }
public enum SequenceSeparator { Space, Underscore, Hyphen, None }

public sealed record AppSettings {
  string DateFormat = "yyyyMMdd"; string CompanyName = ""; string UserName = "";
  // last-used inputs per mode (template string, find/replace text, regex flag, case style, sequence options, selected mode)
}

public interface IRenameMode {
  // stems = file names WITHOUT extension, in list order. Returns new stems (same count/order)
  // or throws RenameRuleException(message) for invalid input (bad regex, unknown {var}).
  IReadOnlyList<string> Apply(IReadOnlyList<string> stems);
}
public sealed class ReplaceMode(string find, string replace, bool useRegex) : IRenameMode
public sealed class TemplateMode(string template, AppSettings settings, DateTime today) : IRenameMode
   // vars: {날짜} {회사명} {파일명} {이름}; unknown {x} -> RenameRuleException
public sealed class CaseMode(CaseStyle style) : IRenameMode
   // TitleCase word separators: space, '_', '-'. Non-cased chars (Hangul) untouched.
public sealed class SequenceMode(string baseName, int start, int digits, SequenceSeparator sep) : IRenameMode

public static class FileNameSplitter { (string Stem, string Ext) Split(string fileName); } // ext incl. dot, last '.'; ".gitignore" -> stem ".gitignore", ext ""

public enum PlanStatus { Ok, Unchanged, DuplicateInBatch, ExistsOnDisk, InvalidName }
public sealed record RenamePlanItem(string SourcePath, string NewName, PlanStatus Status);
public static class RenamePlanner {
  // Builds full plan: splits ext, applies mode to stems, re-attaches original ext,
  // detects conflicts case-insensitively (in-batch duplicates; existing files in same dir that are NOT part of the batch;
  // invalid chars \/:*?"<>| , empty, trailing '.'/space, reserved names CON/PRN/AUX/NUL/COM1-9/LPT1-9).
  // fileExists injected for testability.
  IReadOnlyList<RenamePlanItem> Build(IReadOnlyList<string> paths, IRenameMode mode, Func<string,bool> fileExists);
  bool CanApply(IReadOnlyList<RenamePlanItem> plan); // true iff no conflict/invalid and >=1 Ok
}

public sealed record RenameFailure(string Path, string Reason);
public sealed record RenameResult(IReadOnlyList<(string From, string To)> Succeeded, IReadOnlyList<RenameFailure> Failed);
public interface IFileSystem { void Move(string from, string to); bool Exists(string path); }
public static class RenameExecutor {
  // Two-phase: move every Ok item to a unique temp name in same dir, then temp -> final.
  // Handles swaps (A->B, B->A) and case-only renames. Per-item failure is recorded; others continue;
  // if phase 2 fails, try to restore the original name.
  RenameResult Execute(IReadOnlyList<RenamePlanItem> plan, IFileSystem fs);
  RenameResult Undo(RenameResult last, IFileSystem fs); // reverses Succeeded (To -> From), same two-phase approach
}

public static class FolderExpander { IReadOnlyList<string> Expand(IEnumerable<string> droppedPaths); } // dirs -> top-level files only, dedupe (case-insensitive), keep order
public sealed class SettingsStore(string path) { AppSettings Load(); void Save(AppSettings s); } // default %AppData%\FileRenamer\settings.json; missing/corrupt -> defaults
```

## App (WPF) — MVVM, no third-party MVVM lib required (CommunityToolkit.Mvvm allowed)
- MainWindow: file DataGrid (현재 이름 | 새 이름 | 상태), red row for conflicts; AllowDrop for files/folders; buttons 파일 추가 / 선택 삭제 / 전체 비우기 / 위 / 아래 (+ drag reorder of rows).
- Mode selector (TabControl): 찾아 바꾸기 | 템플릿 | 대소문자 | 순번. Live preview on every input change. Rule error shown under input.
- 적용 (enabled iff CanApply), 되돌리기 (enabled iff last result has successes). Failures dialog after apply/undo. After apply, list paths updated to new names.
- SettingsWindow: 날짜 형식 (+ live sample), 회사명, 이름. Persist settings + last inputs on change/close.
- All UI text Korean.

## Steps
1. Scaffold sln/projects/.gitignore; `dotnet build` green.
2. Core: splitter, 4 modes, planner, executor/undo, folder expander, settings store.
3. Core unit tests covering every spec example (AC 5,6,8,10,11) + conflicts + swap + undo (fake IFileSystem and real temp dir).
4. WPF App wiring.
5. README + publish command: `dotnet publish src/FileRenamer.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish`.
6. QA phase (separate agent): build, run tests, add missing tests, code review vs AC 1–18, publish exe smoke check, report defects.

## Done = all of
- `dotnet build FileRenamer.sln` 0 errors; `dotnet test` all green.
- Publish produces single `FileRenamer.App.exe`.
- QA report maps every AC 1–18 to PASS/FAIL with evidence; lead fixes loop until all PASS (UI-only ACs may be PASS-by-review where not automatable — stated explicitly).
