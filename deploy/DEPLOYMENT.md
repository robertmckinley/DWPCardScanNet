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

### Install the site configuration (required, manual)

The package ships a **fixture** `DWPCardScan.ini` next to the exe so the app can start,
but it is **not** a site's real config. After copying, replace it with the site config:

1. Copy the site's real `DWPCardScan.ini` into the install folder, overwriting the shipped
   fixture (e.g. `C:\MugConfig\DWPCardScanNet\DWPCardScan.ini`). If the site kept its config
   at `C:\MugConfig\DWPCardScan\DWPCardScan.ini`, copy that file here.
2. The app reads `DWPCardScan.ini` **only** from this folder - there is no shared/hardcoded
   fallback. If it is missing, the app shows an error and will not start.
3. Verify every folder the .ini references (`NISTOutDir`, `GroupPDFFolder`, `LogFileDir`,
   `ToProcessQueue`, `GroupsToProcessFolder`, `GroupStatusReportFolder`) exists and is
   writable on this machine - paths are used exactly as written, with no drive remapping.

## Target prerequisites

1. **.NET Framework 4.8** runtime.
2. **Aware AccuScan + NISTPack** installed at the standard path
   `C:\Program Files (x86)\Aware\...`. These provide the native libraries
   (`accuscan.dll`, `nistpack.dll`, `wsq1000.dll`); the app adds the Aware
   `NISTPack\redist` and `AccuScan\redist` folders to its DLL search path at startup.
   The app is **x86** and must run 32-bit (the Aware assemblies are x86).
   **Requires Aware AccuScan version 4.2.1** — the test machine threw errors until
   this version was installed; earlier versions are not supported.
3. **Site configuration** `DWPCardScan.ini` (card/group definitions, folders, banner,
   etc.) **in the application folder, next to `DWPCardScanNet.exe`**. This is the only
   location checked - there is no shared/hardcoded fallback. If the file is missing the
   app shows an error and does not start. Replace the shipped fixture `DWPCardScan.ini`
   with the real site config at install time.
4. The external scanner app referenced by `ScanShellFilePath` in the .ini
   (e.g. `DWPAwareScanMB.exe`), if the site scans via the external scanner.

## Custom post-process plugins (PostProcessDLL)

Sites can run a custom DLL after each group's packet is written (site-specific export, DB
insert, notification, cleanup). This is a **managed plugin** - no COM registration. Configure
per group in the .ini:

```
PostProcessDLL    = C:\Plugins\MyPostProcessor.dll
PostProcessDLLcfg = C:\Plugins\MyPostProcessor.cfg
```

The DLL implements `DWPCardScanNet.Core.Processing.IPostProcessor`. See
`samples\SamplePostProcessor\README.md` for the contract and a reference implementation.

## Configuration assumptions (current build)

- Config is loaded only from `DWPCardScan.ini` in the application folder (next to the
  exe). There is no fallback location; a missing file is a startup error.
- **All paths in the .ini are honoured exactly as written** - there is no drive
  remapping. Every folder the app reads or writes (`NISTOutDir`, `GroupPDFFolder`,
  `LogFileDir`, `ToProcessQueue`, `GroupsToProcessFolder`, `GroupStatusReportFolder`)
  must exist and be writable on the target machine, at the drive/path configured.
  At startup the app errors if `LogFileDir` is missing and warns if an output folder
  (`NISTOutDir`, `GroupPDFFolder`) is missing, so misconfiguration surfaces immediately.

## Uninstall

Delete the target folder and the Desktop shortcut (`%USERPROFILE%\Desktop\DWP Card Scan.lnk`).
No registry entries are created.
