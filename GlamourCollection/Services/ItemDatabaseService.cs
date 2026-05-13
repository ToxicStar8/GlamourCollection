using Dalamud.Utility;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Main.Services;

public sealed class ItemDatabaseService(SourceInfoService sourceInfo)
{
    private readonly List<EquipmentRecord> equipment = [];
    private readonly Dictionary<uint, EquipmentRecord> equipmentById = [];
    private bool isLoaded;

    public IReadOnlyList<EquipmentRecord> Equipment => this.equipment;

    public void Reload()
    {
        this.isLoaded = false;
        this.Load();
    }

    public void Load()
    {
        if (this.isLoaded)
            return;

        this.equipment.Clear();
        this.equipmentById.Clear();

        var sheet = Svc.Data.GetExcelSheet<Item>();

        foreach (var item in sheet)
        {
            if (item.RowId == 0 || item.EquipSlotCategory.RowId == 0)
                continue;

            var name = item.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var itemSourceInfo = sourceInfo.GetSourceInfo(item);
            var record = new EquipmentRecord(
                item.RowId,
                name,
                item.Icon,
                item.ItemUICategory.Value.Name.ExtractText(),
                item.ClassJobCategory.Value.Name.ExtractText(),
                item.LevelEquip,
                item.LevelItem.RowId,
                item.DyeCount > 0,
                itemSourceInfo.Text,
                itemSourceInfo.Categories,
                itemSourceInfo.HasDetailedData,
                itemSourceInfo.Expansion,
                itemSourceInfo.ExpansionText,
                itemSourceInfo.IsExpansionEstimated,
                item.ItemUICategory.RowId,
                item.EquipSlotCategory.RowId,
                item.ModelMain,
                item.ModelSub);

            this.equipment.Add(record);
            this.equipmentById[record.ItemId] = record;
        }

        this.equipment.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
        this.isLoaded = true;
    }

    public bool TryGetEquipment(uint itemId, out EquipmentRecord record)
    {
        this.Load();
        return this.equipmentById.TryGetValue(itemId, out record!);
    }

    public bool RefreshSourceInfo(uint itemId)
    {
        this.Load();
        if (!this.equipmentById.TryGetValue(itemId, out var current))
            return false;

        foreach (var item in Svc.Data.GetExcelSheet<Item>())
        {
            if (item.RowId != itemId)
                continue;

            var itemSourceInfo = sourceInfo.GetSourceInfo(item);
            var updated = current with
            {
                SourceInfo = itemSourceInfo.Text,
                SourceCategories = itemSourceInfo.Categories,
                HasDetailedData = itemSourceInfo.HasDetailedData,
                Expansion = itemSourceInfo.Expansion,
                ExpansionInfo = itemSourceInfo.ExpansionText,
                IsExpansionEstimated = itemSourceInfo.IsExpansionEstimated,
            };

            this.equipmentById[itemId] = updated;
            var index = this.equipment.FindIndex(record => record.ItemId == itemId);
            if (index >= 0)
                this.equipment[index] = updated;

            return true;
        }

        return false;
    }
}
