using Main.Models;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Main.Services;

public sealed class OwnedItemRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly List<OwnedItemRecord> records = [];
    private ulong currentCharacterId;

    public IReadOnlyList<OwnedItemRecord> Records => this.records;

    public int Version { get; private set; }

    public void Load(ulong characterId)
    {
        this.currentCharacterId = characterId;
        this.records.Clear();

        var path = this.GetOwnedItemsPath(characterId);
        if (!File.Exists(path))
        {
            this.MarkChanged();
            return;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<List<OwnedItemRecord>>(File.ReadAllText(path), JsonOptions);
            if (loaded is not null)
                this.records.AddRange(loaded);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to load owned item JSON.");
        }

        this.MarkChanged();
    }

    public void ReplaceAll(ulong characterId, IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;
        this.records.Clear();
        this.records.AddRange(snapshot);
        this.SortRecords();
        this.MarkChanged();
        this.Save();
    }

    public OwnedItemSnapshotChangeKind ReplacePhaseOneSnapshot(ulong characterId, IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;
        var oldSnapshot = this.records
            .Where(item => !IsPersistentExternalRecord(item))
            .ToList();
        var newSnapshot = snapshot.ToList();

        if (HasSameRecordSignature(oldSnapshot, newSnapshot))
            return OwnedItemSnapshotChangeKind.None;

        this.records.RemoveAll(item => !IsPersistentExternalRecord(item));
        this.records.AddRange(newSnapshot);
        this.SortRecords();
        this.MarkChanged();
        this.Save();

        return HasSameOwnershipSignature(oldSnapshot, newSnapshot)
            ? OwnedItemSnapshotChangeKind.LocationsOnly
            : OwnedItemSnapshotChangeKind.OwnershipChanged;
    }

    public OwnedItemSnapshotChangeKind ReplaceRetainerSnapshot(
        ulong characterId,
        ulong retainerId,
        string retainerName,
        IEnumerable<OwnedItemRecord> snapshot,
        bool save = true)
    {
        this.currentCharacterId = characterId;
        var normalizedRetainerName = NormalizeRetainerName(retainerName);
        var oldSnapshot = this.records
            .Where(item => IsSameRetainerRecord(item, retainerId, normalizedRetainerName))
            .ToList();
        var newSnapshot = snapshot.ToList();

        if (HasSameRecordSignature(oldSnapshot, newSnapshot))
            return OwnedItemSnapshotChangeKind.None;

        this.records.RemoveAll(item => IsSameRetainerRecord(item, retainerId, normalizedRetainerName));
        this.records.AddRange(newSnapshot);
        this.SortRecords();
        this.MarkChanged();
        if (save)
            this.Save();

        return HasSameOwnershipSignature(oldSnapshot, newSnapshot)
            ? OwnedItemSnapshotChangeKind.LocationsOnly
            : OwnedItemSnapshotChangeKind.OwnershipChanged;
    }

    public void ReplaceSaddlebagSnapshot(
        ulong characterId,
        bool replaceSaddlebag,
        bool replacePremiumSaddlebag,
        IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;

        if (replaceSaddlebag)
            this.records.RemoveAll(IsSaddlebagRecord);

        if (replacePremiumSaddlebag)
            this.records.RemoveAll(IsPremiumSaddlebagRecord);

        this.records.AddRange(snapshot);
        this.SortRecords();
        this.MarkChanged();
        this.Save();
    }

    public void ReplaceGlamourDresserSnapshot(ulong characterId, IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;
        this.records.RemoveAll(IsGlamourDresserRecord);
        this.records.AddRange(snapshot);
        this.SortRecords();
        this.MarkChanged();
        this.Save();
    }

    public void ReplaceArmoireSnapshot(ulong characterId, IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;
        this.records.RemoveAll(IsArmoireRecord);
        this.records.AddRange(snapshot);
        this.SortRecords();
        this.MarkChanged();
        this.Save();
    }

    public int ClearRetainerSnapshots(ulong characterId)
    {
        this.currentCharacterId = characterId;
        var removed = this.records.RemoveAll(IsRetainerRecord);
        if (removed > 0)
        {
            this.SortRecords();
            this.MarkChanged();
            this.Save();
        }

        return removed;
    }

    public int ClearSaddlebagSnapshots(ulong characterId)
    {
        this.currentCharacterId = characterId;
        var removed = this.records.RemoveAll(item => IsSaddlebagRecord(item) || IsPremiumSaddlebagRecord(item));
        if (removed > 0)
        {
            this.SortRecords();
            this.MarkChanged();
            this.Save();
        }

        return removed;
    }

    public int ClearGlamourDresserSnapshots(ulong characterId)
    {
        this.currentCharacterId = characterId;
        var removed = this.records.RemoveAll(IsGlamourDresserRecord);
        if (removed > 0)
        {
            this.SortRecords();
            this.MarkChanged();
            this.Save();
        }

        return removed;
    }

    public int ClearArmoireSnapshots(ulong characterId)
    {
        this.currentCharacterId = characterId;
        var removed = this.records.RemoveAll(IsArmoireRecord);
        if (removed > 0)
        {
            this.SortRecords();
            this.MarkChanged();
            this.Save();
        }

        return removed;
    }

    public IReadOnlyList<OwnedItemRecord> FindByItemId(uint itemId)
        => this.records.Where(item => item.ItemId == itemId).ToList();

    public void Clear()
    {
        this.currentCharacterId = 0;
        this.records.Clear();
        this.MarkChanged();
    }

    public void Save()
    {
        if (this.currentCharacterId == 0)
            return;

        var path = this.GetOwnedItemsPath(this.currentCharacterId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this.records, JsonOptions));
    }

    private string GetOwnedItemsPath(ulong characterId)
    {
        var directory = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "owned-items");
        return Path.Combine(directory, $"{characterId:X16}.json");
    }

    private void SortRecords()
    {
        this.records.Sort((left, right) =>
        {
            var sourceCompare = GetSourceSort(left).CompareTo(GetSourceSort(right));
            if (sourceCompare != 0)
                return sourceCompare;

            var retainerCompare = string.Compare(left.RetainerName, right.RetainerName, StringComparison.OrdinalIgnoreCase);
            if (retainerCompare != 0)
                return retainerCompare;

            var containerCompare = left.ContainerId.CompareTo(right.ContainerId);
            return containerCompare != 0 ? containerCompare : left.Slot.CompareTo(right.Slot);
        });
    }

    private void MarkChanged()
        => this.Version++;

    private static int GetSourceSort(OwnedItemRecord item)
    {
        if (IsRetainerRecord(item))
            return 1;

        if (IsSaddlebagRecord(item))
            return 2;

        if (IsPremiumSaddlebagRecord(item))
            return 3;

        if (IsGlamourDresserRecord(item))
            return 4;

        if (IsArmoireRecord(item))
            return 5;

        return 0;
    }

    private static bool IsSameRetainerRecord(OwnedItemRecord item, ulong retainerId, string retainerName)
    {
        if (!IsRetainerRecord(item))
            return false;

        if (retainerId != 0 && item.RetainerId != 0)
            return item.RetainerId == retainerId;

        return string.Equals(
            NormalizeRetainerName(item.RetainerName),
            retainerName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetainerRecord(OwnedItemRecord item)
        => item.RetainerId != 0
           || !string.IsNullOrWhiteSpace(item.RetainerName)
           || item.SourceContainer.StartsWith("Retainer:", StringComparison.Ordinal)
           || item.SourceContainer.StartsWith("雇员:", StringComparison.Ordinal)
           || item.ContainerType.StartsWith("Retainer", StringComparison.Ordinal);

    private static bool IsPersistentExternalRecord(OwnedItemRecord item)
        => IsRetainerRecord(item)
           || IsSaddlebagRecord(item)
           || IsPremiumSaddlebagRecord(item)
           || IsGlamourDresserRecord(item)
           || IsArmoireRecord(item);

    private static bool IsSaddlebagRecord(OwnedItemRecord item)
        => string.Equals(item.SourceContainer, "Saddlebag", StringComparison.Ordinal)
           || string.Equals(item.SourceContainer, "陆行鸟背包", StringComparison.Ordinal)
           || item.ContainerType.StartsWith("SaddleBag", StringComparison.Ordinal);

    private static bool IsPremiumSaddlebagRecord(OwnedItemRecord item)
        => string.Equals(item.SourceContainer, "Premium Saddlebag", StringComparison.Ordinal)
           || string.Equals(item.SourceContainer, "高级陆行鸟背包", StringComparison.Ordinal)
           || item.ContainerType.StartsWith("PremiumSaddleBag", StringComparison.Ordinal);

    private static bool IsGlamourDresserRecord(OwnedItemRecord item)
        => string.Equals(item.SourceContainer, "Glamour Dresser", StringComparison.Ordinal)
           || string.Equals(item.SourceContainer, "幻化柜", StringComparison.Ordinal)
           || item.ContainerType.StartsWith("GlamourDresser", StringComparison.Ordinal);

    private static bool IsArmoireRecord(OwnedItemRecord item)
        => string.Equals(item.SourceContainer, "Armoire", StringComparison.Ordinal)
           || string.Equals(item.SourceContainer, "收藏柜", StringComparison.Ordinal)
           || item.ContainerType.StartsWith("Armoire", StringComparison.Ordinal);

    private static bool HasSameRecordSignature(
        IReadOnlyList<OwnedItemRecord> left,
        IReadOnlyList<OwnedItemRecord> right)
        => HasSameSignature(left, right, BuildRecordSignature);

    private static bool HasSameOwnershipSignature(
        IReadOnlyList<OwnedItemRecord> left,
        IReadOnlyList<OwnedItemRecord> right)
        => HasSameSignature(left, right, BuildOwnershipSignature);

    private static bool HasSameSignature(
        IReadOnlyList<OwnedItemRecord> left,
        IReadOnlyList<OwnedItemRecord> right,
        Func<OwnedItemRecord, string> buildSignature)
    {
        if (left.Count != right.Count)
            return false;

        var leftSignatures = left.Select(buildSignature).Order(StringComparer.Ordinal).ToList();
        var rightSignatures = right.Select(buildSignature).Order(StringComparer.Ordinal).ToList();
        for (var index = 0; index < leftSignatures.Count; index++)
        {
            if (!string.Equals(leftSignatures[index], rightSignatures[index], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string BuildRecordSignature(OwnedItemRecord item)
        => $"{BuildOwnershipSignature(item)}|{item.Quantity}|{item.SourceContainer}|{item.ContainerType}|{item.ContainerId}|{item.Slot}";

    private static string BuildOwnershipSignature(OwnedItemRecord item)
        => $"{GetBaseItemId(item)}|{item.IsHq}";

    private static uint GetBaseItemId(OwnedItemRecord item)
    {
        if (item.BaseItemId != 0)
            return item.BaseItemId;

        if (item.ItemId != 0)
            return NormalizeBaseItemId(item.ItemId);

        return NormalizeBaseItemId(item.RawItemId);
    }

    private static uint NormalizeBaseItemId(uint itemId)
    {
        const uint highQualityItemIdOffset = 1_000_000;
        return itemId > highQualityItemIdOffset ? itemId - highQualityItemIdOffset : itemId;
    }

    private static string NormalizeRetainerName(string retainerName)
        => string.IsNullOrWhiteSpace(retainerName) ? "未知雇员" : retainerName.Trim();
}

public enum OwnedItemSnapshotChangeKind
{
    None,
    LocationsOnly,
    OwnershipChanged,
}
