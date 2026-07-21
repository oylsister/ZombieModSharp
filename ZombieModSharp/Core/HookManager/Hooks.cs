using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.GameEvents;
using Sharp.Shared.GameObjects;
using Sharp.Shared.HookParams;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.HookManager;

public class Hooks : IHooks
{
    private readonly ISharedSystem _sharedSystem;
    private readonly IPlayerManager _playerManager;
    private readonly IHookManager _hookManager;
    private readonly IModSharp _modsharp;
    private readonly IEntityManager _entityManager;
    private readonly IInfect _infect;
    private readonly IGrenadeEffect _grenadeEffect;
    private readonly IWeapons _weapons;
    private readonly ICvarServices _cvarServices;
    private readonly IEconItemManager _econItemManager;

    public Hooks(ISharedSystem sharedSystem, IPlayerManager playerManager, IInfect infect, IGrenadeEffect grenadeEffect, IWeapons weapons, ICvarServices cvarServices)
    {
        _sharedSystem = sharedSystem;
        _playerManager = playerManager;
        _hookManager = _sharedSystem.GetHookManager();
        _modsharp = _sharedSystem.GetModSharp();
        _entityManager = _sharedSystem.GetEntityManager();
        _infect = infect;
        _grenadeEffect = grenadeEffect;
        _weapons = weapons;
        _cvarServices = cvarServices;
        _econItemManager = _sharedSystem.GetEconItemManager();
    }

    public void Init()
    {
        _hookManager.PlayerWeaponCanEquip.InstallHookPre(OnCanEquip);
        _hookManager.PlayerGetMaxSpeed.InstallHookPre(OnGetMaxSpeed);
        _hookManager.PlayerDispatchTraceAttack.InstallHookPre(OnTakeDamage);
        _hookManager.GiveNamedItem.InstallHookPost(OnGiveNamedItemPost);
        _hookManager.PlayerCanAcquire.InstallHookPre(OnCanAcquire);
    }

    public void Shutdown()
    {
        _hookManager.PlayerWeaponCanEquip.RemoveHookPre(OnCanEquip);
        _hookManager.PlayerGetMaxSpeed.RemoveHookPre(OnGetMaxSpeed);
        _hookManager.PlayerDispatchTraceAttack.RemoveHookPre(OnTakeDamage);
        _hookManager.GiveNamedItem.RemoveHookPost(OnGiveNamedItemPost);
        _hookManager.PlayerCanAcquire.RemoveHookPre(OnCanAcquire);
    }

    private HookReturnValue<float> OnGetMaxSpeed(IPlayerGetMaxSpeedHookParams param, HookReturnValue<float> result)
    {
        var client = param.Controller.GetGameClient();

        if(client == null)
            return result;

        var player = _playerManager.GetOrCreatePlayer(client);

        if(player.ActiveClass != null)
        {
            return new HookReturnValue<float>(EHookAction.SkipCallReturnOverride, player.ActiveClass.Speed);
        }

        return result;
    }

    private HookReturnValue<bool> OnCanEquip(IPlayerWeaponCanEquipHookParams param, HookReturnValue<bool> result)
    {
        var player = param.Client;
        var weapon = param.Weapon;

        //var isInfected = _player.IsClientInfected(player);
        //_modsharp.PrintChannelAll(HudPrintChannel.Chat, $"Zombie Status: {isInfected}");
        // just in case.
        var client = _playerManager.GetOrCreatePlayer(player);

        // if player is infect and weapon is knife then ignore all of it.
        if (client.IsInfected() && !weapon.IsKnife)
        {
            //_modsharp.PrintChannelAll(HudPrintChannel.Chat, $"This is {EHookAction.SkipCallReturnOverride} and {result.ReturnValue}");
            return new HookReturnValue<bool>(EHookAction.SkipCallReturnOverride, false);
        }

        // we check grenade number here.
        var weapons = param.Pawn.GetWeaponService()?.GetMyWeapons();
        
        if(weapons != null)
        {
            foreach (var item in weapons)
            {
                if(_entityManager.FindEntityByHandle(item)?.ItemDefinitionIndex == (ushort)EconItemId.Hegrenade)
                {
                    if(!client.AllowExtraGrenade && weapon.ItemDefinitionIndex == (ushort)EconItemId.Hegrenade)
                        return new HookReturnValue<bool>(EHookAction.SkipCallReturnOverride, false);
                }
            }
        }

        //_modsharp.PrintChannelAll(HudPrintChannel.Chat, $"This is {result.Action} and {result.ReturnValue}");
        return result;
    }

