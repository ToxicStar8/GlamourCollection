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
        this.records.AddRange(snapshot.OrderBy(item => item.ContainerId).ThenBy(item => item.Slot));
        this.Save();
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
}
