using System;
using System.Text;
using Aware.AccuScan;
using Aware.AwWsq1000;
using Aware.NistPack;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// Diagnostic probe that touches all three managed Aware libraries. It serves two purposes:
    /// (1) it forces a compile-time binding against Aware.AccuScan / Aware.NistPack / Aware.AwWsq1000,
    /// so a successful build proves the references resolve; and (2) at runtime it is a deployment
    /// sanity check - each library is instantiated independently and its status reported, which
    /// surfaces missing native dependencies (accuscan.dll / nistpack.dll / wsq1000.dll) without
    /// throwing. This is not part of the production workflow.
    /// </summary>
    public static class AwareCapabilities
    {
        public sealed class ProbeResult
        {
            public bool AccuScanOk;
            public string AccuScanInfo;
            public bool NistPackOk;
            public string NistPackInfo;
            public bool WsqOk;
            public string WsqInfo;

            public bool AllOk => AccuScanOk && NistPackOk && WsqOk;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine("Aware managed library probe:");
                sb.AppendLine("  AccuScan : " + (AccuScanOk ? "OK  " : "FAIL") + "  " + AccuScanInfo);
                sb.AppendLine("  NistPack : " + (NistPackOk ? "OK  " : "FAIL") + "  " + NistPackInfo);
                sb.AppendLine("  WSQ      : " + (WsqOk ? "OK  " : "FAIL") + "  " + WsqInfo);
                return sb.ToString();
            }
        }

        public static ProbeResult Probe()
        {
            var result = new ProbeResult();

            try
            {
                using (var scanner = new awAccuScan(awAccuScan.AwareScannerType.AW_SCAN_TYPE_MEMORY))
                {
                    result.AccuScanInfo = "version " + scanner.GetVersion();
                    result.AccuScanOk = true;
                }
            }
            catch (Exception ex)
            {
                result.AccuScanInfo = ex.GetType().Name + ": " + ex.Message;
            }

            try
            {
                using (var nist = new NistPack())
                {
                    result.NistPackInfo = "version " + nist.getVersion();
                    result.NistPackOk = true;
                }
            }
            catch (Exception ex)
            {
                result.NistPackInfo = ex.GetType().Name + ": " + ex.Message;
            }

            try
            {
                using (var wsq = new AwWsq())
                {
                    result.WsqInfo = "instantiated (" + wsq.GetType().FullName + ")";
                    result.WsqOk = true;
                }
            }
            catch (Exception ex)
            {
                result.WsqInfo = ex.GetType().Name + ": " + ex.Message;
            }

            return result;
        }
    }
}
