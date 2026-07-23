using System;
using System.Collections.Generic;
using System.Text;
using Aware.AwWsq1000;
using Aware.NistPack;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// Field- and image-level diff of two NIST packets, for golden-file validation of the
    /// managed pipeline against the legacy VB6 output. Fields that legitimately vary per run
    /// (key, dates, PDF-name tag, and the derived record-length fields) are classified as
    /// "expected". WSQ image records are compared by DECODED PIXELS (dimensions + mean absolute
    /// difference), not raw bytes, so a lossy re-encode / embedded-comment difference that
    /// preserves the image is reported as identical rather than a false mismatch.
    /// </summary>
    public static class AwareNistDiff
    {
        private static readonly HashSet<string> ExpectedDiffMnemonics =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "T2_OCA", "T2_PRC", "T15_PCD", "T7_PCD", "T2_EAD", "T1_DAT",
              // Record-length fields are auto-derived and vary with the (expected-diff) key length.
              "T1_LEN", "T2_LEN", "T4_LEN", "T7_LEN", "T15_LEN" };

        private static readonly int[] RecordTypes = { 1, 2, 4, 7, 15 };

        public static string Compare(string pathA, string pathB, string verificationFile, string tot)
        {
            var sb = new StringBuilder();
            using (var vA = new AwareVerification())
            using (var vB = new AwareVerification())
            using (var a = new NistPack())
            using (var b = new NistPack())
            {
                vA.readVerification(verificationFile);
                vB.readVerification(verificationFile);
                a.readTransaction(pathA, vA);
                b.readTransaction(pathB, vB);

                sb.AppendLine("A = " + pathA);
                sb.AppendLine("B = " + pathB);

                int unexpected = 0, expected = 0, imgSame = 0, imgDiff = 0, imgMeta = 0;

                foreach (int t in RecordTypes)
                {
                    int ca = a.getRecordTypeCount(t), cb = b.getRecordTypeCount(t);
                    if (ca != cb)
                    {
                        sb.AppendLine("RECORD-COUNT DIFF Type-" + t + ": A=" + ca + " B=" + cb);
                        unexpected++;
                    }

                    int n = Math.Min(ca, cb);
                    for (int ri = 1; ri <= n; ri++)
                    {
                        var fields = new SortedSet<int>();
                        int f = 0; while ((f = SafeNextField(a, t, ri, f)) > 0) fields.Add(f);
                        f = 0; while ((f = SafeNextField(b, t, ri, f)) > 0) fields.Add(f);

                        foreach (int field in fields)
                        {
                            int subs = Math.Max(SafeNumSub(a, t, ri, field), SafeNumSub(b, t, ri, field));
                            for (int sub = 1; sub <= subs; sub++)
                            {
                                int items = Math.Max(SafeNumItems(a, t, ri, field, sub),
                                                     SafeNumItems(b, t, ri, field, sub));
                                for (int it = 1; it <= items; it++)
                                {
                                    string va = SafeItem(a, t, ri, field, sub, it);
                                    string vb = SafeItem(b, t, ri, field, sub, it);
                                    if (va == vb) continue;

                                    string mn = SafeMnemonic(vA, tot, t, field, sub, it) ?? (t + "." + field);
                                    string line = "  " + mn + " [T" + t + "#" + ri + " " + field + "." + sub + "." + it +
                                                  "]: A='" + Trunc(va) + "' B='" + Trunc(vb) + "'";
                                    if (ExpectedDiffMnemonics.Contains(mn)) { expected++; sb.AppendLine("  (expected)" + line); }
                                    else { unexpected++; sb.AppendLine("  DIFF" + line); }
                                }
                            }
                        }

                        if (t == 4 || t == 7 || t == 15)
                        {
                            byte[] ia = SafeImage(a, t, ri), ib = SafeImage(b, t, ri);
                            if (ia == null && ib == null) { }
                            else if (ia == null || ib == null)
                            {
                                sb.AppendLine("  IMAGE-PRESENCE DIFF T" + t + "#" + ri);
                                imgDiff++;
                            }
                            else CompareImage(t, ri, ia, ib, sb, ref imgSame, ref imgDiff, ref imgMeta);
                        }
                    }
                }

                sb.AppendLine("SUMMARY: unexpected field diffs=" + unexpected +
                              ", expected(key/date/len) diffs=" + expected);
                sb.AppendLine("  images: identical=" + imgSame + " (of which WSQ-byte-differ-but-pixels-identical=" + imgMeta +
                              "), differing=" + imgDiff);
                sb.AppendLine(unexpected == 0 && imgDiff == 0
                    ? "RESULT: MATCH (images pixel-identical; ignoring expected key/date/len fields)"
                    : "RESULT: DIFFERENCES FOUND");
            }
            return sb.ToString();
        }

        private static void CompareImage(int t, int ri, byte[] ia, byte[] ib, StringBuilder sb,
                                         ref int imgSame, ref int imgDiff, ref int imgMeta)
        {
            bool byteEqual = BytesEqual(ia, ib);

            // Type-7 (data header) is stored as a BMP, not WSQ - compare bytes directly.
            if (t == 7)
            {
                if (byteEqual) imgSame++;
                else { sb.AppendLine("  IMAGE BYTES DIFFER T7#" + ri + ": A=" + ia.Length + "b B=" + ib.Length + "b"); imgDiff++; }
                return;
            }

            byte[] ra = DecodeWsq(ia, out int ha, out int wa);
            byte[] rb = DecodeWsq(ib, out int hb, out int wb);
            if (ra == null || rb == null)
            {
                if (byteEqual) imgSame++;
                else { sb.AppendLine("  IMAGE BYTES DIFFER T" + t + "#" + ri + " (WSQ decode failed): A=" + ia.Length + "b B=" + ib.Length + "b"); imgDiff++; }
                return;
            }

            if (ha != hb || wa != wb)
            {
                sb.AppendLine("  IMAGE DIMS DIFFER T" + t + "#" + ri + ": A=" + ha + "x" + wa + " B=" + hb + "x" + wb);
                imgDiff++;
                return;
            }

            double mad = Mad(ra, rb);
            if (mad == 0.0)
            {
                imgSame++;
                if (!byteEqual) { imgMeta++; sb.AppendLine("  image T" + t + "#" + ri + " pixel-identical (" + ha + "x" + wa + "), WSQ bytes differ by " + Math.Abs(ia.Length - ib.Length) + "b (metadata only)"); }
            }
            else
            {
                sb.AppendLine("  IMAGE PIXELS DIFFER T" + t + "#" + ri + " (" + ha + "x" + wa + "): mean-abs-diff=" + Math.Round(mad, 3));
                imgDiff++;
            }
        }

        private static byte[] DecodeWsq(byte[] wsq, out int rows, out int cols)
        {
            rows = 0; cols = 0;
            try
            {
                using (var w = new AwWsq())
                {
                    w.SetInputImage(AwWsqImageFormat.WSQ, wsq);
                    rows = w.GetInputImageRows();
                    cols = w.GetInputImageCols();
                    return w.GetOutputImage(AwWsqImageFormat.RAW);
                }
            }
            catch { return null; }
        }

        private static double Mad(byte[] x, byte[] y)
        {
            if (x == null || y == null || x.Length != y.Length) return -1;
            long sum = 0;
            for (int i = 0; i < x.Length; i++) { int d = x[i] - y[i]; if (d < 0) d = -d; sum += d; }
            return (double)sum / x.Length;
        }

        private static int SafeNextField(NistPack p, int t, int ri, int field)
        { try { return p.getNextField(t, ri, field); } catch { return 0; } }

        private static int SafeNumSub(NistPack p, int t, int ri, int field)
        { try { return p.numSubfields(t, ri, field); } catch { return 0; } }

        private static int SafeNumItems(NistPack p, int t, int ri, int field, int sub)
        { try { return p.numItems(t, ri, field, sub); } catch { return 0; } }

        private static string SafeItem(NistPack p, int t, int ri, int field, int sub, int it)
        { try { return p.findItem(t, ri, field, sub, it); } catch { return null; } }

        private static string SafeMnemonic(AwareVerification v, string tot, int t, int field, int sub, int it)
        { try { return v.getRuleMnemonic(tot, t, field, sub, it); } catch { return null; } }

        private static byte[] SafeImage(NistPack p, int t, int ri)
        { try { return p.getImage(t, ri); } catch { return null; } }

        private static bool BytesEqual(byte[] x, byte[] y)
        {
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        private static string Trunc(string s)
        {
            if (s == null) return "(none)";
            return s.Length <= 40 ? s : s.Substring(0, 40) + "...";
        }
    }
}
