using System;
using DnDGame.Models;
using System.Collections.Generic;

namespace DnDGame.Services
{
    public class EncounterService
    {
        private readonly Random _rng = new();

        public (Enemy enemy, InventoryItem[] loot) GenerateEncounter(Character player)
        {
            // Simple scaling: enemy stats based on player stats with some randomness.
            int baseHp = Math.Max(3, player.HP / 2 + _rng.Next(1, 5));
            var enemy = new Enemy
            {
                Name = PickEnemyName(),
                HP = baseHp,
                Strength = Math.Max(4, player.Strength - _rng.Next(0, 3)),
                Dexterity = Math.Max(4, player.Dexterity - _rng.Next(0, 3)),
                Intelligence = Math.Max(3, player.Intelligence - _rng.Next(0, 4))
            };

            // Loot generation: 50% chance to drop 0..2 items
            var loot = new List<InventoryItem>();
            if (_rng.NextDouble() < 0.5)
            {
                int count = _rng.Next(1, 3);
                for (int i = 0; i < count; i++)
                    loot.Add(GenerateLootItem(player));
            }

            return (enemy, loot.ToArray());
        }

        private InventoryItem GenerateLootItem(Character player)
        {
            var names = new[] { "Ruwe dolk", "Stalen helm", "Kleine bontmantel", "Zeldzame ertsvijl", "Mysterieuze armband" };
            var name = names[_rng.Next(names.Length)];
            var rarity = (Rarity)_rng.Next(0, Enum.GetNames(typeof(Rarity)).Length);

            return new InventoryItem
            {
                Name = name,
                Description = $"Een {name.ToLower()} gevonden tijdens gevecht.",
                Rarity = rarity,
                Weight = Math.Round(_rng.NextDouble() * 2.5, 2),
                Value = 5 + _rng.Next(0, 20)
            };
        }

        private string PickEnemyName()
        {
            var names = new[] { "Goblin", "Skerpioen", "Plunderende dief", "Verwilderde wolf" };
            return names[_rng.Next(names.Length)];
        }
    }
}