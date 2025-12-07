using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DnDGame.Models;

namespace DnDGame.Services
{
    public class LoginService
    {
        private const string CharactersDir = "Data/Characters";
        private const string SessionFile = "Data/session.json";

        public Character? ShowLoginMenu()
        {
            Directory.CreateDirectory(CharactersDir);
            bool hasSession = File.Exists(SessionFile);

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== DnD-AI — Login ===");
                Console.WriteLine();

                int option = 1;
                if (hasSession)
                    Console.WriteLine($"{option++}. Continue last session");
                Console.WriteLine($"{option++}. Start new game (create character)");
                Console.WriteLine($"{option++}. Load existing character");
                Console.WriteLine($"{option++}. Exit");
                Console.WriteLine();
                Console.Write("Choose an option: ");
                string choice = (Console.ReadLine() ?? "").Trim();

                // Numeric selection handling
                if (int.TryParse(choice, out int n))
                {
                    if (hasSession)
                    {
                        switch (n)
                        {
                            case 1:
                                var session = ReadSession();
                                if (!string.IsNullOrEmpty(session?.PlayerName))
                                {
                                    var character = Character.Load(session.PlayerName);
                                    Console.WriteLine($"Continuing session as {character.Name}. Press Enter to continue...");
                                    Console.ReadLine();
                                    return character;
                                }
                                Console.WriteLine("Geen geldige sessie gevonden. Druk Enter om terug te gaan.");
                                Console.ReadLine();
                                break;
                            case 2:
                                return StartNewCharacter();
                            case 3:
                                return LoadExistingCharacter();
                            case 4:
                                return null;
                            default:
                                Console.WriteLine("Ongeldige keuze. Druk Enter om opnieuw te proberen.");
                                Console.ReadLine();
                                break;
                        }
                    }
                    else
                    {
                        switch (n)
                        {
                            case 1:
                                return StartNewCharacter();
                            case 2:
                                return LoadExistingCharacter();
                            case 3:
                                return null;
                            default:
                                Console.WriteLine("Ongeldige keuze. Druk Enter om opnieuw te proberen.");
                                Console.ReadLine();
                                break;
                        }
                    }

                    continue;
                }

                // Textual shortcuts
                var lower = choice.ToLowerInvariant();
                if (hasSession && (lower == "continue" || lower == "1"))
                {
                    var session = ReadSession();
                    if (!string.IsNullOrEmpty(session?.PlayerName))
                    {
                        var character = Character.Load(session.PlayerName);
                        Console.WriteLine($"Continuing session as {character.Name}. Press Enter to continue...");
                        Console.ReadLine();
                        return character;
                    }
                    Console.WriteLine("Geen geldige sessie gevonden. Druk Enter om terug te gaan.");
                    Console.ReadLine();
                    continue;
                }

                if (lower == "new" || lower == "startnewgame")
                    return StartNewCharacter();
                if (lower == "load" || lower == "existing")
                    return LoadExistingCharacter();
                if (lower == "exit" || lower == "quit")
                    return null;

                Console.WriteLine("Ongeldige invoer. Druk Enter om opnieuw te proberen.");
                Console.ReadLine();
            }
        }

        private Character StartNewCharacter()
        {
            while (true)
            {
                Console.Write("Voer een naam voor je character in: ");
                string name = (Console.ReadLine() ?? "").Trim();
                if (string.IsNullOrEmpty(name))
                {
                    Console.WriteLine("Naam mag niet leeg zijn. Probeer opnieuw.");
                    continue;
                }

                var character = new Character { Name = name };
                try
                {
                    Directory.CreateDirectory(CharactersDir);
                    character.Save();
                }
                catch
                {
                    // non-fatal, allow play to continue even if save fails
                    Console.WriteLine("Kon character niet opslaan. Doorgaan met nieuw character. Druk Enter.");
                    Console.ReadLine();
                }

                return character;
            }
        }

        private Character LoadExistingCharacter()
        {
            var files = Directory.GetFiles(CharactersDir, "*.json")
                                 .OrderBy(f => f)
                                 .ToArray();

            if (!files.Any())
            {
                Console.WriteLine("Geen bestaande characters gevonden. Druk Enter om een nieuw character aan te maken.");
                Console.ReadLine();
                return StartNewCharacter();
            }

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Bestaande characters:");
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(files[i]);
                    Console.WriteLine($"{i + 1}. {name}");
                }

                Console.WriteLine();
                Console.Write("Kies een nummer of typ een naam: ");
                Console.WriteLine("Type /'back/' om terug te gaan");
                string input = (Console.ReadLine() ?? "").Trim();

                if (int.TryParse(input, out int idx) && idx >= 1 && idx <= files.Length)
                {
                    string name = Path.GetFileNameWithoutExtension(files[idx - 1]);
                    return Character.Load(name);
                }

                if (!string.IsNullOrEmpty(input))
                {
                    return Character.Load(input);
                }
                if(input.ToLowerInvariant() == "back")
                {
                    return ShowLoginMenu()!;
                }

                Console.WriteLine("Ongeldige invoer. Probeer opnieuw.");
            }
        }

        private SessionData? ReadSession()
        {
            try
            {
                var json = File.ReadAllText(SessionFile);
                return JsonSerializer.Deserialize<SessionData>(json);
            }
            catch
            {
                return null;
            }
        }

        private class SessionData
        {
            public string? Story { get; set; }
            public string? StorySummary { get; set; }
            public string? PlayerName { get; set; }
        }
    }
}