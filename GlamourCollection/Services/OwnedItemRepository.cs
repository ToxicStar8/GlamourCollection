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

    public void Load(ulong characterId)
    {
        this.currentCharacterId = characterId;
        this.records.Clear();

        var path = this.GetOwnedItemsPath(characterId);
        if (!File.Exists(path))
            return;

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
    }

    public void ReplaceAll(ulong characterId, IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;
        this.records.Clear();
        this.records.AddRange(snapshot);
        this.SortRecords();
        this.Save();
    }

    public void ReplacePhaseOneSnapshot(ulong characterId, IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;
        this.records.RemoveAll(item => !IsRetainerRecord(item));
        this.records.AddRange(snapshot);
        this.SortRecords();
        this.Save();
    }

    public void ReplaceRetainerSnapshot(
        ulong characterId,
        ulong retainerId,
        string retainerName,
        IEnumerable<OwnedItemRecord> snapshot)
    {
        this.currentCharacterId = characterId;
        var normalizedRetainerName = NormalizeRetainerName(retainerName);

        this.records.RemoveAll(item => IsSameRetainerRecord(item, retainerId, normalizedRetainerName));
        this.records.AddRange(snapshot);
        this.SortRecords();
        this.Save();
    }

    public int ClearRetainerSnapshots(ulong characterId)
    {
        this.currentCharacterId = characterId;
        var removed = this.records.RemoveAll(IsRetainerRecord);
        if (removed > 0)
        {
            this.SortRecords();
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

    private static int GetSourceSort(OwnedItemRecord item)
        => IsRetainerRecord(item) ? 1 : 0;

    private static bool IsSameRetainerRecord(OwnedItemRecord item, ulong retainerId, string retainerName)
    {
        if (!IsRetainerRecord(item))
            return false;

        if (retainerId != 0 && item.RetainerId == retainerId)
            return true;

        return retainerId == 0
               && string.Equals(NormalizeRetainerName(item.RetainerName), retainerName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetainerRecord(OwnedItemRecord item)
        => item.RetainerId != 0
           || !string.IsNullOrWhiteSpace(item.RetainerName)
           || item.SourceContainer.StartsWith("Retainer:", StringComparison.Ordinal)
           || item.ContainerType.StartsWith("Retainer", StringComparison.Ordinal);

    private static string NormalizeRetainerName(string retainerName)
        => string.IsNullOrWhiteSpace(retainerName) ? "Unknown Retainer" : retainerName.Trim();
}
