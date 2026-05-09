using System;

namespace Main.Models;

public sealed class OwnedItemRecord
{
    public uint RawItemId { get; set; }

    public uint BaseItemId { get; set; }

    public uint ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public bool IsHq { get; set; }

    public string SourceContainer { get; set; } = string.Empty;

    public string ContainerType { get; set; } = string.Empty;

    public ushort ContainerId { get; set; }

    public uint Slot { get; set; }

    public ulong RetainerId { get; set; }

    public string RetainerName { get; set; } = string.Empty;

    public ulong CharacterId { get; set; }

    public uint WorldId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
