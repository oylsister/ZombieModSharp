using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class MapSettings : IMapSettings
{
    private static readonly HashSet<string> ValidLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "easy", "normal", "hard", "hardest"
    };

    private readonly ILogger<MapSettings> _logger;
    private string _configPath = string.Empty;
    private string _currentMap = string.Empty;
    private MapSettingsConfig _config = new();

    public MapSettings(ISharedSystem sharedSystem)
    {
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<MapSettings>();
    }

    public void LoadConfig(string path)
    {
        _configPath = Path.Combine(path, "mapsettings.jsonc");

        if (!File.Exists(_configPath))
        {
            SaveConfig();
            return;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var cleanedJson = string.Join("\n", json.Split('\n').Select(line =>
            {
                var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                return commentIndex >= 0 ? line[..commentIndex] : line;
            }));

            _config = JsonSerializer.Deserialize<MapSettingsConfig>(cleanedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new MapSettingsConfig();

            _config.DisableBoostLevels = NormalizeLevels(_config.DisableBoostLevels);
            _config.Maps = _config.Maps
                .Where(pair => IsValidLevel(pair.Value))
                .ToDictionary(pair => NormalizeMap(pair.Key), pair => pair.Value.ToLowerInvariant());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mapsettings.jsonc");
            _config = new MapSettingsConfig();
        }
    }

    public void SetCurrentMap(string mapName)
    {
        if (!string.IsNullOrEmpty(_configPath))
            LoadConfig(Path.GetDirectoryName(_configPath)!);

        _currentMap = NormalizeMap(mapName);
        _logger.LogInformation("Map level for {Map}: {Level}", _currentMap, GetCurrentLevel());
    }

    public string GetCurrentLevel()
    {
        return _config.Maps.TryGetValue(_currentMap, out var level) ? level : "easy";
    }

    public bool AreBoostCommandsDisabled()
    {
        return _config.DisableBoostLevels.Contains(GetCurrentLevel());
    }

    public bool SetCurrentMapLevel(string level)
    {
        level = level.Trim().ToLowerInvariant();
        if (!IsValidLevel(level) || string.IsNullOrEmpty(_currentMap))
            return false;

        _config.Maps[_currentMap] = level;
        SaveConfig();
        return true;
    }

    private void SaveConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static HashSet<string> NormalizeLevels(IEnumerable<string>? levels)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var level in levels ?? ["hard", "hardest"])
        {
            if (IsValidLevel(level))
                result.Add(level.ToLowerInvariant());
        }

        if (result.Count == 0)
        {
            result.Add("hard");
            result.Add("hardest");
        }

        return result;
    }

    private static bool IsValidLevel(string level)
    {
        return ValidLevels.Contains(level);
    }

    private static string NormalizeMap(string mapName)
    {
        return mapName.Trim().ToLowerInvariant();
    }
}

public class MapSettingsConfig
{
    public HashSet<string> DisableBoostLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "hard", "hardest"
    };

    public Dictionary<string, string> Maps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
