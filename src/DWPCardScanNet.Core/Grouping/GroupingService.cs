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
        /// <param name="confirmDuplicateGroup">
        /// Called (baseKey, suffixedKey) when a conflict forces the card into a new suffixed group;
        /// return false to cancel. Null = proceed silently. Lets the UI prompt without Core doing UI.
        /// </param>
        public GroupingResult AddCardToGroup(string cardType, string keyValue, string sourceBmpPath,
                                             string keyPrefix, IReadOnlyList<CropRegion> adjustedRegions = null,
                                             Func<string, string, bool> confirmDuplicateGroup = null)
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

            string baseKey = string.IsNullOrEmpty(keyPrefix) ? keyValue.Trim() : keyPrefix + keyValue.Trim();
            string key = baseKey;
            string groupFolder = _defaults.GroupsToProcessFolder + key + "\\";
            string dest = groupFolder + cardType + ".BMP";

            // A card conflicts with a group that already holds the same card type, or a card using
            // one of this card's finger indices (flip side checked too). When the base key group
            // conflicts, spin the card into a new suffixed group (_2, _3, ...) so two subjects that
            // share a key value in one batch are kept apart. Port of the VB6 GroupConflict loop.
            int suffix = 1;
            while (GroupConflict(card, groupFolder, dest))
            {
                suffix++;
                if (suffix > 999)
                    return GroupingResult.Fail("Unable to find an available group for '" + cardType + "'.");
                key = baseKey + "_" + suffix.ToString();
                groupFolder = _defaults.GroupsToProcessFolder + key + "\\";
                dest = groupFolder + cardType + ".BMP";
            }
            if (key != baseKey && confirmDuplicateGroup != null && !confirmDuplicateGroup(baseKey, key))
                return GroupingResult.Fail(null);   // cancelled by the operator (no error to show)

            Directory.CreateDirectory(groupFolder);
            string undoPath = groupFolder + key + ".undo";

            var removed = new List<string>();

            // Move the selected card, renamed to its card type.
            MoveCard(sourceBmpPath, dest, removed, undoPath);
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
                    MoveCard(flipSource, groupFolder + card.FlipSide + ".BMP", removed, undoPath);
                    _log.Write("Added flip side '" + card.FlipSide + "' to group '" + key + "'.");
                }
            }

            return GroupingResult.Ok(groupFolder, removed);
        }

        /// <summary>
        /// Undoes a group: moves every card recorded in the group's ".undo" journal back to its
        /// original queue location and removes the group folder. Returns the restored BMP paths in
        /// <see cref="GroupingResult.RemovedSourcePaths"/> (the caller regenerates their thumbnails).
        /// Port of VB6 UndoImageGroup.
        /// </summary>
        public GroupingResult UndoGroup(string groupFolder)
        {
            if (string.IsNullOrEmpty(groupFolder) || !Directory.Exists(groupFolder))
                return GroupingResult.Fail("Group folder not found: " + groupFolder);

            string groupName = Path.GetFileName(groupFolder.TrimEnd('\\', '/'));
            string undoPath = Path.Combine(groupFolder, groupName + ".undo");
            var restored = new List<string>();

            if (File.Exists(undoPath))
            {
                foreach (string line in File.ReadAllLines(undoPath))
                {
                    string t = line.Trim();
                    if (t.Length == 0) continue;
                    string[] parts = t.Split('|');           // originalPath | currentPath
                    if (parts.Length != 2) continue;

                    string original = parts[0].Trim();
                    string current = parts[1].Trim();
                    if (!File.Exists(current)) continue;
                    if (File.Exists(original)) File.Delete(original);
                    File.Move(current, original);
                    restored.Add(original);
                }
            }

            // Remove leftover work files (.crp/.undo/etc.) and the folder itself.
            foreach (string f in Directory.GetFiles(groupFolder)) File.Delete(f);
            Directory.Delete(groupFolder, true);

            _log.Write("Undid group '" + groupName + "', restored " + restored.Count + " card(s).");
            return GroupingResult.Ok(groupFolder, restored);
        }

        private static void MoveCard(string sourceBmp, string destBmp, List<string> removed, string undoPath)
        {
            if (File.Exists(destBmp)) File.Delete(destBmp);
            File.Move(sourceBmp, destBmp);
            removed.Add(sourceBmp);

            // Journal the move so the group can be undone (source|dest).
            File.AppendAllText(undoPath, sourceBmp + "|" + destBmp + "\r\n");

            // Remove the matching thumbnail from the queue.
            string thumb = Path.ChangeExtension(sourceBmp, ".JPG");
            if (File.Exists(thumb)) File.Delete(thumb);
        }

        /// <summary>True if the card can't be added to the group: same card type present, or a shared finger index (flip side too). Port of VB6 GroupConflict.</summary>
        private bool GroupConflict(CardDefinition card, string groupFolder, string destFile)
        {
            try
            {
                if (File.Exists(destFile)) return true;                       // same card type already present
                if (FingerExistsInGroup(card, groupFolder)) return true;      // shared finger index
                if (!string.IsNullOrEmpty(card.FlipSide) &&
                    _cardsByName.TryGetValue(card.FlipSide, out CardDefinition flip) &&
                    FingerExistsInGroup(flip, groupFolder)) return true;
                return false;
            }
            catch
            {
                return true;   // fail closed: never silently merge different subjects into one group
            }
        }

        /// <summary>True if any card already in the group shares a finger index (other than 15) with <paramref name="card"/>. Port of VB6 FingerExistsInGroup.</summary>
        private bool FingerExistsInGroup(CardDefinition card, string groupFolder)
        {
            if (!Directory.Exists(groupFolder)) return false;

            var mine = new HashSet<int>();
            foreach (CropRegion r in card.CropRegions)
                if (r.FingerIndex != 15) mine.Add(r.FingerIndex);
            if (mine.Count == 0) return false;

            foreach (string bmp in Directory.GetFiles(groupFolder, "*.BMP"))
            {
                if (!_cardsByName.TryGetValue(Path.GetFileNameWithoutExtension(bmp), out CardDefinition existing))
                    continue;
                foreach (CropRegion r in existing.CropRegions)
                    if (r.FingerIndex != 15 && mine.Contains(r.FingerIndex))
                        return true;
            }
            return false;
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
