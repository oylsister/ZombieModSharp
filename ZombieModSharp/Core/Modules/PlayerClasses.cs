using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Abstractions.Storage;

namespace ZombieModSharp.Core.Modules;

public class ClassAttribute
{
    public string Name { get; set; } = string.Empty;
    public int Team { get; set; } = 0;
    public bool MotherZombie { get; set; } = false;
    public string Model { get; set; } = "default";
    public int Health { get; set; } = 100;
    public int HealthRegen { get; set; } = 0;
    public float HealthRegenInterval { get; set; } = 0.0f;
    public float NapalmDuration { get; set; } = 0.0f;
    public float Knockback { get; set; } = 3.0f;
    public float Speed { get; set; } = 250f;
}

public class PlayerClasses : IPlayerClasses
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<PlayerClasses> _logger;
    private readonly IPlayerManager _playerManager;
    private readonly IModSharp _modSharp;
    private readonly ISqliteDatabase _sqlite;
    private IMenuManager? _menuManager;

    public PlayerClasses(ISharedSystem sharedSystem, IPlayerManager playerManager, ISqliteDatabase sqliteDatabase)
    {
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<PlayerClasses>();
        _playerManager = playerManager;
        _modSharp = _sharedSystem.GetModSharp();
        _sqlite = sqliteDatabase;
    }

    public Dictionary<string, ClassAttribute> classesData = [];

    public void LoadConfig(string path)
    {
        var configPath = Path.Combine(path, "playerclasses.jsonc");

        if (!File.Exists(configPath))
        {
            _logger.LogCritical("File is not found!");
            return;
        }

        classesData.Clear();

        try
        {
            var jsonContent = File.ReadAllText(configPath);
            
            // Simple comment removal (basic implementation)
            var lines = jsonContent.Split('\n');
            var cleanedLines = lines.Select(line => 
            {
                var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
            });
            var cleanedJson = string.Join("\n", cleanedLines);

            classesData = JsonSerializer.Deserialize<Dictionary<string, ClassAttribute>>(cleanedJson) ?? [];

            _logger.LogInformation("Successfully loaded {count} classes configurations", classesData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse classes configuration");
        }
    }

    public void GetMenuManager(IModSharpModuleInterface<IMenuManager>? menuManager)
    {
        _menuManager = menuManager?.Instance;
    }

    public void PlayerClassMenu(IGameClient client)
    {
        if (_menuManager == null)
        {
            _logger.LogError("MenuManager is not available!");
            return;
        }

        var menu = Menu.Create().Build();

        menu.SetTitle("[ZombieModSharp] Player Class Menu");

        menu.AddSubMenu("Human Classes", _ => ClassSelectionMenu(client, false));
        menu.AddSubMenu("Zombie Classes", _ => ClassSelectionMenu(client, true));
        menu.AddExitItem();

        _menuManager.DisplayMenu(client, menu);
    }

    private Menu ClassSelectionMenu(IGameClient client, bool isZombie)
    {
        var menu = Menu.Create().Build();

        var player = _playerManager.GetOrCreatePlayer(client);

        var selectedClass = isZombie ? player.ZombieClass : player.HumanClass;
        menu.SetTitle("[ZMS:Class] Current class: " + (selectedClass?.Name ?? "None"));
        var classList = classesData.Where(c => c.Value.Team == (isZombie ? 0 : 1)).ToList();

        // this only added one time.
        foreach (var classObject in classList)
        {
            var classValue = classObject.Value;

            if(selectedClass == classValue)
            {
                menu.AddDisabledItem(classValue.Name + " (Selected)");
                continue;
            }

            menu.AddItem(_ => classValue.Name, controller => 
            {
                if (isZombie)
                {
                    player.ZombieClass = classValue;
                    var humanClassKey = classesData.FirstOrDefault(c => c.Value == player.HumanClass).Key;
                    _sqlite.InsertPlayerClassesAsync(client.SteamId.ToString(), humanClassKey, classObject.Key);
                }
                else
                {
                    player.HumanClass = classValue;
                    var zombieClassKey = classesData.FirstOrDefault(c => c.Value == player.ZombieClass).Key;
                    _sqlite.InsertPlayerClassesAsync(client.SteamId.ToString(), classObject.Key, zombieClassKey);
                }

                _modSharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} You have selected the new class, this change will be applied in the next respawn.", new RecipientFilter(client));
                controller.Exit();
            });
        }

        menu.AddBackItem();
        return menu;
    }

    public void ApplyPlayerClassAttribute(IPlayerPawn playerPawn, ClassAttribute classAttribute)
    {
        if (!playerPawn.IsAlive)
        {
            return;
        }

        playerPawn.Health = classAttribute.Health;
        var team = classAttribute.Team;

        if (classAttribute.Model != "default" && !string.IsNullOrEmpty(classAttribute.Model))
            playerPawn.SetModel(classAttribute.Model);

        var gameClient = playerPawn.GetController()?.GetGameClient();

        if (gameClient == null)
        {
            _logger.LogError("IGameClient is null!");
            return;
        }

        var client = _playerManager.GetOrCreatePlayer(gameClient);

        SetClassArmor(playerPawn, team);
        InitialHeathRegen(playerPawn, classAttribute, client);

        client.ActiveClass = classAttribute;
    }

    private void SetClassArmor(IPlayerPawn playerPawn, int team)
    {
        if (team == 0)
        {
            playerPawn.ArmorValue = 0;
            try
            {
                playerPawn.GetItemService()!.HasHelmet = false;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {ex}", ex);
            }
        }
        else
        {
            playerPawn.ArmorValue = 100;
        }
    }
    
    public ClassAttribute? GetClassByName(string classname)
    {
        if (string.IsNullOrEmpty(classname))
            return null;

        if (!classesData.TryGetValue(classname, out var targetClass))
            return null;

        return targetClass;
    }

    public ClassAttribute? GetMotherZombieClass()
    {
        return classesData.Values.FirstOrDefault(c => c.MotherZombie);
    }

    private void InitialHeathRegen(IPlayerPawn playerPawn, ClassAttribute classAttribute, Player? player)
    {
        if (player == null)
        {
            return;
        }

        if(player.RegenerationTimer != Guid.Empty)
        {
            _modSharp.StopTimer(player.RegenerationTimer);
            player.RegenerationTimer = Guid.Empty;
        }

        if (classAttribute.HealthRegen > 0 && classAttribute.HealthRegenInterval > 0)
        {
            player.RegenerationTimer = _modSharp.PushTimer(new Func<TimerAction>(() =>
            {
                if (!playerPawn.IsAlive)
                {
                    return TimerAction.Stop;
                }

                var currentHealth = playerPawn.Health;
                var maxHealth = classAttribute.Health;

                // current health is less than max health, we can regen.
                if (currentHealth < maxHealth)
                {
                    var newHealth = currentHealth + classAttribute.HealthRegen;

                    // make sure we don't overheal the player.
                    if (newHealth > maxHealth)
                    {
                        newHealth = maxHealth;
                    }

                    playerPawn.Health = newHealth;
                }

                else
                {
                    // stop the timer if player is at full health.
                    return TimerAction.Continue;
                }

                return TimerAction.Continue;
            }), classAttribute.HealthRegenInterval, GameTimerFlags.Repeatable | GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
        }
    }
}
