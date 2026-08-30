using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Extensions.CommandManager;
using Sharp.Extensions.GameEventManager;
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.ClientPreferences.Shared;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Abstractions;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Core.Services;
using ZombieModSharp.Shared;

namespace ZombieModSharp;

public sealed class ZombieModSharp : IModSharpModule
{
    public string DisplayName => "Zombie ModSharp";
    public string DisplayAuthor => "Oylsister";

    private IModSharpModuleInterface<IAdminManager>? _adminManager;
    private IModSharpModuleInterface<IMenuManager>? _menuManager;

    private readonly ILogger<ZombieModSharp> _logger;

    // private readonly InterfaceBridge  _bridge;
    private readonly ServiceProvider _serviceProvider;
    private readonly ISharedSystem _sharedSystem;
    private readonly IEvents _eventListener;
    private readonly IListeners _listeners;
    private readonly ICommand _command;
    private readonly IHooks _hooks;
    private readonly IConfigs _configs;
    private readonly ICvarServices _cvarServices;
    private readonly IInfect _infect;
    private readonly ILeaderServices _leaderServices;
    private readonly ISharpModuleManager _modules;
    private readonly IPlayerClasses _playerClasses;
    private readonly IPlayerManager _playerManager;
    private IModSharpModuleInterface<IClientPreference>? _clientPreference;
    private IDisposable? _clientPreferenceLoadSubscription;

    public static string Prefix { get; } = " \x04[Z:MS]\x01";
    internal const string HumanClassCookieKey = "ZombieModSharp.HumanClass";
    internal const string ZombieClassCookieKey = "ZombieModSharp.ZombieClass";
    internal const string SoundEnabledCookieKey = "ZombieModSharp.SoundEnabled";
    internal const string SoundVolumeCookieKey = "ZombieModSharp.SoundVolume";
    private const string AdminManagerAssemblyName = "Sharp.Modules.AdminManager";

    public ZombieModSharp(ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version? version,
        IConfiguration configuration,
        bool hotReload)
    {
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);
        ArgumentNullException.ThrowIfNull(version);
        //ArgumentNullException.ThrowIfNull(coreConfiguration);

        _sharedSystem = sharedSystem ?? throw new ArgumentNullException(nameof(sharedSystem));
        _modules = _sharedSystem.GetSharpModuleManager();
        // var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(dllPath, "appsettings.json"), false, false).Build();

        var services = new ServiceCollection();

        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        // Register external dependencies
        services.AddSingleton(_sharedSystem);
        services.AddSingleton(_sharedSystem.GetEntityManager());
        services.AddSingleton(_sharedSystem.GetEventManager());
        services.AddSingleton(_sharedSystem.GetModSharp());
        services.AddSingleton(_sharedSystem.GetClientManager());

        services.AddCommandManager(sharedSystem);
        services.AddGameEventManager(sharedSystem);

        // Register our services using the extension method
        services.AddZombieModSharpServices();

        // _bridge = new InterfaceBridge(dllPath, sharpPath, version, sharedSystem);
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<ZombieModSharp>();
        _serviceProvider = services.BuildServiceProvider();

