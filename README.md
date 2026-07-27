# DWPCardScanNet

A ground-up migration of the VB6 **DWPCardScan** fingerprint/palm-card scanning
application to **.NET Framework 4.8 (C#, WinForms, x86)**.

The original VB6 project at `C:\DWP\DEV\MUG\DWPCardScan` is **not modified** by this
work; this is a new, parallel implementation.

## Status

Completed increments:

1. **Foundation** — `DWPCardScanNet.Core` class library (UI/COM-free) + a WinForms shell.
   - Data model: `Defaults`, `CardDefinition`, `GroupDefinition`, `CropRegion`
     (ports of `clsDefaults`/`clsCardDef`/`clsGroupDef`/`clsCropRegion`).
   - `IniFile` (Win32 private-profile API) + `ConfigReader` (port of `clsConfig`).
2. **Logging** — `Core.Logging`: `ILog`, `NullLog`, `FileLog` (port of `WriteToLog`,
   with safe deviations: never throws, creates its directory, lock-serialized).
3. **Aware integration (fully managed, no COM)** — `DWPCardScanNet.Aware`:
   - `IScanner` (Core contract) + `AwareScanner` — port of `clsDWPAwareScan_Net`
     scanning, on the managed `Aware.AccuScan.awAccuScan` API.
   - `AwareCapabilities.Probe()` — a diagnostic that instantiates all three Aware
     managed libraries; verified at runtime (AccuScan 4.2.1, NistPack 5.15, WSQ).
4. **Crop → WSQ → NIST pipeline (ported, NOT yet validated)** — port of
   `ParseScannedCard` + `cmdProcessGroup` + `AddType4/15/7Rec`:
   - `Core.Processing`: `RawImageRotation` (pure), the pipeline interfaces
     (`IWsqCompressor`/`INistTransaction`/`ICardImageExtractor`/`IGroupProcessor`), and
     `GroupProcessor` — all record-building logic, testable via fakes.
   - `Aware`: `AwareWsqCompressor`, `AwareNistTransaction`, `AwareCardImageExtractor`,
     and `BmpCodec` (GDI+ 8-bpp decode + whole-card rotate — the LEAD `RotateBMP` replacement).
   - Runtime-verified: WSQ emits valid WSQ (FF A0, ~10:1); a NIST transaction reads the real
     verification file and writes a valid `.eft`. **The full group path is NOT yet validated
     against real card data — see below.**

## Key migration decisions

- **Language:** C#. **Runtime:** .NET Framework 4.8, WinForms, **x86**.
- **No COM.** Aware ships managed assemblies, so we use them directly instead of the
  VB6 COM controls — no `tlbimp`, no `regsvr32`, no interop DLLs:
  | VB6 COM control | Managed replacement |
  |---|---|
  | `AwScanControl` | `Aware.AccuScan` (`Aware.AccuScan.dll`) |
  | `AwTransCtrl` | `Aware.NistPack` (`Aware.NistPack.dll`) |
  | `AwWsqCtrl` | `Aware.AwWsq1000` (`Aware.AwWsq1000.dll`) |
- **x86 is mandatory** — all three Aware managed assemblies are x86.
- **LEADTOOLS is eliminated** — the 4 live uses will be rebuilt on `System.Drawing`/GDI+
  (the scanner's thumbnailing already is).

## Aware dependency & deployment

Only `DWPCardScanNet.Aware` references the vendor assemblies; Core and the UI stay clean.
References resolve via the `AwareAccuScanDir` / `AwareNistPackDir` MSBuild properties
(default: the standard `C:\Program Files (x86)\Aware\...\redist` install paths — override
on build machines with a different install).

**Runtime note:** the managed assemblies load native DLLs (`accuscan.dll`, `nistpack.dll`,
`wsq1000.dll`). `accuscan.dll` is in SysWOW64, but the NISTPack natives are not — so the
app must run with the Aware `NISTPack\redist` folder discoverable (on PATH, or the natives
deployed alongside). `AwareCapabilities.Probe()` is the sanity check for this.

## Pipeline validation status: VALIDATED against the legacy output

Confirmed by diffing against golden `.eft` packets produced by the legacy `DWPCardScan.exe`
(tooling: `tools/DWPCardScanNet.PipelineHarness`, which builds groups from real card BMPs, runs
`GroupProcessor`, and field/image-diffs against a golden via `AwareNistDiff`):

- **Structure, every NIST field, all crop dimensions, and the WSQ setting match the legacy output.**
- Resolved during validation: WSQ param is a **quality factor** (`SetCompressWsqFactor(10)`, not a
  ratio); `T4_VLL/HLL` are emitted as `0` to match VB6; `T2_PRC` uses VB6's locale datetime format.
- **Known, accepted deviation:** a constant **~2 px (0.004") horizontal crop offset** vs the legacy
  output, because this port uses the managed `awAccuScan` engine while VB6 used the legacy
  `AwScanControl` COM engine. Biometrically negligible (AFIS minutiae matching tolerates far more);
  accepted rather than reintroducing COM. Revisit only if the receiving AFIS ever flags it.
- Per-group error handling is intentionally more robust than VB6 (one bad group is logged and
  skipped; the run continues, rather than aborting the whole batch).

## Main window (WinForms)

The `frmMain` port is functional and starts cleanly. `MainForm` + `CardPreviewControl` (GDI+ card
display with read-only crop-region overlays) + `ZoomForm` (GDI+ viewer) + `Core.Grouping.GroupingService`.
Wires config load, the to-process thumbnail queue, card-type → overlays/key prompt, add-to-group,
Process → `GroupProcessor`, Scan → external `ScanShellFilePath` app, and double-click → zoom.
**This completes the LEADTOOLS elimination** — all four legacy LEAD uses are now GDI+.

Config is loaded from `DWPCardScan.ini` in the application folder (next to the exe) only - no
shared/hardcoded location and no fallback; a missing file is a startup error. Paths in the .ini
are used exactly as written (no drive remapping). The package ships a `DWPCardScan_Sample.ini`
template, which is never loaded - the real `DWPCardScan.ini` must be created at install time.

## Interactive crop-region editing

Operators can drag the crop rectangles in `CardPreviewControl` to reposition them (move-only, fixed
size, matching the VB6 `CardScanManualAx`). On "Add Card to Group" the adjusted positions are written
to a `.crp` file (`Core.Grouping.CropRegionFile`, format `FingerIndex,Desc,Top,Left,Height,Width`),
which `GroupProcessor` reads to override each region's Top/Left. Verified end-to-end: a `.crp`
shifting one finger moves exactly that finger's extracted crop and leaves the others untouched.

This makes the **core VB6 workflow feature-complete** in the port: scan launch, to-process queue,
card preview with crop editing, grouping, NIST processing, and zoom.

## Not yet started

The in-process scan dialog (`AwareScanner` UI, for when `ScanShellFilePath` is not configured), the
undo journal, the group-conflict duplicate-group spin-off, the group status-report window, PDF output
(MUGPDF), the `PostProcessDLL` hook, and installer/deployment.

## Build

```
dotnet build DWPCardScanNet.sln
```

## Layout

```
DWPCardScanNet.sln
src/
  DWPCardScanNet.Core/     class library: model + INI config + logging + scan contracts
  DWPCardScanNet.Aware/    Aware managed integration (x86) — scanning; NIST/WSQ next
  DWPCardScanNet/          WinForms exe (net48, x86)
```
