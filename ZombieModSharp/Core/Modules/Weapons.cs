using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Extensions.CommandManager;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class WeaponData
{
    public required string WeaponName { get; set; }
    public required string EntityName { get; set; }
    public int WeaponSlot { get; set; }
    public float Knockback { get; set; } = 1.0f;
    public bool Restrict { get; set; } = false;
    public int MaxPurchase { get; set; } = 0;
    public List<string> Command { get; set; } = [];
    public int Price { get; set; }
    public WeaponAmmo? Ammo { get; set; }
}

public class WeaponAmmo
{
    public int Clip { get; set; }
    public int ReserveAmmo { get; set; }
}

public class Weapons : IWeapons
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<Weapons> _logger;
    private readonly IModSharp _modsharp;
    private readonly ICommandManager _commandManager;
    private readonly IPlayerManager _playerManager;
    private readonly IEntityManager _entityManager;
    private readonly IConVarManager _conVarManager;

    private Dictionary<string, WeaponData> weaponDatas = [];
    private readonly Dictionary<string, int> _grenadeAmmoIndexes = [];

    public Weapons(ISharedSystem sharedSystem, ILogger<Weapons> logger, ICommandManager commandManager, IPlayerManager playerManager)
    {
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<Weapons>();
        _modsharp = _sharedSystem.GetModSharp();
        _commandManager = commandManager;
        _playerManager = playerManager;
        _entityManager = _sharedSystem.GetEntityManager();
        _conVarManager = _sharedSystem.GetConVarManager();
    }

    public void LoadConfig(string path)
    {
        var configPath = Path.Combine(path, "weapons.jsonc");

        if (!File.Exists(configPath))
        {
            _logger.LogCritical("File is not found!");
            return;
        }

        weaponDatas.Clear();

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

            weaponDatas = JsonSerializer.Deserialize<Dictionary<string, WeaponData>>(cleanedJson) ?? [];
            _logger.LogInformation("Successfully loaded {count} weapon configurations", weaponDatas.Count);
            AssignWeaponPurchaseCommand();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse weapons configuration");
        }
    }

    private void AssignWeaponPurchaseCommand()
    {
        if(weaponDatas == null || weaponDatas.Count <= 0)
            return;

        foreach(var weapon in weaponDatas)
        {
            if(weapon.Value.Command == null || weapon.Value.Command.Count <= 0)
                continue;

            foreach(var command in weapon.Value.Command)
            {
                _commandManager.RegisterClientCommand(command, OnPurchaseWeaponCommand);
                //_logger.LogInformation("Assigned Command {command}", command);
            }
        }
    }

    private void OnPurchaseWeaponCommand(IGameClient client, StringCommand command)
    {
        var arg = command.CommandName;
        var weaponData = weaponDatas.FirstOrDefault(w => w.Value.Command.Contains(arg)).Value;

        if(weaponData == null)
        {
            PrintToChat(client, "Invalid weapon command!");
            return;
        }

        PurchaseWeapon(client, weaponData);
    }

    public void PurchaseWeapon(IGameClient client, WeaponData weapon)
    {
        var controller = client.GetPlayerController();
        var pawn = controller?.GetPlayerPawn();
        var player = _playerManager.GetOrCreatePlayer(client);

        if(weapon.Restrict)
        {
            PrintToChat(client, $"Weapon \x05{weapon.WeaponName}\x01 is restricted");
            return;
        }

        if(pawn == null || controller == null)
        {
            return;
        }

        if(pawn.Team <= CStrikeTeam.Spectator)
        {
            PrintToChat(client, "This feature require player to be in team.");
            return;
        }

        if(!pawn.IsAlive)
        {
            PrintToChat(client, "This feature require player to be alive.");
            return;
        }

        if(player.IsInfected())
        {
            PrintToChat(client, "This feautre require player to be human.");
            return;
        }

        if(weapon.MaxPurchase == -1)
        {
            PrintToChat(client, $"Weapon \x05{weapon.WeaponName}\x01 is restricted for purchasing, and only can be obtained in the map.");
            return;
        }

        if(weapon.MaxPurchase > 0)
        {
            if(player.PurchaseHistory.TryGetValue(weapon.WeaponName, out var weaponData) && weaponData >= weapon.MaxPurchase)
            {
                PrintToChat(client, $"Your purchase of weapon \x05{weapon.WeaponName}\x01 has reached maximum number that allow this round.");
                return;
            }
        }

        var money = controller.GetInGameMoneyService()?.Account;

        if(money < weapon.Price)
        {
            PrintToChat(client, $"You don't have enough cash for purchasing this weapon! (Price: {weapon.Price}$)");
            return;
        }

        if(weapon.EntityName == "item_assaultsuit")
        {
            var armor = pawn.ArmorValue;
            
            if(armor < 100)
            {
                pawn.GiveNamedItem(EconItemId.AssaultSuit);
                PrintToChat(client, $"You have purchased weapon \x05{weapon.WeaponName}\x01. {(weapon.MaxPurchase > 0 ? $"Purchases available left: ({weapon.MaxPurchase - player.PurchaseHistory[weapon.WeaponName]}/{weapon.MaxPurchase})" : "")}");
                return;
            }

            else
            {
                PrintToChat(client, $"Your armor still good! try again once it got damaged.");
                return;
            }
        }

        // force drop weapon mostly.
        if(weapon.WeaponSlot <= (int)GearSlot.Pistol)
        {
            var ent = pawn.GetWeaponBySlot((GearSlot)weapon.WeaponSlot);

            if(ent != null)
            {
                pawn.DropWeapon(ent);
                _modsharp.PushTimer(() =>
                {
                    if(ent != null && ent.IsValid())
                        ent.AcceptInput("Kill");
                }, 0.02f);
            }
        }

        else if(weapon.WeaponSlot == (int)GearSlot.Grenades)
        {
            var carriedGrenade = GetCarriedGrenade(pawn, weapon);

            if(carriedGrenade != null)
            {
                var stackLimit = GetGrenadeStackLimit();
                var carriedCount = Math.Max(GetVisibleGrenadeStack(pawn, weapon.EntityName), 1);

                if(carriedCount >= stackLimit)
                {
                    PrintToChat(client, $"You already carried the maximum stack of \x05{weapon.WeaponName}\x01.");
                    return;
                }

                ChargeAndTrackPurchase(controller, player, weapon);
                SetVisibleGrenadeStack(pawn, weapon.EntityName, carriedCount + 1);
                PrintPurchaseMessage(client, player, weapon);
                return;
            }
        }

        int[]? ammoBefore = null;

        if(weapon.WeaponSlot == (int)GearSlot.Grenades)
            ammoBefore = SnapshotAmmo(pawn);

        ChargeAndTrackPurchase(controller, player, weapon);
        pawn.GiveNamedItem(weapon.EntityName);

        if(weapon.WeaponSlot == (int)GearSlot.Grenades)
        {
            DetectGrenadeAmmoIndex(pawn, weapon.EntityName, ammoBefore);
            SetVisibleGrenadeStack(pawn, weapon.EntityName, Math.Max(GetVisibleGrenadeStack(pawn, weapon.EntityName), 1));
        }

        PrintPurchaseMessage(client, player, weapon);
    }

    public bool TryPickupStackedGrenade(IGameClient client, string weaponEntityName)
    {
        var pawn = client.GetPlayerController()?.GetPlayerPawn();
        var entityName = GetGrenadeEntityName(weaponEntityName);

        if(pawn == null || entityName == null || !pawn.IsAlive)
            return false;

        if(GetCarriedGrenade(pawn, entityName) == null)
            return false;

        var current = Math.Max(GetVisibleGrenadeStack(pawn, entityName), 1);

        if(current >= GetGrenadeStackLimit())
            return false;

        SetVisibleGrenadeStack(pawn, entityName, current + 1);
        return true;
    }

    public float GetWeaponKnockback(string weaponentity)
    {
        if (!weaponDatas.TryGetValue(weaponentity, out var weaponData))
        {
            // _modsharp.PrintToChatAll($"No weapons name {weaponentity}");
            return 1.0f;
        }

        // _modsharp.PrintToChatAll($"Found {weaponData.EntityName} and KB: {weaponData.Knockback}");
        return weaponData.Knockback;
    }

    public WeaponAmmo? GetWeaponAmmo(string weaponentity)
    {
        return weaponDatas.FirstOrDefault(p => p.Value.EntityName == weaponentity).Value?.Ammo;
    }

    public bool IsWeaponRestricted(string weaponentity)
    {
        var data = weaponDatas.FirstOrDefault(p => p.Value.EntityName == weaponentity).Value;
        return data != null ? data.Restrict : false;
    }

    public WeaponData GetWeaponDataWithEntityName(string weaponentity)
    {
        var result = weaponDatas.FirstOrDefault(p => p.Key == weaponentity
            || p.Value.EntityName == weaponentity
            || p.Value.EntityName == $"weapon_{weaponentity}").Value;
        return result;
    }

    private IBaseWeapon? GetCarriedGrenade(IPlayerPawn pawn, WeaponData weaponData)
    {
        return GetCarriedGrenade(pawn, weaponData.EntityName);
    }

    private IBaseWeapon? GetCarriedGrenade(IPlayerPawn pawn, string entityName)
    {
        var weapons = pawn.GetWeaponService()?.GetMyWeapons();
        var itemDefinitionIndex = GetGrenadeItemDefinitionIndex(entityName);

        if(weapons == null)
            return null;

        foreach(var item in weapons)
        {
            var weapon = _entityManager.FindEntityByHandle(item)?.AsBaseWeapon();

            if(weapon == null)
                continue;

            if(weapon.Classname == entityName)
                return weapon;

            if(itemDefinitionIndex.HasValue && weapon.ItemDefinitionIndex == itemDefinitionIndex.Value)
                return weapon;
        }

        return null;
    }

    private static ushort? GetGrenadeItemDefinitionIndex(string entityName)
    {
        return entityName == "weapon_hegrenade" ? (ushort)EconItemId.Hegrenade : null;
    }

    private int GetGrenadeStackLimit()
    {
        return _conVarManager.FindConVar("zms_grenade_stack_limit", true)?.GetInt32() ?? 3;
    }

    private int[]? SnapshotAmmo(IPlayerPawn pawn)
    {
        var ammo = pawn.GetWeaponService()?.GetAmmo();

        if(ammo == null)
            return null;

        var snapshot = new int[ammo.Size];

        for(var i = 0; i < ammo.Size; i++)
            snapshot[i] = ammo[i];

        return snapshot;
    }

    private void DetectGrenadeAmmoIndex(IPlayerPawn pawn, string entityName, int[]? ammoBefore)
    {
        if(ammoBefore == null || _grenadeAmmoIndexes.ContainsKey(entityName))
            return;

        var ammo = pawn.GetWeaponService()?.GetAmmo();

        if(ammo == null)
            return;

        var length = Math.Min(ammo.Size, ammoBefore.Length);

        for(var i = 0; i < length; i++)
        {
            if(ammo[i] > ammoBefore[i])
            {
                _grenadeAmmoIndexes[entityName] = i;
                return;
            }
        }
    }

    private int GetVisibleGrenadeStack(IPlayerPawn pawn, string entityName)
    {
        var index = GetGrenadeAmmoIndex(entityName);
        var ammo = pawn.GetWeaponService()?.GetAmmo();

        if(index == null || ammo == null || index.Value >= ammo.Size)
            return 0;

        return ammo[index.Value];
    }

    private void SetVisibleGrenadeStack(IPlayerPawn pawn, string entityName, int count)
    {
        var index = GetGrenadeAmmoIndex(entityName);
        var ammo = pawn.GetWeaponService()?.GetAmmo();

        if(index == null || ammo == null || index.Value >= ammo.Size)
            return;

        ammo[index.Value] = (ushort)Math.Clamp(count, 0, GetGrenadeStackLimit());
    }

    private int? GetGrenadeAmmoIndex(string entityName)
    {
        entityName = GetGrenadeEntityName(entityName) ?? entityName;

        if(_grenadeAmmoIndexes.TryGetValue(entityName, out var cached))
            return cached;

        return entityName switch
        {
            "weapon_flashbang" => 14,
            "weapon_hegrenade" => 15,
            "weapon_smokegrenade" => 16,
            "weapon_molotov" => 17,
            "weapon_decoy" => 18,
            "weapon_incgrenade" => 17,
            _ => null
        };
    }

    private static string? GetGrenadeEntityName(string weaponName)
    {
        return weaponName switch
        {
            "hegrenade" or "weapon_hegrenade" => "weapon_hegrenade",
            "flashbang" or "weapon_flashbang" => "weapon_flashbang",
            "smokegrenade" or "weapon_smokegrenade" => "weapon_smokegrenade",
            "decoy" or "weapon_decoy" => "weapon_decoy",
            "molotov" or "weapon_molotov" => "weapon_molotov",
            "incgrenade" or "weapon_incgrenade" => "weapon_incgrenade",
            _ => null
        };
    }

    private void ChargeAndTrackPurchase(IPlayerController controller, Player player, WeaponData weapon)
    {
        controller.GetInGameMoneyService()!.Account -= weapon.Price;

        if (!player.PurchaseHistory.ContainsKey(weapon.WeaponName))
            player.PurchaseHistory[weapon.WeaponName] = 0;

        player.PurchaseHistory[weapon.WeaponName] += 1;
    }

    private void PrintPurchaseMessage(IGameClient client, Player player, WeaponData weapon)
    {
        PrintToChat(client, $"You have purchased weapon \x05{weapon.WeaponName}\x01. {(weapon.MaxPurchase > 0 ? $"Purchases available left: ({weapon.MaxPurchase - player.PurchaseHistory[weapon.WeaponName]}/{weapon.MaxPurchase})" : "")}");
    }

    private void PrintToChat(IGameClient client, string text)
    {
        _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} {text}", new RecipientFilter(client));
    }
}