    private HookReturnValue<long> OnTakeDamage(IPlayerDispatchTraceAttackHookParams param, HookReturnValue<long> result)
    {
        var attacker = _entityManager.FindEntityByHandle(param.AttackerPawnHandle)?.GetOriginalController()?.GetGameClient();

        var client = param.Controller.GetGameClient();
        var victimPawn = param.Controller.AsPlayerPawn(true);

        if (attacker == null || client == null)
        {
            return result;
        }
        var attackerPlayer = _playerManager.GetOrCreatePlayer(attacker);
        var victimPlayer = _playerManager.GetOrCreatePlayer(client);

        // prevent infected stab to death for humans.
        if(attackerPlayer.IsInfected() && victimPlayer.IsHuman())
        {
            param.Damage = 1;
            return result;
        }

        if(victimPlayer.IsHuman())
        {
            var inflictor = _entityManager.FindEntityByHandle(param.InflictorHandle);
            //_modsharp.PrintToChatAll($"Inflictor: {inflictor?.Classname} | Attacker: {attacker.Name} | Victim: {client.Name} | Owner: {inflictor?.OwnerEntity?.AsPlayerPawn()?.GetController()?.PlayerName}");
            if(inflictor?.Classname.Contains("inferno") ?? false )
            {
                if(attacker == client)
                {
                    //_modsharp.PrintToChatAll($"Prevent self ignite for player {client.Name}");
                    return new HookReturnValue<long>(EHookAction.SkipCallReturnOverride, 0);
                }
            }
        }

        if(attackerPlayer.IsHuman() && victimPlayer.IsInfected())
        {
            var inflictor = _entityManager.FindEntityByHandle(param.InflictorHandle);
            // _modsharp.PrintToChatAll($"Player {client.Name} has been tased by {inflictor?.Classname}");
            var activeWeapon = attacker.GetPlayerController()?.GetPlayerPawn()?.GetActiveWeapon();

            if(attackerPlayer.PowerKnifeActive && (activeWeapon?.IsKnife ?? false))
            {
                param.Damage = _cvarServices.CvarList["Cvar_ZKnifeDamage"]?.GetInt32() ?? 20000;
            }

            if(inflictor?.Classname.Contains("hegrenade") ?? false)
            {
                var duration = victimPlayer.ActiveClass?.NapalmDuration ?? 0.0f;

                if(duration > 0.0f)
                {
                    _grenadeEffect.IgnitePawn(param.Pawn, (int)param.Damage, duration);
                }
            }

            if(activeWeapon?.Classname.Contains("taser") ?? false)
            {
                // _modsharp.PrintToChatAll($"Player {client.Name} has been tased by {attacker.Name}");
                param.Damage = 5000;
            }
        }

        return result;
    }

    private void OnGiveNamedItemPost(IGiveNamedItemHookParams param, HookReturnValue<IBaseWeapon> result)
    {
        /*
        if(result.ReturnValue != null || (result.ReturnValue?.IsValid() ?? false))
        {
            _modsharp.PushTimer(() => {
                result.ReturnValue.GetWeaponData().PrimaryReserveAmmoMax = 1200;
                result.ReturnValue.ReserveAmmo = 1200;
            }, 1.0f);
        }
        */
    }

    private HookReturnValue<EAcquireResult> OnCanAcquire(IPlayerCanAcquireHookParams param, HookReturnValue<EAcquireResult> result)
    {
        var index = param.ItemDefinitionIndex;
        var econItem = _econItemManager.GetEconItemDefinitionByIndex(index);
        var definationName = econItem?.DefinitionName;

        // _modsharp.PrintToChatAll($"Player {param.Client.Name} has received {name} | {className} | {definationName}");

        if(definationName != null)
        {
            if(param.Method == EAcquireMethod.PickUp)
            {
                var restrict = _weapons.IsWeaponRestricted(definationName);

                if(restrict)
                    return new HookReturnValue<EAcquireResult>(EHookAction.SkipCallReturnOverride, EAcquireResult.NotAllowedByProhibition);
            }
            else if(param.Method == EAcquireMethod.Buy)
            {
                if(_playerManager.GetOrCreatePlayer(param.Client).IsInfected())
                {
                    param.Controller.Print(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} You have to be human to purchase weapon!");
                    return new HookReturnValue<EAcquireResult>(EHookAction.SkipCallReturnOverride, EAcquireResult.NotAllowedByProhibition);
                }
                
                var weaponTarget = _weapons.GetWeaponDataWithEntityName(definationName);

                if(weaponTarget != null)
                {
                    _weapons.PurchaseWeapon(param.Client, weaponTarget);
                    return new HookReturnValue<EAcquireResult>(EHookAction.SkipCallReturnOverride, EAcquireResult.NotAllowedByProhibition);
                }
            }
        }

        return result;


    }
    
}
