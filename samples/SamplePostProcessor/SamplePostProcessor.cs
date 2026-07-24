using System;
using System.IO;
using DWPCardScanNet.Core.Processing;

namespace SamplePostProcessor
{
    /// <summary>
    /// Reference implementation of a DWPCardScanNet post-process plugin (the managed replacement for
    /// the old VB6 PostProcessDLL COM hook). Customers copy this project as a starting point.
    ///
    /// Contract:
    ///   * Implement <see cref="IPostProcessor"/> in a public class with a public parameterless ctor.
    ///   * Reference DWPCardScanNet.Core (this project references it; a customer can reference either the
    ///     Core project or the shipped DWPCardScanNet.Core.dll).
    ///   * Configure the group in DWPCardScan.ini:
    ///         PostProcessDLL    = C:\Plugins\SamplePostProcessor.dll
    ///         PostProcessDLLcfg = C:\Plugins\SamplePostProcessor.cfg
    ///   * Build for x86 / .NET Framework 4.8 (to match the host) and drop the DLL where PostProcessDLL points.
    ///
    /// This sample simply writes a small marker file next to the NIST packet recording what it received.
    /// Replace the body with the real site-specific work (DB insert, export, notification, etc.).
    /// </summary>
    public sealed class SamplePostProcessor : IPostProcessor
    {
        public PostProcessResult PostProcess(PostProcessContext context)
        {
            try
            {
                string dir = Path.GetDirectoryName(context.NistPacketPath) ?? ".";
                string marker = Path.Combine(dir,
                    Path.GetFileNameWithoutExtension(context.NistPacketPath) + ".postprocessed.txt");

                File.WriteAllText(marker,
                    "Post-processed by SamplePostProcessor" + Environment.NewLine +
                    "Key           : " + context.KeyValue + Environment.NewLine +
                    "NIST packet   : " + context.NistPacketPath + Environment.NewLine +
                    "TransactionTOT: " + context.TransactionType + Environment.NewLine +
                    "Verification  : " + context.VerificationFile + Environment.NewLine +
                    "Config file   : " + context.ConfigFileName + Environment.NewLine +
                    "Status report : " + context.StatusReportPath + Environment.NewLine);

                // Success. ProcessComplete = true lets the host remove the processed group folder
                // (return Ok(processComplete: false) to keep the group for deferred work).
                return PostProcessResult.Ok(processComplete: true);
            }
            catch (Exception ex)
            {
                // A failure is reported back to the host, which records the group as failed and
                // leaves its folder in place. Mirrors the VB6 iRet<>0 / LastError behaviour.
                return PostProcessResult.Fail(ex.Message);
            }
        }
    }
}
