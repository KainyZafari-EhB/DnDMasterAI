using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DnDGame.Models;

namespace DnDGame.Services
{
    public class GameEngine
    {
        private readonly AIService _aiService = new();
        private readonly EncounterService _encounterService = new();
        private readonly Character _player;
        private string story;
        private string storySummary;
        private const int MaxSummaryLength = 1200;
        private const string SessionFile = "Data/session.json";
        private List<string> _activeNpcs = new();

        private static readonly string[] StoryStarts = new[]
        {
            "Je ontwaakt in een donkere kerker. Je hoort een druppel vallen...",
            "De ochtendmist trekt op over een verlaten marktplein; iemand heeft iets achtergelaten bij de fontein.",
            "Je wordt wakker op het dek van een schip dat zachtjes dobbert op onbekend water.",
            "In het schemerdonker van een oude bibliotheek valt er een boek open dat nooit eerder werd aangeraakt.",
            "Je grijpt het zwaard dat in de rots steekt; de runen beginnen net te gloeien.",
            "Een smalle doorgang in een grot leidt naar een kamer verlicht door blauwachtige kristallen."
        };

        public GameEngine(Character player)
        {
            _player = player;

            if (File.Exists(SessionFile))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<SessionData>(File.ReadAllText(SessionFile));
                    story = saved?.Story ?? GetRandomStart();
                    storySummary = saved?.StorySummary ?? story;
                    Console.WriteLine("Vorige sessie geladen.\n");
                }
                catch
                {
                    story = GetRandomStart();
                    storySummary = story;
                }
            }
            else
            {
                story = GetRandomStart();
                storySummary = story;
            }

