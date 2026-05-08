namespace Main.Models;

public sealed record EquipmentRecord(
    uint ItemId,
    string Name,
    uint IconId,
    uint ItemUICategoryId,
    uint EquipSlotCategoryId,
    ulong ModelMain,
    ulong ModelSub)
{
    public string AppearanceKey => this.ModelMain == 0 && this.ModelSub == 0
        ? $"item:{this.ItemId}"
        : $"model:{this.ItemUICategoryId}:{this.EquipSlotCategoryId}:{this.ModelMain}:{this.ModelSub}";
}
