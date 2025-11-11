using System.Text.Json;

namespace DnDGame.Models
{
    public class Character
    {
        public string Name { get; set; }
        public int HP { get; set; } = 10;
        public int Strength { get; set; } = 8;
        public int Dexterity { get; set; } = 12;
        public int Intelligence { get; set; } = 10;
        public List<string> Inventory { get; set; } = new();

        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText($"Data/Characters/{Name}.json", json);
        }

        public static Character Load(string name)
        {
            string path = $"Data/Characters/{name}.json";
            if (!File.Exists(path))
                return new Character { Name = name };

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Character>(json);
        }
    }
}
