using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using DnDGame.Models;

namespace DnDGame.Services
{
    public class GameEngine
    {
        private readonly AIService _aiService = new();
        private readonly CombatService _combatService = new();
        private readonly EncounterService _encounterService = new();

        private readonly Character _player;
        private string story;
        private string storySummary;
        private const int MaxSummaryLength = 1200;
        private const string SessionFile = "Data/session.json";

        private readonly ConsoleColor _defaultForeground;
        private readonly ConsoleColor _defaultBackground;

        public GameEngine(Character player)
        {
            _player = player;

            _defaultForeground = Console.ForegroundColor;
            _defaultBackground = Console.BackgroundColor;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "DnD-AI";

            if (File.Exists(SessionFile))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<SessionData>(File.ReadAllText(SessionFile));
                    story = saved?.Story ?? "Je ontwaakt in een donkere kerker. Je hoort een druppel vallen...";
                    storySummary = saved?.StorySummary ?? story;
                    WriteLineColor("Vorige sessie geladen.\n", ConsoleColor.DarkGray);
                }
                catch
                {
                    story = "Je ontwaakt in een donkere kerker. Je hoort een druppel vallen...";
                    storySummary = story;
                }
            }
            else
            {
                story = "Je ontwaakt in een donkere kerker. Je hoort een druppel vallen...";
                storySummary = story;
            }
        }

        public void Run()
        {
            WriteLineColor("Welkom bij DnD-AI! Typ 'exit' om te stoppen.", ConsoleColor.Green);
            WriteLineColor("(Typ 'inventory' of 'inv' om je inventaris te bekijken, 'startnewgame' om opnieuw te beginnen)\n", ConsoleColor.DarkGray);

            while (true)
            {
                WriteLineColor($"\n{story}", ConsoleColor.Cyan);

                WriteColor("\nWat doe je? > ", ConsoleColor.Yellow);
                Console.ForegroundColor = _defaultForeground;
                string action = Console.ReadLine() ?? "";
                string actionTrimmed = action.Trim();
                string lowerAction = actionTrimmed.ToLowerInvariant();

                if (lowerAction == "exit")
                {
                    SaveSession();
                    WriteLineColor("Spel opgeslagen. Tot de volgende keer!", ConsoleColor.Green);
                    ResetColors();
                    break;
                }
                if (lowerAction == "startnewgame")
                {
                    EraseSession();
                    WriteLineColor("Spel opnieuw gestart..", ConsoleColor.Yellow);
                    ResetColors();
                    break;
                }

                // Show inventory command
                if (lowerAction == "inventory" || lowerAction == "inv")
                {
                    var inv = _player.Inventory != null && _player.Inventory.Any()
                        ? string.Join(Environment.NewLine, _player.Inventory.Select((it, i) => $"{i + 1}. {it.Name} ({it.Rarity}) - {it.Description}"))
                        : "(lege inventaris)";
                    WriteLineColor($"\nInventaris van {_player.Name}: ", ConsoleColor.Magenta, false);
                    WriteLineColor(inv, ConsoleColor.White);
                    continue;
                }

                // Pickup commands (english/dutch) - create a simple item with name when user manually picks up
                if (lowerAction.StartsWith("pick up ") || lowerAction.StartsWith("pickup ") ||
                    lowerAction.StartsWith("neem ") || lowerAction.StartsWith("pak "))
                {
                    int firstSpace = actionTrimmed.IndexOf(' ');
                    if (firstSpace >= 0)
                    {
                        string itemName = actionTrimmed.Substring(actionTrimmed.IndexOf(' ') + 1).Trim();
                        if (itemName.StartsWith("up ", StringComparison.InvariantCultureIgnoreCase))
                            itemName = itemName.Substring(3).Trim();

                        if (string.IsNullOrEmpty(itemName))
                        {
                            WriteLineColor("Wat wil je oppakken? Geef een itemnaam op.", ConsoleColor.Yellow);
                            continue;
                        }

                        _player.Inventory ??= new List<InventoryItem>();
                        var newItem = new InventoryItem { Name = itemName, Description = $"Een {itemName.ToLower()} die je oppakte.", Rarity = Rarity.Common };
                        _player.Inventory.Add(newItem);
                        try
                        {
                            _player.Save();
                        }
                        catch
                        {
                            // ignore save errors for now
                        }

                        WriteLineColor($"Je hebt '{itemName}' opgepakt en toegevoegd aan je inventaris.", ConsoleColor.Green);
                        continue;
                    }
                }

                // Search / explore command that can trigger an encounter
                if (lowerAction == "search" || lowerAction == "zoek" || lowerAction == "explore")
                {
                    var (enemy, loot) = _encounterService.GenerateEncounter(_player);
                    WriteLineColor($"Je komt een {enemy.Name} tegen! (HP: {enemy.HP})", ConsoleColor.Red);

                    bool continueGame = HandleEncounter(enemy, loot);
                    if (!continueGame)
                        return;

                    story = $"Je hebt een encounter met {enemy.Name} gehad.";
                    UpdateStorySummary(story);
                    continue;
                }

                string prompt = BuildPrompt(actionTrimmed);

                // Inform the player that the AI is working and guard against empty responses.
                WriteLineColor("De Dungeon Master denkt na...", ConsoleColor.DarkGray);
                string aiResponse = _aiService.AskAI(prompt) ?? "";
                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    aiResponse = "De omgeving blijft stil; er gebeurt momenteel niets merkwaardigs.";
                }

                story = aiResponse.Trim();
                UpdateStorySummary(story);

                // If the new story suggests an encounter (movement into an environment or explicit encounter phrasing),
                // trigger an encounter automatically.
                if (ShouldTriggerEncounter(story, actionTrimmed))
                {
                    var (enemy, loot) = _encounterService.GenerateEncounter(_player);
                    WriteLineColor($"Je komt een {enemy.Name} tegen! (HP: {enemy.HP})", ConsoleColor.Red);

                    bool continueGame = HandleEncounter(enemy, loot);
                    if (!continueGame)
                        return;

                    story = $"Je hebt een encounter met {enemy.Name} gehad.";
                    UpdateStorySummary(story);
                    continue;
                }
            }
        }

        private string BuildPrompt(string action)
        {
            // Do not expose the full inventory contents to the AI prompt in a way that will make them show up
            // constantly in the console. Provide only a small summary so the AI can account for items without
            // echoing every item name into the story output.
            var inventoryString = _player.Inventory;

            return $@"
                        Je bent een ervaren Dungeon Master in een Dungeons & Dragons-verhaal.
                        Het is jouw taak om het verhaal logisch en samenhangend voort te zetten op basis van de voorgaande gebeurtenissen.

                        Korte samenvatting van wat er eerder gebeurde (context):
                        {storySummary}

                        Statistieken van de speler:
                        HP: {_player.HP}, STR: {_player.Strength}, DEX: {_player.Dexterity}, INT: {_player.Intelligence}

                        Hou rekening met de inventaris: {inventoryString} en zorg dat er niks kan gebeuren uit het niets.
                        Zorg ervoor dat er af en toe items te vinden vallen of dat er na combat items droppen,
                        en dat je daarna zegt wat voor items gedropped zijn. Als de player ervoor kiest om deze op te nemen,
                        voeg deze toe aan de inventaris van de speler.

                        De speler doet het volgende: ""{action}""

                        Beperkingen en regels:
                        - Blijf consistent met eerdere gebeurtenissen en personages.
                        - Zorg ervoor dat het verhaal vooruitgaat maar niet te snel.
                        - Introduceer geen nieuwe elementen zonder logische aanleiding.
                        - Als de speler iets onmogelijks probeert, beschrijf wat er in plaats daarvan gebeurt.
                        - Beschrijf uitsluitend wat er NU gebeurt, niet wat er later zal gebeuren.
                        - Houd de toon avontuurlijk, maar realistisch binnen een fantasysetting.

                        Geef een samenhangende voortzetting van het verhaal in maximaal 5 zinnen.
                        Praat met de gebruiker in het nederlands.
                        ";
        }

        private void UpdateStorySummary(string newText)
        {
            storySummary += " " + newText;
            if (storySummary.Length > MaxSummaryLength)
            {
                storySummary = storySummary.Substring(storySummary.Length - MaxSummaryLength);
                int firstPeriod = storySummary.IndexOf('.');
                if (firstPeriod >= 0)
                    storySummary = storySummary.Substring(firstPeriod + 1).Trim();
            }
        }

        private void SaveSession()
        {
            Directory.CreateDirectory("Data");
            var data = new SessionData
            {
                Story = story,
                StorySummary = storySummary,
                PlayerName = _player.Name
            };

            File.WriteAllText(SessionFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        private void EraseSession()
        {
            try
            {
                if (File.Exists(SessionFile))
                    File.Delete(SessionFile);
            }
            catch
            {
                // ignore deletion errors
            }

            story = "Je ontwaakt in een donkere kerker. Je hoort een druppel vallen...";
            storySummary = story;
        }

        private class SessionData
        {
            public string? Story { get; set; }
            public string? StorySummary { get; set; }
            public string? PlayerName { get; set; }
        }

        // Helper methods for colored output
        private void WriteLineColor(string text, ConsoleColor color, bool newLine = true)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (newLine)
                Console.WriteLine(text);
            else
                Console.Write(text);
            Console.ForegroundColor = prev;
        }

        private void WriteColor(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = prev;
        }

        private void ResetColors()
        {
            Console.ForegroundColor = _defaultForeground;
            Console.BackgroundColor = _defaultBackground;
        }

        // New: decide whether the generated story and player's action imply an encounter
        private bool ShouldTriggerEncounter(string newStory, string action)
        {
            if (string.IsNullOrWhiteSpace(newStory))
                return false;

            var storyLower = newStory.ToLowerInvariant();
            var actionLower = (action ?? "").ToLowerInvariant();

            // explicit encounter phrasing (nl/en)
            var encounterPhrases = new[]
            {
                "komt een", "je komt een", "ontmoet", "tegenkomt", "er verschijnt", "verschijnt",
                "a wild", "you encounter", "encounter", "you meet", "stuit op", "je stuit"
            };
            if (encounterPhrases.Any(p => storyLower.Contains(p)))
                return true;

            // environment words that could cause an encounter after movement/exploration
            var environmentWords = new[]
            {
                "bos", "forest", "grot", "cave", "woestijn", "desert", "ruïne", "ruins", "brug", "bridge",
                "kerker", "dungeon", "veld", "meadow", "moeras", "swamp", "berg", "mountain", "stad", "city", "jungle"
            };

            // movement verbs / entering verbs (nl/en). If action suggests movement and story mentions environment -> trigger.
            var movementVerbs = new[]
            {
                "walk", "walk into", "enter", "go to", "ga naar", "loop", "loop naar", "betreed", "verlaat", "move", "travel", "wandelen"
            };

            if (movementVerbs.Any(m => actionLower.Contains(m)) && environmentWords.Any(e => storyLower.Contains(e)))
                return true;

            // also if player explicitly searched/explored we handled earlier, so no need to check that here.

            return false;
        }

        // New: extracted encounter handling logic. Returns true to continue the game, false to end session (player defeated)
        private bool HandleEncounter(Enemy enemy, InventoryItem[] loot)
        {
            // ask player to fight or flee
            while (true)
            {
                WriteLineColor("Wil je vechten (v) of vluchten (f)?", ConsoleColor.Yellow, false);
                Console.ForegroundColor = _defaultForeground;
                string choice = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                if (choice == "v" || choice == "vecht" || choice == "fight")
                {
                    var combatResult = _combatService.Engage(_player, enemy, loot);
                    foreach (var line in combatResult.Log)
                        WriteLineColor(line, ConsoleColor.Gray);

                    _player.HP = combatResult.PlayerRemainingHP;
                    try
                    {
                        _player.Save();
                    }
                    catch { }

                    if (combatResult.Winner == _player.Name)
                    {
                        WriteLineColor("Je hebt gewonnen!", ConsoleColor.Green);
                        if (combatResult.Loot != null && combatResult.Loot.Length > 0)
                        {
                            WriteLineColor("Je vindt de volgende spullen:", ConsoleColor.Magenta);
                            for (int i = 0; i < combatResult.Loot.Length; i++)
                            {
                                var it = combatResult.Loot[i];
                                WriteLineColor($"{i + 1}. {it.Name} ({it.Rarity}) - {it.Description}", ConsoleColor.White);
                            }

                            // offer pickup
                            while (true)
                            {
                                WriteLineColor("Wil je alles meenemen? (y/n)", ConsoleColor.Yellow, false);
                                Console.ForegroundColor = _defaultForeground;
                                string takeAll = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                                if (takeAll == "y" || takeAll == "yes" || takeAll == "ja")
                                {
                                    _player.Inventory ??= new List<InventoryItem>();
                                    _player.Inventory.AddRange(combatResult.Loot);
                                    try { _player.Save(); } catch { }
                                    WriteLineColor("Alle items toegevoegd aan je inventaris.", ConsoleColor.Green);
                                    break;
                                }
                                if (takeAll == "n" || takeAll == "no" || takeAll == "nee")
                                {
                                    WriteLineColor("Typ nummers gescheiden door komma's om items te pakken (bijv. 1,3) of 'none' om niks te nemen.", ConsoleColor.Yellow);
                                    Console.ForegroundColor = _defaultForeground;
                                    string sel = (Console.ReadLine() ?? "").Trim();
                                    if (sel.ToLowerInvariant() == "none" || string.IsNullOrEmpty(sel))
                                    {
                                        WriteLineColor("Je laat de items liggen.", ConsoleColor.DarkGray);
                                        break;
                                    }

                                    var parts = sel.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                                    _player.Inventory ??= new List<InventoryItem>();
                                    foreach (var p in parts)
                                    {
                                        if (int.TryParse(p, out int idx) && idx >= 1 && idx <= combatResult.Loot.Length)
                                        {
                                            _player.Inventory.Add(combatResult.Loot[idx - 1]);
                                            WriteLineColor($"Opgenomen: {combatResult.Loot[idx - 1].Name}", ConsoleColor.Green);
                                        }
                                    }

                                    try { _player.Save(); } catch { }
                                    break;
                                }

                                WriteLineColor("Ongeldige invoer. Beantwoord met y of n.", ConsoleColor.Yellow);
                            }
                        }
                    }
                    else
                    {
                        WriteLineColor("Je bent verslagen...", ConsoleColor.Red);
                        WriteLineColor("Einde van de sessie. Je kunt opnieuw beginnen.", ConsoleColor.Yellow);
                        EraseSession();
                        if (_player.Inventory != null)
                            _player.Inventory.Clear();
                        ResetColors();
                        return false;
                    }

                    break;
                }
                else if (choice == "f" || choice == "vlucht" || choice == "run")
                {
                    var rng = new Random();
                    int fleeRoll = rng.Next(1, 21) + _player.Dexterity / 2;
                    int enemyRoll = rng.Next(1, 21) + enemy.Dexterity / 2;
                    if (fleeRoll >= enemyRoll)
                    {
                        WriteLineColor("Je rent succesvol weg.", ConsoleColor.Green);
                    }
                    else
                    {
                        WriteLineColor("Je probeert te vluchten maar faalt; het gevecht begint!", ConsoleColor.Red);
                        var combatResult = _combatService.Engage(_player, enemy, loot);
                        foreach (var line in combatResult.Log)
                            WriteLineColor(line, ConsoleColor.Gray);

                        _player.HP = combatResult.PlayerRemainingHP;
                        try { _player.Save(); } catch { }

                        if (combatResult.Winner == _player.Name)
                        {
                            WriteLineColor("Je hebt gewonnen!", ConsoleColor.Green);
                            if (combatResult.Loot != null && combatResult.Loot.Length > 0)
                            {
                                WriteLineColor("Je vindt de volgende spullen:", ConsoleColor.Magenta);
                                for (int i = 0; i < combatResult.Loot.Length; i++)
                                    WriteLineColor($"{i + 1}. {combatResult.Loot[i].Name} ({combatResult.Loot[i].Rarity}) - {combatResult.Loot[i].Description}", ConsoleColor.White);

                                WriteLineColor("Typ 'pickup <naam>' om items handmatig op te pakken, of 'inventory' om je inventaris te bekijken.", ConsoleColor.DarkGray);
                            }
                        }
                        else
                        {
                            WriteLineColor("Je bent verslagen...", ConsoleColor.Red);
                            WriteLineColor("Einde van de sessie. Je kunt opnieuw beginnen.", ConsoleColor.Yellow);
                            EraseSession();
                            ResetColors();
                            return false;
                        }
                    }

                    break;
                }

                WriteLineColor("Ongeldige keuze. Typ 'v' om te vechten of 'f' om te vluchten.", ConsoleColor.Yellow);
            }

            return true;
        }
    }
}
