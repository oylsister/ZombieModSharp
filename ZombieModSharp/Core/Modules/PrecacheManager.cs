using Microsoft.Extensions.Logging;
using Sharp.Shared;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class PrecacheManager : IPrecacheManager
{
    private readonly ISharedSystem _sharedSystem;
    private readonly IModSharp _modsharp;
    private readonly ILogger<PrecacheManager> _logger;

    private List<string> _precacheList = [];

    public PrecacheManager(ISharedSystem sharedSystem)
    {
        _sharedSystem = sharedSystem;
        _modsharp = _sharedSystem.GetModSharp();
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<PrecacheManager>();
    }

    // it should be done as soon as possible for precaching.
    public void LoadConfig(string path)
    {
        var configPath = Path.Combine(path, "resources.txt");

        if (!File.Exists(configPath))
        {
            _logger.LogCritical("File is not found!");
            return;
        }

        _precacheList.Clear();

        // read all line in .txt file and ignore the line that start with // or empty space.
        _modsharp.InvokeFrameActionAsync(async () =>
        {
            var lines = await File.ReadAllLinesAsync(configPath);

            _precacheList = lines
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//"))
                .Select(line => line.Trim())
                .ToList();

            _logger.LogInformation("Loaded {Count} resources for precaching", _precacheList.Count);
        });
    }

    public void PrecacheAllResource()
    {
        if (_precacheList.Count <= 0)
        {
            _logger.LogWarning("No resource found for precaching");
            return;
        }

        foreach (var resource in _precacheList) _modsharp.PrecacheResource(resource);
    }
}