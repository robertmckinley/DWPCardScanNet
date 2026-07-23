using System;
using System.Text;
using Aware.NistPack;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// Re-reads a written NIST packet and reports its structure and whether it passes Aware's
    /// own verification. Used by the validation harness to confirm a produced .eft is well-formed.
    /// </summary>
    public static class AwareNistInspector
    {
        public static string Inspect(string eftPath, string verificationFile)
        {
            var sb = new StringBuilder();
            using (var verification = new AwareVerification())
            using (var nist = new NistPack())
            {
                verification.readVerification(verificationFile);
                nist.readTransaction(eftPath, verification);

                sb.AppendLine("    total records: " + nist.getNumRecords());
                foreach (int type in new[] { 1, 2, 4, 7, 15 })
                {
                    int count = nist.getRecordTypeCount(type);
                    if (count > 0) sb.AppendLine("    Type-" + type + ": " + count);
                }

                bool verified;
                try { verified = nist.verify(); }
                catch (Exception ex) { verified = false; sb.AppendLine("    verify() threw: " + ex.Message); }
                sb.AppendLine("    verify(): " + verified);

                int errorCount = nist.getErrorCount();
                if (errorCount > 0)
                {
                    sb.AppendLine("    verification errors (" + errorCount + "):");
                    for (int i = 0; i < errorCount && i < 15; i++)
                        sb.AppendLine("      [" + i + "] " + nist.getErrorDesc(i));
                }
            }
            return sb.ToString();
        }
    }
}
