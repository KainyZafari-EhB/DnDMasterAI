using System;
using System.IO;
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

        public GameEngine(Character player)
        {
            _player = player;

            if (File.Exists(SessionFile))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<SessionData>(File.ReadAllText(SessionFile));
                    story = saved?.Story ?? "Je ontwaakt in een donkere kerker. Je hoort een druppel vallen...";
                    storySummary = saved?.StorySummary ?? story;
                    Console.WriteLine("Vorige sessie geladen.\n");
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
            Console.WriteLine("Welkom bij DnD-AI! Typ 'exit' om te stoppen.\n");

            while (true)
            {
                Console.WriteLine($"\n{story}");
                Console.Write("\nWat doe je? > ");
                string action = Console.ReadLine() ?? "";

                if (action.Trim().ToLower() == "exit")
                {
                    SaveSession();
                    Console.WriteLine("Spel opgeslagen. Tot de volgende keer!");
                    break;
                }

                string prompt = BuildPrompt(action);
                string aiResponse = _aiService.AskAI(prompt);

                story = aiResponse.Trim();
                UpdateStorySummary(story);
            }
        }

        private string BuildPrompt(string action)
        {
            return $@"
                        Je bent een ervaren Dungeon Master in een Dungeons & Dragons-verhaal.
                        Het is jouw taak om het verhaal logisch en samenhangend voort te zetten op basis van de voorgaande gebeurtenissen.

                        Korte samenvatting van wat er eerder gebeurde (context):
                        {storySummary}

                        Statistieken van de speler:
                        HP: {_player.HP}, STR: {_player.Strength}, DEX: {_player.Dexterity}, INT: {_player.Intelligence}

                        De speler doet het volgende: ""{action}""

                        Beperkingen en regels:
                        - Blijf consistent met eerdere gebeurtenissen en personages.
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

        private class SessionData
        {
            public string? Story { get; set; }
            public string? StorySummary { get; set; }
            public string? PlayerName { get; set; }
        }
    }
}
