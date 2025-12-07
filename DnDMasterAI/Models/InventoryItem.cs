using System;

namespace DnDGame.Models
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public class InventoryItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Rarity Rarity { get; set; } = Rarity.Common;
        public double Weight { get; set; } = 0.0;
        public int Value { get; set; } = 0;

        public override string ToString() => $"{Name} ({Rarity})";
    }
}