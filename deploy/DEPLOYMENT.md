# DWPCardScanNet - Deployment (xcopy)

Simple file-copy deployment - no MSI/installer engine. Two scripts:

- **`package.cmd`** - run on a build machine (with the .NET SDK). Builds the Release
  app and stages everything it needs into `deploy\dist\DWPCardScanNet\`.
- **`install.cmd [targetDir]`** - run on the target machine (with the `dist` folder
  present). Xcopies the app to `targetDir` (default `C:\MugConfig\DWPCardScanNet`) and
  creates a Desktop shortcut.

## Build & package (build machine)

```
deploy\package.cmd
```

Produces `deploy\dist\DWPCardScanNet\` containing `DWPCardScanNet.exe`, the
`DWPCardScanNet.Core` / `DWPCardScanNet.Aware` assemblies, the managed Aware
wrappers (`Aware.AccuScan.dll`, `Aware.NistPack.dll`, `Aware.AwWsq1000.dll`), and
`PdfSharp.dll` (MIT-licensed; the managed replacement for the VB6 MUGPDF component,
used to archive scanned groups as PDF when `SaveGroupPDF = Y`).

## Install (target machine)

Copy the `deploy` folder (or just `deploy\dist` + `install.cmd`) to the target, then:

```
install.cmd
```
or to a specific location:
```
install.cmd "C:\Apps\DWPCardScanNet"
```

## Target prerequisites

1. **.NET Framework 4.8** runtime.
2. **Aware AccuScan + NISTPack** installed at the standard path
   `C:\Program Files (x86)\Aware\...`. These provide the native libraries
   (`accuscan.dll`, `nistpack.dll`, `wsq1000.dll`); the app adds the Aware
   `NISTPack\redist` and `AccuScan\redist` folders to its DLL search path at startup.
   The app is **x86** and must run 32-bit (the Aware assemblies are x86).
3. **Site configuration** at `C:\MugConfig\DWPCardScan\DWPCardScan.ini`
   (card/group definitions, folders, banner, etc.). The app reads this when present,
   otherwise falls back to the `DWPCardScan.ini` shipped next to the exe.
4. The external scanner app referenced by `ScanShellFilePath` in the .ini
   (e.g. `DWPAwareScanMB.exe`), if the site scans via the external scanner.

## Configuration assumptions (current build)

- Config is loaded from `C:\MugConfig\DWPCardScan\DWPCardScan.ini` (fallback: the
  copy beside the exe).
- Paths beginning `D:\` in the .ini (e.g. `NISTOutDir`, PDF folder) are remapped to
  `C:\MugConfig\DWPCardScan\` - a convenience for the current test box. Sites that use
  real `D:\` paths should have those drives present, or this remap can be removed.

These assumptions live in `MainForm` (`MugConfigIni`, `DriveRemapTo`); make them
configurable before a wider rollout if sites differ.

## Uninstall

Delete the target folder and the Desktop shortcut (`%USERPROFILE%\Desktop\DWP Card Scan.lnk`).
No registry entries are created.
