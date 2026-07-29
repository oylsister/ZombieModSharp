using Microsoft.Extensions.Logging;
using Sharp.Shared;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class Configs : IConfigs
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<Configs> _logger;
    private readonly IWeapons _weapons;
    private readonly IHitGroup _hitGroup;
    private readonly IPrecacheManager _precacheManager;
    private readonly IPlayerClasses _playerClasses;

    private static string configPath = string.Empty;

    public Configs(ISharedSystem sharedSystem, ILogger<Configs> logger, IWeapons weapons, IHitGroup hitGroup, IPrecacheManager precacheManager, IPlayerClasses playerClasses)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
        _weapons = weapons;
        _hitGroup = hitGroup;
        _precacheManager = precacheManager;
        _playerClasses = playerClasses;
    }

    public void Init()
    {
        _logger.LogInformation("Configs.PostInit() called");

        var gamePath = _sharedSystem.GetModSharp().GetGamePath();
        configPath = Path.Combine(gamePath, "../sharp", "configs", "zombiemodsharp");

        if (!Directory.Exists(configPath))
        {
            _logger.LogWarning("Path {config} is not existed, create new one.", configPath);
            Directory.CreateDirectory(configPath);
        }

        _logger.LogInformation("Loading weapons config...");
        _precacheManager.LoadConfig(configPath);
        _playerClasses.LoadConfig(configPath);
        _weapons.LoadConfig(configPath);
        _hitGroup.LoadConfig(configPath);
    }
}