            ExtractNpcsFromStory(storySummary);
        }

        public void Run()
        {
            Console.WriteLine("Welkom bij DnD-AI! Typ 'exit' om te stoppen.\n");

            while (true)
            {
                Console.WriteLine($"\n{story}");
                Console.Write("\nWat doe je? > ");
                string action = Console.ReadLine() ?? "";
                string actionTrimmed = action.Trim();
                string lowerAction = actionTrimmed.ToLowerInvariant();

                if (lowerAction == "exit")
                {
                    SaveSession();
                    Console.WriteLine("Spel opgeslagen. Tot de volgende keer!");
                    break;
                }
                if (lowerAction == "startnewgame")
                {
                    EraseSession();
                    Console.WriteLine("Spel opnieuw gestart..");
                    break;
                }

                // Show inventory command
                if (lowerAction == "inventory" || lowerAction == "inv")
                {
                    var inv = _player.Inventory != null && _player.Inventory.Any()
                        ? string.Join(", ", _player.Inventory.Select(i => i.Name))
                        : "(lege inventaris)";
                    Console.WriteLine($"\nInventaris van {_player.Name}: {inv}");
                    continue;
                }

                // Pickup commands (english/dutch)
                if (lowerAction.StartsWith("pick up ") || lowerAction.StartsWith("pickup ") ||
                    lowerAction.StartsWith("neem ") || lowerAction.StartsWith("pak "))
                {
                    // Determine start index of item name in original (preserve casing)
                    int firstSpace = actionTrimmed.IndexOf(' ');
                    if (firstSpace >= 0)
                    {
                        // find index after the command word(s)
                        string itemName = actionTrimmed.Substring(actionTrimmed.IndexOf(' ') + 1).Trim();
                        // for "pick up" we need to strip the second word if present
                        if (itemName.StartsWith("up ", StringComparison.InvariantCultureIgnoreCase))
                            itemName = itemName.Substring(3).Trim();

                        if (string.IsNullOrEmpty(itemName))
                        {
                            Console.WriteLine("Wat wil je oppakken? Geef een itemnaam op.");
                            continue;
                        }

                        _player.Inventory ??= new List<InventoryItem>();
                        _player.Inventory.Add(new InventoryItem { Name = itemName });
                        try
                        {
                            _player.Save();
                        }
                        catch
                        {
                            // ignore save errors for now
                        }

                        Console.WriteLine($"Je hebt '{itemName}' opgepakt en toegevoegd aan je inventaris.");
                        continue;
                    }
                }

                // Attack commands: validate target is present
                if (lowerAction.StartsWith("attack ") || lowerAction.StartsWith("val aan ") || lowerAction.StartsWith("sla "))
                {
                    string targetName = ExtractTargetFromAttackCommand(actionTrimmed);
                    if (string.IsNullOrEmpty(targetName))
                    {
                        Console.WriteLine("Wie wil je aanvallen? Geef een naam op.");
                        continue;
                    }

                    if (!IsNpcPresent(targetName))
                    {
                        Console.WriteLine($"'{targetName}', kan ik hier niet vinden.");
                        continue;
                    }

                    var (enemy, loot) = _encounterService.GenerateEncounter(_player);
                    enemy.Name = targetName;
                    string encounterText = ResolveEncounter(enemy, loot);
                    story = encounterText;
                    UpdateStorySummary(story);
                    ExtractNpcsFromStory(story);
                    continue;
                }

                string prompt = BuildPrompt(actionTrimmed);
                string aiResponse = _aiService.AskAI(prompt);

                story = aiResponse.Trim();
                UpdateStorySummary(story);
                ExtractNpcsFromStory(story);
            }
        }

        private string BuildPrompt(string action)
        {
            var inventoryString = _player.Inventory != null && _player.Inventory.Any()
                ? string.Join(", ", _player.Inventory.Select(i => i.Name))
                : "(lege inventaris)";

            var activeNpcsString = _activeNpcs.Any() ? string.Join(", ", _activeNpcs) : "geen";

            return $@"Je bent een ervaren Dungeon Master in een Dungeons & Dragons-verhaal.
            Het is jouw taak om het verhaal logisch en samenhangend voort te zetten op basis van de voorgaande gebeurtenissen.

            Korte samenvatting van wat er eerder gebeurde (context):
            {storySummary}

            Statistieken van de speler:
            HP: {_player.HP}, STR: {_player.Strength}, DEX: {_player.Dexterity}, INT: {_player.Intelligence}

            Huidige aanwezige NPC's/vijanden: {activeNpcsString}

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
            - Introduceer af en toe vijanden of interessante NPC's als dit logisch aansluit bij het verhaal en de omgeving.
            - Vermeld duidelijk de namen van NPC's/vijanden die aanwezig zijn zodat de speler weet tegen wie hij kan aanvallen.

            Geef een samenhangende voortzetting van het verhaal in maximaal 5 zinnen.
            Praat met de gebruiker in het nederlands.";
        }

        private string ExtractTargetFromAttackCommand(string action)
        {
            var prefixes = new[] { "attack ", "val aan ", "sla " };
            foreach (var prefix in prefixes)
            {
                if (action.ToLowerInvariant().StartsWith(prefix))
                {
                    return action.Substring(prefix.Length).Trim();
                }
            }
            return string.Empty;
        }

        private bool IsNpcPresent(string npcName)
        {
            return _activeNpcs.Any(npc => npc.Equals(npcName, StringComparison.InvariantCultureIgnoreCase));
        }

        private void ExtractNpcsFromStory(string text)
        {
            // Simple heuristic: look for capitalized words that might be NPC names
            // In a real implementation, you'd parse the story more intelligently or have the AI provide NPC info
            _activeNpcs.Clear();

            // Common enemy/NPC names to look for
            var commonNames = new[]
            {
                "Goblin", "Skerpioen", "Plunderende dief", "Verwilderde wolf",
                "Orc", "Zombie", "Geest", "Draak", "Trol", "Bandit", "Ridder",
                "Koopman", "Soldaat", "Priester", "Heks", "Dief", "Bard"
            };

            var textLower = text.ToLowerInvariant();
            foreach (var name in commonNames)
            {
                if (textLower.Contains(name.ToLowerInvariant()))
                    _activeNpcs.Add(name);
            }
        }

        private string ResolveEncounter(Enemy enemy, InventoryItem[] loot)
        {
            var rng = new Random();
            var narrative = $"Je gaat aanvallen op {enemy.Name}! HP: {enemy.HP}, STR: {enemy.Strength}.\n";
            int enemyHp = enemy.HP;
            int playerHp = _player.HP;

            // Simple turn-based auto-resolve: player strikes first
            while (enemyHp > 0 && playerHp > 0)
            {
                // player attack
                int playerDamage = Math.Max(1, _player.Strength / 2 + rng.Next(0, _player.Strength / 2 + 1));
                enemyHp -= playerDamage;

                narrative += $"Je slaat {enemy.Name} voor {playerDamage} schade. (Enemy HP: {Math.Max(0, enemyHp)})\n";
                if (enemyHp <= 0)
                    break;

                // enemy attack
                int enemyDamage = Math.Max(1, enemy.Strength / 2 + rng.Next(0, enemy.Strength / 2 + 1));
                playerHp -= enemyDamage;
                narrative += $"{enemy.Name} raakt jou voor {enemyDamage} schade. (Jouw HP: {Math.Max(0, playerHp)})\n";
            }

            if (playerHp <= 0)
            {
                // Player defeated: minimal handling (reduce to 1 HP and continue)
                _player.HP = 1;
                narrative += "Je bent verslagen, maar op wonderbaarlijke wijze blijf je leven. Je ontwaakt later met 1 HP.\n";
            }
            else
            {
                _player.HP = playerHp;
                narrative += $"Je hebt {enemy.Name} verslagen!\n";

                if (loot != null && loot.Length > 0)
                {
                    narrative += "Je ziet de volgende buit: " + string.Join(", ", loot.Select(l => l.Name)) + ".\n";
                    Console.WriteLine(narrative);
                    Console.Write("Wil je de buit opnemen? (j/n) > ");
                    string pick = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                    if (pick == "j" || pick == "ja" || pick == "y")
                    {
                        _player.Inventory ??= new List<InventoryItem>();
                        foreach (var item in loot)
                            _player.Inventory.Add(item);

                        try
                        {
                            _player.Save();
                        }
                        catch
                        {
                            // ignore save errors
                        }

                        narrative += "Buit toegevoegd aan je inventaris.\n";
                    }
                    else
                    {
                        narrative += "Je laat de buit liggen.\n";
                    }
                }
                else
                {
                    narrative += "Er is geen buit gevonden.\n";
                }
            }

            // Save session state after encounter
            try
            {
                _player.Save();
                SaveSession();
            }
            catch
            {
                // ignore save errors
            }

            return narrative.Trim();
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

            story = GetRandomStart();
            storySummary = story;
            _activeNpcs.Clear();
        }

        private string GetRandomStart()
        {
            return StoryStarts[Random.Shared.Next(StoryStarts.Length)];
        }

        private class SessionData
        {
            public string? Story { get; set; }
            public string? StorySummary { get; set; }
            public string? PlayerName { get; set; }
        }
    }
}
