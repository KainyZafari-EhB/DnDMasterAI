namespace DnDGame.Models
{
    public class Enemy
    {
        public string Name { get; set; } = "Unknown";
        public int HP { get; set; } = 5;
        public int Strength { get; set; } = 6;
        public int Dexterity { get; set; } = 8;
        public int Intelligence { get; set; } = 5;

        public override string ToString() => $"{Name} (HP: {HP})";
    }
}