        // Get services from DI container instead of manual instantiation
        _eventListener = _serviceProvider.GetRequiredService<IEvents>();
        _listeners = _serviceProvider.GetRequiredService<IListeners>();
        _command = _serviceProvider.GetRequiredService<ICommand>();
        _hooks = _serviceProvider.GetRequiredService<IHooks>();
        _configs = _serviceProvider.GetRequiredService<IConfigs>();
        _cvarServices = _serviceProvider.GetRequiredService<ICvarServices>();
        _infect = _serviceProvider.GetRequiredService<IInfect>();
        _leaderServices = _serviceProvider.GetRequiredService<ILeaderServices>();
        _playerClasses = _serviceProvider.GetRequiredService<IPlayerClasses>();
        _playerManager = _serviceProvider.GetRequiredService<IPlayerManager>();
    }

    public bool Init()
    {
        // we need this for command extensions.
        _serviceProvider.LoadAllSharpExtensions();

        _logger.LogInformation(
            "Oh wow, we seem to be crossing paths a lot lately... Where could I have seen you before? Can you figure it out?");

        _listeners.Init();
        _eventListener.Init();
        _hooks.Init();
        _cvarServices.Init();
        _configs.Init();

        var _gamedata = _sharedSystem.GetModSharp().GetGameData();
        _gamedata.Register("ZombieModSharp.jsonc");

        return true;
    }

    public void Shutdown()
    {
        // yes this one too
        _serviceProvider.ShutdownAllSharpExtensions();

        // _logger.LogInformation("See you around, Nameless~ Try to stay out of trouble, especially... the next time we meet!");
        _listeners.Shutdown();
        _hooks.Shutdown();
        _clientPreferenceLoadSubscription?.Dispose();
        _cvarServices.Shutdown();
    }

    public void PostInit()
    {
        // _logger.LogInformation("Why don't you stay and play for a while?");
        // _eventListener.RegisterEvents();
        _command.PostInit();
        _modules.RegisterSharpModuleInterface<IInfectShared>(this, IInfectShared.Identity, _infect);
        _modules.RegisterSharpModuleInterface<ILeaderShared>(this, ILeaderShared.Identity, _leaderServices);
    }

    public void OnAllModulesLoaded()
    {
        TryResolveAdminManager();
        _menuManager = _sharedSystem.GetSharpModuleManager()
            .GetOptionalSharpModuleInterface<IMenuManager>(IMenuManager.Identity);
        _playerClasses.GetMenuManager(_menuManager);
        _clientPreference = _modules.GetRequiredSharpModuleInterface<IClientPreference>(IClientPreference.Identity);
        _clientPreferenceLoadSubscription = _clientPreference.Instance!.ListenOnLoad(OnClientPreferencesLoaded);
    }

    private void OnClientPreferencesLoaded(IGameClient client)
    {
        if (client.IsFakeClient || client.IsHltv)
            return;

        var preferences = _clientPreference!.Instance!;
        var humanClassName = preferences.GetCookie(client.SteamId, HumanClassCookieKey)?.GetString();
        var zombieClassName = preferences.GetCookie(client.SteamId, ZombieClassCookieKey)?.GetString();
        var soundEnabledText = preferences.GetCookie(client.SteamId, SoundEnabledCookieKey)?.GetString();
        var soundVolumeText = preferences.GetCookie(client.SteamId, SoundVolumeCookieKey)?.GetString();

        var humanClass = _playerClasses.GetClassByName(humanClassName ?? string.Empty);
        if (humanClass == null)
        {
            humanClassName = _cvarServices.CvarList["Cvar_HumanDefault"]!.GetString();
            humanClass = _playerClasses.GetClassByName(humanClassName);
            preferences.SetCookie(client.SteamId, HumanClassCookieKey, humanClassName);
        }

        var zombieClass = _playerClasses.GetClassByName(zombieClassName ?? string.Empty);
        if (zombieClass == null)
        {
            zombieClassName = _cvarServices.CvarList["Cvar_ZombieDefault"]!.GetString();
            zombieClass = _playerClasses.GetClassByName(zombieClassName);
            preferences.SetCookie(client.SteamId, ZombieClassCookieKey, zombieClassName);
        }

        if (!bool.TryParse(soundEnabledText, out var soundEnabled))
        {
            soundEnabled = true;
            preferences.SetCookie(client.SteamId, SoundEnabledCookieKey, bool.TrueString);
        }

        if (!int.TryParse(soundVolumeText, out var soundVolume) || soundVolume is < 0 or > 100)
        {
            soundVolume = 100;
            preferences.SetCookie(client.SteamId, SoundVolumeCookieKey, "100");
        }

        var player = _playerManager.GetOrCreatePlayer(client);
        player.HumanClass = humanClass;
        player.ZombieClass = zombieClass;
        player.SoundEnabled = soundEnabled;
        player.SoundVolume = soundVolume;
    }

    public void OnLibraryConnected(string name)
    {
        if (name.Equals(AdminManagerAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            TryResolveAdminManager(true);
        }
    }

    public void OnLibraryDisconnect(string name)
    {
    }

    private void TryResolveAdminManager(bool logFailure = false)
    {
        if (_adminManager?.Instance is not null)
        {
            return;
        }

        _adminManager = _sharedSystem.GetSharpModuleManager()
            .GetOptionalSharpModuleInterface<IAdminManager>(IAdminManager.Identity);

        if (_adminManager?.Instance is null)
        {
            if (logFailure)
            {
                _logger.LogWarning("AdminManager is not installed. Admin commands will not work.");
            }

            return;
        }

        _command.GetAdminManager(_adminManager);
        _command.RegisterAdminCommand();
    }
}