using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DnDGame.Services
{
    public class MapService
    {
        private readonly Dictionary<string, Location> _locations = new();
        private string _currentLocationKey = "unknown";
        private readonly AIService _aiService;

        public MapService(AIService aiService)
        {
            _aiService = aiService;
        }

        public void UpdateMapFromStory(string story, string storySummary)
        {
            // Extract location information from the story using AI
            string prompt = $@"Analyseer de volgende tekst en identificeer de HUIDIGE locatie waar de speler zich bevindt.
            
Story: {story}
Context: {storySummary}

Geef alleen de volgende informatie terug in dit exacte formaat (één regel per item):
LOCATIE: [naam van de locatie]
BESCHRIJVING: [korte beschrijving]
UITGANGEN: [komma-gescheiden lijst van richtingen zoals noord,zuid,oost,west]

Als er geen duidelijke locatie is, gebruik dan 'Onbekende locatie' als naam.";

            try
            {
                string aiResponse = _aiService.AskAI(prompt);
                ParseAILocationResponse(aiResponse, story);
            }
            catch
            {
                // Fallback: try pattern matching
                ExtractLocationFromPattern(story);
            }
        }

        private void ParseAILocationResponse(string response, string originalStory)
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string locationName = "Onbekende locatie";
            string description = "Je bent ergens, maar weet niet precies waar.";
            var exits = new Dictionary<string, string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("LOCATIE:", StringComparison.OrdinalIgnoreCase))
                {
                    locationName = trimmed.Substring(8).Trim();
                }
                else if (trimmed.StartsWith("BESCHRIJVING:", StringComparison.OrdinalIgnoreCase))
                {
                    description = trimmed.Substring(13).Trim();
                }
                else if (trimmed.StartsWith("UITGANGEN:", StringComparison.OrdinalIgnoreCase))
                {
                    var exitString = trimmed.Substring(10).Trim();
                    var directions = exitString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var dir in directions)
                    {
                        var cleanDir = dir.Trim().ToLowerInvariant();
                        if (!string.IsNullOrEmpty(cleanDir))
                        {
                            // Create placeholder locations for exits
                            exits[cleanDir] = $"unknown_{cleanDir}";
                        }
                    }
                }
            }

            // Sanitize location name for use as key
            string locationKey = SanitizeLocationKey(locationName);

            // Add or update the location
            if (!_locations.ContainsKey(locationKey))
            {
                _locations[locationKey] = new Location
                {
                    Name = locationName,
                    Description = description,
                    Exits = exits
                };
            }
            else
            {
                // Update existing location
                _locations[locationKey].Description = description;
                // Merge exits
                foreach (var exit in exits)
                {
                    if (!_locations[locationKey].Exits.ContainsKey(exit.Key))
                        _locations[locationKey].Exits[exit.Key] = exit.Value;
                }
            }

            // Update current location
            _currentLocationKey = locationKey;
        }

        private void ExtractLocationFromPattern(string story)
        {
            // Fallback pattern matching for common location descriptions
            var patterns = new Dictionary<string, string>
            {
                { @"kerker|cel|gevangenis", "Kerker" },
                { @"gang|corridor|hal", "Gang" },
                { @"kamer|ruimte", "Kamer" },
                { @"bos|woud", "Bos" },
                { @"grot|hol", "Grot" },
                { @"marktplein|markt|plein", "Marktplein" },
                { @"schip|boot|dek", "Schip" },
                { @"bibliotheek", "Bibliotheek" },
                { @"troonzaal|troon", "Troonzaal" }
            };

            string locationName = "Onbekende locatie";
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(story, pattern.Key, RegexOptions.IgnoreCase))
                {
                    locationName = pattern.Value;
                    break;
                }
            }

            string locationKey = SanitizeLocationKey(locationName);
            
            if (!_locations.ContainsKey(locationKey))
            {
                _locations[locationKey] = new Location
                {
                    Name = locationName,
                    Description = story.Length > 100 ? story.Substring(0, 100) + "..." : story,
                    Exits = new Dictionary<string, string>()
                };
            }

            _currentLocationKey = locationKey;
        }

        private string SanitizeLocationKey(string name)
        {
            return Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "_");
        }

        public bool TryMove(string direction)
        {
            if (!_locations.TryGetValue(_currentLocationKey, out var currentLocation))
                return false;

            var directionLower = direction.ToLowerInvariant();

            // Map common variations
            var directionMap = new Dictionary<string, string>
            {
                { "n", "noord" }, { "north", "noord" },
                { "s", "zuid" }, { "south", "zuid" },
                { "e", "oost" }, { "east", "oost" },
                { "w", "west" }, { "west", "west" },
                { "o", "oost" }, { "z", "zuid" }
            };

            if (directionMap.ContainsKey(directionLower))
                directionLower = directionMap[directionLower];

            if (currentLocation.Exits.TryGetValue(directionLower, out string? nextLocationKey))
            {
                // If the next location doesn't exist yet, create a placeholder
                if (!_locations.ContainsKey(nextLocationKey))
                {
                    _locations[nextLocationKey] = new Location
                    {
                        Name = $"Nieuwe locatie ({directionLower})",
                        Description = "Een nieuwe plek die je nog moet verkennen.",
                        Exits = new Dictionary<string, string>
                        {
                            // Add reverse direction back
                            { GetOppositeDirection(directionLower), _currentLocationKey }
                        }
                    };
                }

                _currentLocationKey = nextLocationKey;
                return true;
            }

            return false;
        }

        private string GetOppositeDirection(string direction)
        {
            return direction switch
            {
                "noord" => "zuid",
                "zuid" => "noord",
                "oost" => "west",
                "west" => "oost",
                _ => direction
            };
        }

        public void AddExit(string direction, string targetLocationKey)
        {
            if (_locations.TryGetValue(_currentLocationKey, out var location))
            {
                location.Exits[direction.ToLowerInvariant()] = targetLocationKey;
            }
        }

        public string GetCurrentLocationName()
        {
            return _locations.TryGetValue(_currentLocationKey, out var location) 
                ? location.Name 
                : "Onbekende Locatie";
        }

        public string GetCurrentLocationDescription()
        {
            return _locations.TryGetValue(_currentLocationKey, out var location)
                ? location.Description
                : "Je weet niet waar je bent.";
        }

        public string[] GetAvailableExits()
        {
            return _locations.TryGetValue(_currentLocationKey, out var location)
                ? location.Exits.Keys.ToArray()
                : Array.Empty<string>();
        }

        public string DisplayMap()
        {
            var current = _locations.TryGetValue(_currentLocationKey, out var loc) 
                ? loc 
                : new Location { Name = "Onbekend", Description = "???" };

            var map = new System.Text.StringBuilder();

            map.AppendLine("?????????????????????????????????????????????");
            map.AppendLine($"? ?? Huidige Locatie: {TruncateOrPad(current.Name, 22)} ?");
            map.AppendLine("?????????????????????????????????????????????");
            
            // Word wrap description
            var descLines = WordWrap(current.Description, 41);
            foreach (var line in descLines)
            {
                map.AppendLine($"? {TruncateOrPad(line, 41)} ?");
            }
            
            map.AppendLine("?????????????????????????????????????????????");
            map.AppendLine("? Uitgangen:                                ?");

            if (current.Exits.Any())
            {
                foreach (var exit in current.Exits)
                {
                    var targetName = _locations.TryGetValue(exit.Value, out var targetLoc) 
                        ? targetLoc.Name 
                        : "???";
                    var exitLine = $"? {exit.Key,-8} naar {targetName}";
                    map.AppendLine($"?   {TruncateOrPad(exitLine, 39)} ?");
                }
            }
            else
            {
                map.AppendLine("?   (gebruik AI om nieuwe locaties te      ?");
                map.AppendLine("?    ontdekken tijdens het verhaal)         ?");
            }

            map.AppendLine("?????????????????????????????????????????????");

            return map.ToString();
        }

        private string TruncateOrPad(string text, int length)
        {
            if (text.Length > length)
                return text.Substring(0, length - 3) + "...";
            return text.PadRight(length);
        }

        private List<string> WordWrap(string text, int maxWidth)
        {
            var lines = new List<string>();
            var words = text.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                if ((currentLine + " " + word).Trim().Length <= maxWidth)
                {
                    currentLine = (currentLine + " " + word).Trim();
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                        lines.Add(currentLine);
                    currentLine = word;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines;
        }

        public void SetCurrentLocation(string locationKey)
        {
            if (_locations.ContainsKey(locationKey))
                _currentLocationKey = locationKey;
        }

        public string GetCurrentLocationKey() => _currentLocationKey;

        private class Location
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public Dictionary<string, string> Exits { get; set; } = new();
        }
    }
}