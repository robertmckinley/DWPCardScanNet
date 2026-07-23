using System;
using System.Collections.Generic;
using System.IO;
using DWPCardScanNet.Aware;
using DWPCardScanNet.Core.Configuration;
using DWPCardScanNet.Core.Processing;

namespace DWPCardScanNet.PipelineHarness
{
    /// <summary>
    /// Validation harness for the managed crop -> WSQ -> NIST pipeline.
    /// <para>Batch mode (no args): processes test cards 0001-0005 as "CAR  - Front"/"CAR  - Back"
    /// and verifies each produced packet.</para>
    /// <para>Diff mode: <c>diff &lt;packetA.eft&gt; &lt;packetB.eft&gt;</c> compares two packets
    /// field- and image-wise (for golden-file comparison against the legacy VB6 output).</para>
    /// </summary>
    internal static class Program
    {
        private const string IniPath = @"C:\MugConfig\DWPCardScan\DWPCardScan.ini";
        private const string SourceDir = @"C:\MugConfig\DWPCardScan\CardsToProcess";
        private const string DriveRemapTo = @"C:\MugConfig\DWPCardScan\"; // test D: -> C:\MugConfig\DWPCardScan
        private const string FrontCard = "CAR  - Front";
        private const string BackCard = "CAR  - Back";
        private static readonly string[] Samples = { "0001", "0002", "0003", "0004", "0005" };

        private static int Main(string[] args)
        {
            try
            {
                var reader = new ConfigReader(IniPath);
                Defaults defaults = reader.LoadDefaults();

                var cardsByName = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (CardDefinition c in reader.LoadCardDefinitions()) cardsByName[c.CardName] = c;

                var groupsByName = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (GroupDefinition g in reader.LoadGroupDefinitions())
                {
                    g.NISTOutDir = Remap(g.NISTOutDir);
                    Directory.CreateDirectory(g.NISTOutDir);
                    groupsByName[g.GroupName] = g;
                }
                GroupDefinition car = groupsByName["CAR"];

                if (args.Length >= 3 && string.Equals(args[0], "diff", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Write(AwareNistDiff.Compare(args[1], args[2], car.VerificationFile, car.TOT));
                    return 0;
                }

                return RunBatch(defaults, cardsByName, groupsByName, car);
            }
            catch (Exception ex)
            {
                Console.WriteLine("HARNESS FAILED: " + ex);
                return 1;
            }
        }

        private static int RunBatch(Defaults defaults, IDictionary<string, CardDefinition> cardsByName,
                                    IDictionary<string, GroupDefinition> groupsByName, GroupDefinition car)
        {
            // Isolate group input to a fresh temp folder.
            string groupsRoot = Path.Combine(Path.GetTempPath(), "DWPCardScanNet_Harness", "Groups");
            if (Directory.Exists(groupsRoot)) Directory.Delete(groupsRoot, true);
            Directory.CreateDirectory(groupsRoot);
            defaults.GroupsToProcessFolder = groupsRoot + "\\";

            // Remove packets from prior harness runs so names don't get deduped to _1, _2, ...
            foreach (string old in Directory.GetFiles(car.NISTOutDir, "S-HARNESS*.eft"))
                File.Delete(old);

            var built = new List<string>();
            foreach (string sample in Samples)
            {
                string groupName = "S-HARNESS" + sample;
                string groupDir = Path.Combine(groupsRoot, groupName);
                Directory.CreateDirectory(groupDir);
                CopyAs(Path.Combine(SourceDir, sample + "_Front.BMP"), Path.Combine(groupDir, FrontCard + ".BMP"));
                CopyAs(Path.Combine(SourceDir, sample + "_Back.BMP"), Path.Combine(groupDir, BackCard + ".BMP"));
                built.Add(groupName);
            }

            Console.WriteLine("=== DWPCardScanNet pipeline harness (batch) ===");
            Console.WriteLine("Groups built : " + string.Join(", ", built.ToArray()));
            Console.WriteLine("Front card   : '" + FrontCard + "' regions=" + RegionCount(cardsByName, FrontCard));
            Console.WriteLine("Output dir   : " + car.NISTOutDir);
            Console.WriteLine();

            var processor = new GroupProcessor(
                new AwareNistTransactionFactory(),
                new AwareCardImageExtractorFactory(),
                new AwareWsqCompressor());

            var request = new GroupProcessRequest
            {
                Defaults = defaults,
                CardsByName = cardsByName,
                GroupsByName = groupsByName,
                DeleteGroupsAfterProcessing = false
            };

            GroupProcessResult result = processor.ProcessGroups(request);

            Console.WriteLine("GroupsProcessed=" + result.GroupsProcessed + "  GroupsFailed=" + result.GroupsFailed);
            foreach (string err in result.Errors) Console.WriteLine("ERROR: " + err);

            foreach (string packet in result.WrittenPackets)
            {
                Console.WriteLine();
                Console.WriteLine("Packet: " + Path.GetFileName(packet) +
                                  (File.Exists(packet) ? "  (" + new FileInfo(packet).Length + " bytes)" : "  (MISSING)"));
                if (File.Exists(packet))
                    Console.Write(AwareNistInspector.Inspect(packet, car.VerificationFile));
            }

            return result.GroupsFailed == 0 ? 0 : 2;
        }

        private static void CopyAs(string source, string dest)
        {
            if (File.Exists(source)) File.Copy(source, dest, true);
        }

        private static string RegionCount(IDictionary<string, CardDefinition> cards, string name) =>
            cards.TryGetValue(name, out CardDefinition c) ? c.CropRegions.Count.ToString() : "CARD NOT FOUND";

        private static string Remap(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.StartsWith(@"D:\", StringComparison.OrdinalIgnoreCase)
                ? DriveRemapTo + path.Substring(3)
                : path;
        }
    }
}
