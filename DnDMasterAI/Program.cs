using System;
using System.IO;
using System.Linq;
using System.Text;
using DnDGame.Models;
using DnDGame.Services;

Console.OutputEncoding = Encoding.UTF8;
Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.Gray;
Console.Clear();

while (true)
{
    Console.Clear();
    WriteLineColor("=== DnD-AI Character Menu ===", ConsoleColor.Cyan);
    WriteLineColor("1. Nieuwe character aanmaken", ConsoleColor.White);
    WriteLineColor("2. Bestaande character laden", ConsoleColor.White);
    WriteLineColor("3. Character verwijderen", ConsoleColor.Red);
    WriteLineColor("4. Afsluiten", ConsoleColor.DarkGray);
    WriteColor("\nKies een optie: ", ConsoleColor.Yellow);

    string choice = Console.ReadLine()?.Trim() ?? "";

    if (choice == "1")
    {
        CreateNewCharacter();
        break;
    }
    else if (choice == "2")
    {
        if (LoadExistingCharacter())
            break;
    }
    else if (choice == "3")
    {
        DeleteCharacter();
    }
    else if (choice == "4")
    {
        WriteLineColor("Tot ziens!", ConsoleColor.Green);
        return;
    }
    else
    {
        WriteLineColor("Ongeldige keuze. Probeer opnieuw.", ConsoleColor.Red);
        Console.ReadKey();
    }
}

void CreateNewCharacter()
{
    Console.Clear();
    WriteLineColor("=== Nieuwe Character Aanmaken ===", ConsoleColor.Cyan);
    WriteColor("Naam: ", ConsoleColor.Yellow);
    string name = Console.ReadLine()?.Trim() ?? "Hero";

    if (string.IsNullOrEmpty(name))
        name = "Hero";

    var player = new Character { Name = name };
    player.Save();

    WriteLineColor($"\nCharacter '{name}' aangemaakt!", ConsoleColor.Green);
    Console.ReadKey();

    var engine = new GameEngine(player);
    engine.Run();
}

bool LoadExistingCharacter()
{
    Console.Clear();
    string charDir = "Data/Characters";

    if (!Directory.Exists(charDir) || !Directory.GetFiles(charDir, "*.json").Any())
    {
        WriteLineColor("Geen characters gevonden. Maak eerst een character aan.", ConsoleColor.Red);
        Console.ReadKey();
        return false;
    }

    WriteLineColor("=== Bestaande Characters ===", ConsoleColor.Cyan);
    var files = Directory.GetFiles(charDir, "*.json");

    for (int i = 0; i < files.Length; i++)
    {
        string charName = Path.GetFileNameWithoutExtension(files[i]);
        WriteLineColor($"{i + 1}. {charName}", ConsoleColor.White);
    }

    WriteColor("\nKies een character (nummer): ", ConsoleColor.Yellow);
    string input = Console.ReadLine()?.Trim() ?? "";

    if (int.TryParse(input, out int index) && index > 0 && index <= files.Length)
    {
        string selectedName = Path.GetFileNameWithoutExtension(files[index - 1]);
        var player = Character.Load(selectedName);

        WriteLineColor($"\nCharacter '{selectedName}' geladen!", ConsoleColor.Green);
        Console.ReadKey();

        var engine = new GameEngine(player);
        engine.Run();
        return true;
    }
    else
    {
        WriteLineColor("Ongeldige keuze.", ConsoleColor.Red);
        Console.ReadKey();
        return false;
    }
}

void DeleteCharacter()
{
    Console.Clear();
    string charDir = "Data/Characters";

    if (!Directory.Exists(charDir) || !Directory.GetFiles(charDir, "*.json").Any())
    {
        WriteLineColor("Geen characters gevonden om te verwijderen.", ConsoleColor.Red);
        Console.ReadKey();
        return;
    }

    WriteLineColor("=== Characters Verwijderen ===", ConsoleColor.Red);
    var files = Directory.GetFiles(charDir, "*.json");

    for (int i = 0; i < files.Length; i++)
    {
        string charName = Path.GetFileNameWithoutExtension(files[i]);
        WriteLineColor($"{i + 1}. {charName}", ConsoleColor.White);
    }

    WriteLineColor("0. Annuleren", ConsoleColor.DarkGray);
    WriteColor("\nKies een character om te verwijderen (nummer): ", ConsoleColor.Yellow);
    string input = Console.ReadLine()?.Trim() ?? "";

    if (input == "0")
    {
        WriteLineColor("Verwijderen geannuleerd.", ConsoleColor.DarkGray);
        Console.ReadKey();
        return;
    }

    if (int.TryParse(input, out int index) && index > 0 && index <= files.Length)
    {
        string selectedFile = files[index - 1];
        string selectedName = Path.GetFileNameWithoutExtension(selectedFile);

        WriteColor($"\nWeet je zeker dat je '{selectedName}' wilt verwijderen? (ja/nee): ", ConsoleColor.Red);
        string confirm = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";

        if (confirm == "ja" || confirm == "yes" || confirm == "j" || confirm == "y")
        {
            try
            {
                File.Delete(selectedFile);
                WriteLineColor($"\nCharacter '{selectedName}' succesvol verwijderd.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                WriteLineColor($"\nFout bij verwijderen: {ex.Message}", ConsoleColor.Red);
            }
        }
        else
        {
            WriteLineColor("Verwijderen geannuleerd.", ConsoleColor.DarkGray);
        }
    }
    else
    {
        WriteLineColor("Ongeldige keuze.", ConsoleColor.Red);
    }

    Console.ReadKey();
}

void WriteLineColor(string text, ConsoleColor color)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = prev;
}

void WriteColor(string text, ConsoleColor color)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ForegroundColor = prev;
}
