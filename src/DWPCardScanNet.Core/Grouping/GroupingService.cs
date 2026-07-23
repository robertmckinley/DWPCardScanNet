using System;
using System.Collections.Generic;
using System.IO;
using DWPCardScanNet.Core.Configuration;
using DWPCardScanNet.Core.Logging;

namespace DWPCardScanNet.Core.Grouping
{
    public sealed class GroupingResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public string GroupFolder { get; }
        public List<string> RemovedSourcePaths { get; }

        private GroupingResult(bool ok, string err, string folder, List<string> removed)
        {
            Success = ok; ErrorMessage = err; GroupFolder = folder;
            RemovedSourcePaths = removed ?? new List<string>();
        }

        public static GroupingResult Ok(string folder, List<string> removed) => new GroupingResult(true, null, folder, removed);
        public static GroupingResult Fail(string err) => new GroupingResult(false, err, null, null);
    }

    /// <summary>
    /// Moves a scanned card (and, when configured, its flip side) from the to-process queue into a
    /// group folder keyed by the operator's key value. Port of the core of VB6 cmdAddNewGroup_Click.
    /// <para>
    /// Deferred vs. the VB6 (noted for later increments): the GroupConflict spin-off into suffixed
    /// duplicate groups, the .undo journal, and writing a per-card .crp override file (only needed
    /// once interactive crop-region editing exists - the processor falls back to the card definition).
    /// </para>
    /// </summary>
    public sealed class GroupingService
    {
        private readonly Defaults _defaults;
        private readonly IDictionary<string, CardDefinition> _cardsByName;
        private readonly ILog _log;

        public GroupingService(Defaults defaults, IDictionary<string, CardDefinition> cardsByName, ILog log = null)
        {
            _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
            _cardsByName = cardsByName ?? throw new ArgumentNullException(nameof(cardsByName));
            _log = log ?? NullLog.Instance;
        }

        /// <param name="keyPrefix">Optional prefix (group KeyPrefix, e.g. "S-") prepended to the key value.</param>
        /// <param name="adjustedRegions">
        /// The (possibly operator-moved) crop regions for the primary card. When non-empty a ".crp"
        /// override file is written next to the moved card so the processor uses these positions.
        /// </param>
        public GroupingResult AddCardToGroup(string cardType, string keyValue, string sourceBmpPath,
                                             string keyPrefix, IReadOnlyList<CropRegion> adjustedRegions = null)
        {
            if (string.IsNullOrEmpty(cardType) || !_cardsByName.TryGetValue(cardType, out CardDefinition card))
                return GroupingResult.Fail("Please select a valid card type.");
            if (string.IsNullOrWhiteSpace(keyValue))
                return GroupingResult.Fail("Please enter a key value.");
            if (string.IsNullOrEmpty(sourceBmpPath) || !File.Exists(sourceBmpPath))
                return GroupingResult.Fail("Source card image not found: " + sourceBmpPath);

            // If the operator is adding a Back while its Front is still in the queue, require the Front first.
            if (sourceBmpPath.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string frontPeer = ReplaceToken(sourceBmpPath, "Back", "Front");
                if (File.Exists(frontPeer))
                    return GroupingResult.Fail("Please process the Front of the card first.");
            }

            string key = string.IsNullOrEmpty(keyPrefix) ? keyValue.Trim() : keyPrefix + keyValue.Trim();
            string groupFolder = _defaults.GroupsToProcessFolder + key + "\\";
            Directory.CreateDirectory(groupFolder);

            var removed = new List<string>();

            // Move the selected card, renamed to its card type.
            string dest = groupFolder + cardType + ".BMP";
            MoveCard(sourceBmpPath, dest, removed);
            _log.Write("Added card '" + cardType + "' to group '" + key + "'.");

            // Persist any operator crop-region adjustments as a .crp beside the moved card.
            if (adjustedRegions != null && adjustedRegions.Count > 0)
                CropRegionFile.Write(Path.ChangeExtension(dest, ".CRP"), adjustedRegions);

            // Optionally bring the flip side along (unless AddBackToGroup = N).
            if (!string.IsNullOrEmpty(card.FlipSide) &&
                !string.Equals(card.AddBackToGroup, "N", StringComparison.OrdinalIgnoreCase))
            {
                if (!_cardsByName.ContainsKey(card.FlipSide))
                    return GroupingResult.Fail("FlipSide '" + card.FlipSide + "' was not found in the card definitions.");

                string flipSource =
                    sourceBmpPath.IndexOf("Front", StringComparison.OrdinalIgnoreCase) >= 0 ? ReplaceToken(sourceBmpPath, "Front", "Back") :
                    sourceBmpPath.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0 ? ReplaceToken(sourceBmpPath, "Back", "Front") : "";

                if (flipSource != "" && File.Exists(flipSource))
                {
                    MoveCard(flipSource, groupFolder + card.FlipSide + ".BMP", removed);
                    _log.Write("Added flip side '" + card.FlipSide + "' to group '" + key + "'.");
                }
            }

            return GroupingResult.Ok(groupFolder, removed);
        }

        private static void MoveCard(string sourceBmp, string destBmp, List<string> removed)
        {
            if (File.Exists(destBmp)) File.Delete(destBmp);
            File.Move(sourceBmp, destBmp);
            removed.Add(sourceBmp);

            // Remove the matching thumbnail from the queue.
            string thumb = Path.ChangeExtension(sourceBmp, ".JPG");
            if (File.Exists(thumb)) File.Delete(thumb);
        }

        /// <summary>Case-insensitive replace of the first occurrence of a Front/Back token in a path.</summary>
        private static string ReplaceToken(string path, string from, string to)
        {
            int i = path.IndexOf(from, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return path;
            return path.Substring(0, i) + to + path.Substring(i + from.Length);
        }
    }
}
