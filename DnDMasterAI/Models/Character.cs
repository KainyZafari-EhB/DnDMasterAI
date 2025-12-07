using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DnDGame.Models
{
    public class Character
    {
        public string Name { get; set; } = "Unnamed";
        public int HP { get; set; } = 10;
        public int Strength { get; set; } = 8;
        public int Dexterity { get; set; } = 12;
        public int Intelligence { get; set; } = 10;
        public List<InventoryItem> Inventory { get; set; } = new();

        public void Save()
        {
            Directory.CreateDirectory("Data/Characters");
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText($"Data/Characters/{Name}.json", json);
        }

        public static Character Load(string name)
        {
            string path = $"Data/Characters/{name}.json";
            if (!File.Exists(path))
                return new Character { Name = name };

            string json = File.ReadAllText(path);

            try
            {
                // Attempt full deserialization into the new model
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var character = JsonSerializer.Deserialize<Character>(json, options);
                if (character != null)
                {
                    character.Name ??= name;
                    character.Inventory ??= new List<InventoryItem>();
                    return character;
                }
            }
            catch
            {
                // fallthrough to legacy parsing below
            }

            // Backwards-compatible parsing:
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var result = new Character { Name = name };

                if (root.TryGetProperty("Name", out var pName) && pName.ValueKind == JsonValueKind.String)
                    result.Name = pName.GetString() ?? name;

                if (root.TryGetProperty("HP", out var pHp) && pHp.ValueKind == JsonValueKind.Number)
                    result.HP = pHp.GetInt32();

                if (root.TryGetProperty("Strength", out var pStr) && pStr.ValueKind == JsonValueKind.Number)
                    result.Strength = pStr.GetInt32();

                if (root.TryGetProperty("Dexterity", out var pDex) && pDex.ValueKind == JsonValueKind.Number)
                    result.Dexterity = pDex.GetInt32();

                if (root.TryGetProperty("Intelligence", out var pInt) && pInt.ValueKind == JsonValueKind.Number)
                    result.Intelligence = pInt.GetInt32();

                // Inventory may be array of strings (legacy) or objects (new)
                if (root.TryGetProperty("Inventory", out var pInv) && pInv.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in pInv.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                        {
                            result.Inventory.Add(new InventoryItem { Name = el.GetString() ?? "Unknown" });
                        }
                        else if (el.ValueKind == JsonValueKind.Object)
                        {
                            try
                            {
                                var item = JsonSerializer.Deserialize<InventoryItem>(el.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (item != null)
                                    result.Inventory.Add(item);
                            }
                            catch
                            {
                                // ignore malformed items
                            }
                        }
                    }
                }

                return result;
            }
            catch
            {
                // If everything fails, return a basic character
                return new Character { Name = name };
            }
        }
    }
}