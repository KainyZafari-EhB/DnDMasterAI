using System;
using System.Collections.Generic;
using System.Linq;

namespace DnDGame.Services
{
    public class MapService
    {
        private readonly Dictionary<string, Location> _locations = new();
        private string _currentLocationKey = "start";
        private readonly Dictionary<string, (int x, int y)> _locationPositions = new();

        public MapService()
        {
            InitializeStartingLocation();
        }

        private void InitializeStartingLocation()
        {
            _locations["start"] = new Location
            {
                Name = "Startlocatie",
                Description = "Je beginpunt van het avontuur.",
                Exits = new Dictionary<string, string>()
            };
            _locationPositions["start"] = (0, 0);
            _currentLocationKey = "start";
        }

        public void UpdateMapFromStory(string story)
        {
            // Simply extract direction keywords from the story
            // The player will tell us where they're going with commands like "ga noord"
            // This map service doesn't need to parse the story - the game engine handles direction
        }

        public bool TryMove(string direction)
        {
            if (!_locations.TryGetValue(_currentLocationKey, out var currentLocation))
                return false;

            var directionLower = direction.ToLowerInvariant();

            // Normalize direction input
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

            // Check if we know where this direction leads
            if (currentLocation.Exits.ContainsKey(directionLower))
            {
                _currentLocationKey = currentLocation.Exits[directionLower];
                return true;
            }

            // Create a new location in this direction
            string newLocationKey = CreateNewLocation(directionLower);
            currentLocation.Exits[directionLower] = newLocationKey;
            _currentLocationKey = newLocationKey;
            return true;
        }

        private string CreateNewLocation(string direction)
        {
            var (offsetX, offsetY) = GetOffsetFromDirection(direction);
            var (currentX, currentY) = _locationPositions[_currentLocationKey];
            int newX = currentX + offsetX;
            int newY = currentY + offsetY;
            var newPos = (newX, newY);

            // Generate unique key based on position
            string newLocationKey = $"loc_{newX}_{newY}";

            // Create the new location with reverse exit
            _locations[newLocationKey] = new Location
            {
                Name = $"Onbekende ruimte",
                Description = "Een plek die je nog moet verkennen.",
                Exits = new Dictionary<string, string>
                {
                    { GetOppositeDirection(direction), _currentLocationKey }
                }
            };

            _locationPositions[newLocationKey] = newPos;
            return newLocationKey;
        }

        private (int, int) GetOffsetFromDirection(string direction)
        {
            return direction switch
            {
                "noord" => (0, 1),
                "zuid" => (0, -1),
                "oost" => (1, 0),
                "west" => (-1, 0),
                _ => (0, 0)
            };
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

        public void UpdateLocationName(string locationKey, string name)
        {
            if (_locations.ContainsKey(locationKey))
                _locations[locationKey].Name = name;
        }

        public void UpdateLocationDescription(string locationKey, string description)
        {
            if (_locations.ContainsKey(locationKey))
                _locations[locationKey].Description = description;
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
            if (!_locationPositions.Any())
                return "Geen kaart beschikbaar.\n";

            var output = new System.Text.StringBuilder();
            output.AppendLine("\n╔════════════════════════════════════════════════════════════╗");
            output.AppendLine("║                    🗺️  DUNGEON KAART  🗺️                     ║");
            output.AppendLine("╚════════════════════════════════════════════════════════════╝\n");

            int minX = _locationPositions.Values.Min(p => p.x);
            int maxX = _locationPositions.Values.Max(p => p.x);
            int minY = _locationPositions.Values.Min(p => p.y);
            int maxY = _locationPositions.Values.Max(p => p.y);

            // Draw rooms
            for (int y = maxY; y >= minY; y--)
            {
                var roomLine = new System.Text.StringBuilder();

                for (int x = minX; x <= maxX; x++)
                {
                    var locKey = _locationPositions.FirstOrDefault(kvp => kvp.Value.x == x && kvp.Value.y == y).Key;

                    if (locKey != null)
                    {
                        bool isCurrent = (locKey == _currentLocationKey);
                        roomLine.Append(isCurrent ? " [●] " : " [◯] ");
                    }
                    else
                    {
                        roomLine.Append("     ");
                    }

                    // Horizontal connection
                    if (x < maxX)
                    {
                        var rightLoc = _locationPositions.FirstOrDefault(kvp => kvp.Value.x == x + 1 && kvp.Value.y == y).Key;
                        if (locKey != null && rightLoc != null)
                            roomLine.Append("─");
                        else
                            roomLine.Append(" ");
                    }
                }

                output.AppendLine(roomLine.ToString());

                // Room names
                var nameLine = new System.Text.StringBuilder();
                for (int x = minX; x <= maxX; x++)
                {
                    var locKey = _locationPositions.FirstOrDefault(kvp => kvp.Value.x == x && kvp.Value.y == y).Key;
                    if (locKey != null)
                    {
                        var loc = _locations[locKey];
                        string name = loc.Name.Length > 5 ? loc.Name.Substring(0, 5) : loc.Name;
                        nameLine.Append($" {name,-5}");
                    }
                    else
                    {
                        nameLine.Append("      ");
                    }

                    if (x < maxX)
                        nameLine.Append(" ");
                }

                output.AppendLine(nameLine.ToString());

                // Vertical connections
                if (y > minY)
                {
                    var vertLine = new System.Text.StringBuilder();
                    for (int x = minX; x <= maxX; x++)
                    {
                        var locKey = _locationPositions.FirstOrDefault(kvp => kvp.Value.x == x && kvp.Value.y == y).Key;
                        var belowLoc = _locationPositions.FirstOrDefault(kvp => kvp.Value.x == x && kvp.Value.y == y - 1).Key;

                        if (locKey != null && belowLoc != null)
                            vertLine.Append("  │  ");
                        else
                            vertLine.Append("     ");

                        if (x < maxX)
                            vertLine.Append(" ");
                    }
                    output.AppendLine(vertLine.ToString());
                }
            }

            output.AppendLine("\n═══════════════════════════════════════════════════════════\n");

            // Current location info
            output.AppendLine($"📍 JE BENT HIER: {GetCurrentLocationName()}");
            output.AppendLine($"   {GetCurrentLocationDescription()}\n");

            var exits = GetAvailableExits();
            if (exits.Any())
            {
                output.AppendLine("⬇️  JE KUNT GAAN NAAR:");
                foreach (var exit in exits)
                {
                    var arrow = exit switch
                    {
                        "noord" => "⬆️ ",
                        "zuid" => "⬇️ ",
                        "oost" => "➡️ ",
                        "west" => "⬅️ ",
                        _ => "→ "
                    };
                    output.AppendLine($"   {arrow} Typ: 'ga {exit}'");
                }
            }
            else
            {
                output.AppendLine("ℹ️  Je kunt in alle richtingen gaan (noord, zuid, oost, west)");
            }

            output.AppendLine("\n═══════════════════════════════════════════════════════════");
            output.AppendLine("Legend: ● = je bent hier | ◯ = bezochte ruimte");
            output.AppendLine("═══════════════════════════════════════════════════════════\n");

            return output.ToString();
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