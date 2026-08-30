using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Core.HookManager;

public class Listeners : IListeners, IClientListener, IGameListener, IEntityListener
{
    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 0;
    int IEntityListener.ListenerVersion => IEntityListener.ApiVersion;
    int IEntityListener.ListenerPriority => 0;
    int IGameListener.ListenerVersion => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 0;

    private readonly IPlayerManager _playerManager;
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<Listeners> _logger;
    private readonly IModSharp _modsharp;
    private readonly ICvarServices _cvarServices;
    private readonly IPlayerClasses _playerClasses;
    private readonly IPrecacheManager _precacheManager;
    private readonly IRespawnServices _respawnServices;
    private readonly IEntityManager _entityManager;
    private readonly IWeapons _weapons;
    private readonly IGrenadeEffect _grenadeEffect;
    private readonly IMarkerServices _markerServices;
    private readonly IGlowServices _glowServices;
    private readonly ILeaderServices _leaderServices;

    public Listeners(IPlayerManager playerManager, ISharedSystem sharedSystem, ICvarServices cvarServices,
        IPlayerClasses playerClasses, IPrecacheManager precacheManager, IRespawnServices respawnServices,
        IWeapons weapons, IGrenadeEffect grenadeEffect, IMarkerServices markerServices, ILeaderServices leaderServices,
        IGlowServices glowServices)
    {
        _playerManager = playerManager;
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<Listeners>();
        _modsharp = _sharedSystem.GetModSharp();
        _cvarServices = cvarServices;
        _playerClasses = playerClasses;
        _precacheManager = precacheManager;
        _respawnServices = respawnServices;
        _entityManager = _sharedSystem.GetEntityManager();
        _weapons = weapons;
        _grenadeEffect = grenadeEffect;
        _markerServices = markerServices;
        _leaderServices = leaderServices;
        _glowServices = glowServices;
    }

    public void Init()
    {
        var clientManager = _sharedSystem.GetClientManager();

        clientManager.InstallClientListener(this);
        clientManager.InstallCommandListener("jointeam", OnJoinTeamCommand);

        clientManager.InstallCommandListener("player_ping", OnPlayerPing);

        _entityManager.InstallEntityListener(this);
        _entityManager.HookEntityInput("logic_relay", "Trigger");
        _entityManager.HookEntityInput("logic_relay", "Enable");
        _entityManager.HookEntityInput("logic_relay", "Disable");
        _modsharp.InstallGameListener(this);
    }

    public void Shutdown()
    {
        var clientManager = _sharedSystem.GetClientManager();
        clientManager.RemoveClientListener(this);
        clientManager.RemoveCommandListener("jointeam", OnJoinTeamCommand);
        clientManager.RemoveCommandListener("player_ping", OnPlayerPing);

        _entityManager.RemoveEntityListener(this);
        _modsharp.RemoveGameListener(this);
    }

    public void OnClientPutInServer(IGameClient client)
    {
        //_logger.LogInformation("ClientPutInServer: {Name}", client.Name);
        if (client.IsHltv)
            return;

        _playerManager.GetOrCreatePlayer(client);
    }

    public void OnClientDisconnecting(IGameClient client, NetworkDisconnectionReason reason)
    {
        //_logger.LogInformation("ClientDisconnect: {Name}", client.Name);
        if (client.IsHltv)
            return;

        _playerManager.RemovePlayer(client);
    }

    public void OnResourcePrecache()
    {
        // _logger.LogInformation("Precache GoldShip Here");
        // _modsharp.PrecacheResource("characters/models/s2ze/zombie_frozen/zombie_frozen.vmdl");
        // _modsharp.PrecacheResource("particles/leader_a_1.vpcf");
        // _modsharp.PrecacheResource("particles/leader_a_2.vpcf");
        // ^^^ remember to precache a~d vcpf leader markers ^^^

        _precacheManager.PrecacheAllResource();
    }

    public void OnGameActivate()
    {
        // we execute config first.
        _modsharp.ServerCommand($"exec zombiemodsharp/zombiemodsharp.cfg");

        // get map name
        var mapname = _modsharp.GetMapName();

        if (!string.IsNullOrEmpty(mapname)) _modsharp.ServerCommand($"exec zombiemodsharp/{mapname}.cfg");
    }

    public void OnRoundRestarted()
    {
        _markerServices.CleanupAll();
        _glowServices.CleanupAll();
        _leaderServices.ReloadLeaderList(_sharedSystem);

        _modsharp.PushTimer(() =>
        {
            var leaders = _leaderServices.GetAllLeaders();

            foreach (var controller in leaders)
            {
                if (controller == null || !controller.IsValid()) continue;


                var pawn = controller.GetPlayerPawn();
                if (pawn == null || !pawn.IsValid()) continue;

                var client = controller.GetGameClient();
                if (client == null || !client.IsValid) continue;

                var randomByteR = (byte)Random.Shared.Next(0, 256);
                var randomByteG = (byte)Random.Shared.Next(0, 256);

                _glowServices.CreateGlow(
                    client,
                    pawn,
                    new Color32(randomByteR, randomByteG, 0, 255),
                    0,
                    IGlowServices.GlowVisibleMode.ExceptTarget
                );
            }
        }, 0.7f, GameTimerFlags.StopOnMapEnd);
    }

