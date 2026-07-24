# DWPCardScanNet post-process plugins

The **PostProcessDLL** hook lets a site drop in a custom DLL that runs after each card
group's NIST packet is written - the managed replacement for the old VB6 `PostProcessDLL`
COM object. **No COM registration is required.**

## Writing a plugin

1. Create a .NET Framework **4.8**, **x86** class library (match the host).
2. Reference `DWPCardScanNet.Core` - either the project (as this sample does) or the shipped
   `DWPCardScanNet.Core.dll`.
3. Add a `public` class with a public parameterless constructor that implements
   `DWPCardScanNet.Core.Processing.IPostProcessor`:

   ```csharp
   public sealed class MyPostProcessor : IPostProcessor
   {
       public PostProcessResult PostProcess(PostProcessContext ctx)
       {
           // ctx.NistPacketPath   - the .eft that was just written
           // ctx.KeyValue         - the group key (folder name)
           // ctx.TransactionType  - the group TOT
           // ctx.VerificationFile - the group verification/template file
           // ctx.ConfigFileName   - your PostProcessDLLcfg value
           // ctx.StatusReportPath - the run's status report (may be null)
           //
           // ... do the site-specific work ...
           return PostProcessResult.Ok();               // success; host may remove the group folder
           // return PostProcessResult.Ok(processComplete: false); // success, but keep the group folder
           // return PostProcessResult.Fail("reason");  // failure; group recorded failed, folder kept
       }
   }
   ```

4. Build, then point the group at the DLL in `DWPCardScan.ini`:

   ```
   PostProcessDLL    = C:\Plugins\MyPostProcessor.dll
   PostProcessDLLcfg = C:\Plugins\MyPostProcessor.cfg
   ```

## PostProcessDLL spec forms

| Spec in the .ini                                  | Meaning                                                        |
|---------------------------------------------------|----------------------------------------------------------------|
| `C:\Plugins\MyPlugin.dll`                         | Load the DLL, use its single `IPostProcessor` implementation.  |
| `C:\Plugins\MyPlugin.dll!My.Namespace.MyType`     | Load the DLL, use the named type (for DLLs with more than one).|
| `My.Namespace.MyType, MyAssemblyName`             | Resolve by assembly-qualified type name (DLL alongside the app).|

Relative assembly paths resolve against the application folder. If a `PostProcessDLL` is
configured but cannot be resolved, or the plugin reports failure, that group is recorded as
failed and its folder is left in place; other groups continue.

## Return semantics (parity with VB6)

- **`Success = true`, `ProcessComplete = true`** - host removes the processed group folder
  (when the run is configured to delete groups).
- **`Success = true`, `ProcessComplete = false`** - group folder is kept (deferred work).
- **`Success = false`** - `LastError` is logged, the group is recorded failed, folder kept.

## Build this sample

```
dotnet build samples\SamplePostProcessor\SamplePostProcessor.csproj -c Release
```

Produces `SamplePostProcessor.dll`, which writes a `<packet>.postprocessed.txt` marker next to
the NIST packet. This project is intentionally **not** part of `DWPCardScanNet.sln`.
