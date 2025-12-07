using System;
using DnDGame.Models;
using System.Collections.Generic;

namespace DnDGame.Services
{
    public class CombatService
    {
        private readonly Random _rng = new();

        public CombatResult Engage(Character player, Enemy enemy, InventoryItem[]? potentialLoot = null)
        {
            var result = new CombatResult
            {
                PlayerRemainingHP = player.HP,
                EnemyRemainingHP = enemy.HP
            };

            bool playerTurn = true;
            while (result.PlayerRemainingHP > 0 && result.EnemyRemainingHP > 0)
            {
                if (playerTurn)
                {
                    int attackRoll = Roll(20) + (player.Strength / 2);
                    int defenseRoll = Roll(20) + (enemy.Dexterity / 2);
                    if (attackRoll > defenseRoll)
                    {
                        int dmg = Roll(6) + Math.Max(1, player.Strength / 4);
                        result.EnemyRemainingHP = Math.Max(0, result.EnemyRemainingHP - dmg);
                        result.Log.Add($"{player.Name} treft {enemy.Name} voor {dmg} schade. ({result.EnemyRemainingHP} HP over)");
                    }
                    else
                    {
                        result.Log.Add($"{player.Name} mist {enemy.Name}.");
                    }
                }
                else
                {
                    int attackRoll = Roll(20) + (enemy.Strength / 2);
                    int defenseRoll = Roll(20) + (player.Dexterity / 2);
                    if (attackRoll > defenseRoll)
                    {
                        int dmg = Roll(4) + Math.Max(0, enemy.Strength / 4);
                        result.PlayerRemainingHP = Math.Max(0, result.PlayerRemainingHP - dmg);
                        result.Log.Add($"{enemy.Name} raakt {player.Name} voor {dmg} schade. ({result.PlayerRemainingHP} HP over)");
                    }
                    else
                    {
                        result.Log.Add($"{enemy.Name} mist {player.Name}.");
                    }
                }

                playerTurn = !playerTurn;
            }

            result.Winner = result.PlayerRemainingHP > 0 ? player.Name : enemy.Name;

            // if player won, attach possible loot
            if (result.PlayerRemainingHP > 0 && potentialLoot != null && potentialLoot.Length > 0)
                result.Loot = potentialLoot;

            return result;
        }

        private int Roll(int sides) => _rng.Next(1, sides + 1);
    }
}