    private ECommandAction OnJoinTeamCommand(IGameClient client, StringCommand command)
    {
        var team = (CStrikeTeam)int.Parse(command.GetArg(1));

        var allowJoinLate = _cvarServices.CvarList["Cvar_RespawnLateJoin"]?.GetBool() ?? false;

        if (team == CStrikeTeam.TE || team == CStrikeTeam.CT)
            if (allowJoinLate)
                _respawnServices.InitRespawn(client.GetPlayerController());

        return ECommandAction.Skipped;
    }

    private ECommandAction OnPlayerPing(IGameClient client, StringCommand command)
    {
        if (!client.IsValid) return ECommandAction.Handled;
        if (!_leaderServices.IsClientLeader(client.GetPlayerController())) return ECommandAction.Handled;


        var controller = client.GetPlayerController();
        var pawn = controller?.GetPawn();
        // get eye position and angles for place marker
        if (pawn != null && pawn.IsAlive)
        {
            var eyePos = pawn.GetEyePosition();
            var eyeAngles = pawn.GetEyeAngles();
            var forward = eyeAngles.AnglesToVectorForward();
            var endPos = eyePos + forward * 3000f;

            var trace = _sharedSystem.GetPhysicsQueryManager().TraceLineNoPlayers(
                eyePos,
                endPos,
                UsefulInteractionLayers.PlayerPing,
                (CollisionGroupType)3,
                TraceQueryFlag.All
            );

            var hitPos = trace.DidHit() ? trace.HitPoint : trace.EndPosition;
            var placePos = hitPos + new Vector(0, 0, 1.0f);

            _markerServices.CreateMarker(client, placePos);
        }

        return ECommandAction.Stopped;
    }

    public EHookAction OnEntityAcceptInput(IBaseEntity entity, string input, in EntityVariant value,
        IBaseEntity? activator, IBaseEntity? caller)
    {
        //_modsharp.PrintToChatAll($"Founded {entity.Name} {input}");

        if (!_cvarServices.CvarList["Cvar_RespawnTogglerEnable"]?.GetBool() ?? false)
            return EHookAction.Ignored;

        var respawner = _respawnServices.GetRespawnToggler();
        if (respawner == null || !respawner.IsValid())
            return EHookAction.Ignored;

        if (entity != respawner)
            return EHookAction.Ignored;

        if (input.Equals("Trigger", StringComparison.OrdinalIgnoreCase))
        {
            _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} Respawn has been disabled!");
            RespawnServices.SetRespawnEnable(false);
        }

        else if (input.Equals("Enable", StringComparison.OrdinalIgnoreCase) && !RespawnServices.IsRespawnEnabled())
        {
            _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} Respawn has been enabled!");
            RespawnServices.SetRespawnEnable();
        }

        else if (input.Equals("Disable", StringComparison.OrdinalIgnoreCase) && RespawnServices.IsRespawnEnabled())
        {
            _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} Respawn has been disabled!");
            RespawnServices.SetRespawnEnable(false);
        }

        return EHookAction.Ignored;
    }

    public void OnEntitySpawned(IBaseEntity entity)
    {
        if (entity.Classname.Contains("weapon_"))
        {
            if (entity.AsBaseWeapon() is not { } weapon) return;

            var name = weapon?.GetItemDefinitionName();

            if (name == null || weapon == null)
                return;

            var ammo = _weapons.GetWeaponAmmo(name);
            // _modsharp.PrintToChatAll($"Weapon name: {name}");

            var vdata = weapon.GetWeaponData();

            if (ammo != null)
            {
                if (ammo.ReserveAmmo > 0)
                {
                    vdata.PrimaryReserveAmmoMax = ammo.ReserveAmmo;
                    weapon.ReserveAmmo = ammo.ReserveAmmo;
                }

                if (ammo.Clip > 0)
                {
                    vdata.MaxClip = ammo.Clip;
                    weapon.Clip = ammo.Clip;
                }
            }
        }

        if (entity.Classname.Contains("_projectile")) entity.SetCollisionGroup(CollisionGroupType.Debris);

        if (entity.Classname.Contains("smokegrenade_projectile") || entity.Classname.Contains("decoy_projectile"))
            _modsharp.PushTimer(() => { _grenadeEffect.ApplyFreeze(entity, 600, 3f); }, 1.3f,
                GameTimerFlags.StopOnMapEnd);

        if (entity.Classname.Contains("flashbang_projectile"))
            _modsharp.PushTimer(() => { _grenadeEffect.ApplyLightGrenade(entity, 15f); }, 1.3f,
                GameTimerFlags.StopOnMapEnd);
    }
}