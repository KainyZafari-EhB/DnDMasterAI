using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DnDGame.Models;

namespace DnDGame.Services
{
    public class GameEngine
    {
        private readonly AIService _aiService = new();
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
                string 
                actionTrimmed = action.Trim();
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
                        ? string.Join(", ", _player.Inventory)
                        : "(lege inventaris)";
                    WriteLineColor($"\nInventaris van {_player.Name}: ", ConsoleColor.Magenta, false);
                    WriteLineColor(inv, ConsoleColor.White);
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
                            WriteLineColor("Wat wil je oppakken? Geef een itemnaam op.", ConsoleColor.Yellow);
                            continue;
                        }

                        _player.Inventory ??= new System.Collections.Generic.List<string>();
                        _player.Inventory.Add(itemName);
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

                string prompt = BuildPrompt(actionTrimmed);
                string aiResponse = _aiService.AskAI(prompt);

                story = aiResponse.Trim();
                UpdateStorySummary(story);
            }
        }

        private string BuildPrompt(string action)
        {
            var inventoryString = _player.Inventory != null && _player.Inventory.Any()
                ? string.Join(", ", _player.Inventory)
                : "(lege inventaris)";

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
    }
}
