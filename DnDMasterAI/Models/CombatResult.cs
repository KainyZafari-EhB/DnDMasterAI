using System.Collections.Generic;

namespace DnDGame.Models
{
    public class CombatResult
    {
        public string Winner { get; set; } = string.Empty;
        public int PlayerRemainingHP { get; set; }
        public int EnemyRemainingHP { get; set; }
        public List<string> Log { get; set; } = new();
        public InventoryItem[]? Loot { get; set; }
    }
}