using Microsoft.Extensions.Logging;
using Sharp.Extensions.GameEventManager;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEvents;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Core.HookManager;

public class Events : IEvents
{
    private readonly ISharedSystem _sharedSystem;
    // private readonly IEventManager _eventManager;
    private readonly IGameEventManager _gameEventManager;
    private readonly ILogger<Events> _logger;
    private readonly IPlayerManager _playerManager;
    private readonly IInfect _infect;
    private readonly IModSharp _modSharp;
    private readonly IZTele _ztele;
    private readonly IKnockback _knockback;
    private readonly ICvarServices _cvarServices;
    private readonly ISoundServices _soundServices;
    private readonly IRespawnServices _respawnServices;
    private readonly IGlowServices _glowServices;
    private readonly ILeaderServices _LeaderServices;
    private readonly IConVarManager _convarManager;

    public int ListenerVersion => IEventListener.ApiVersion;
    public int ListenerPriority => 0;

    public bool RoundEnded { get; private set; } = false;
    private Guid _roundTimer = Guid.Empty;

    public Events(ISharedSystem sharedSystem, IGameEventManager gameEventManager, ILogger<Events> logger, IPlayerManager playerManager, IInfect infect, IZTele ztele, IKnockback knockback, ICvarServices cvarServices, ISoundServices soundServices, IRespawnServices respawnServices, IGlowServices glowMethod, ILeaderServices leaderServices)
    {
        _sharedSystem = sharedSystem;
        _gameEventManager = gameEventManager;
        _logger = logger;
        _playerManager = playerManager;
        _modSharp = _sharedSystem.GetModSharp();
        _infect = infect;
        _ztele = ztele;
        _knockback = knockback;
        _cvarServices = cvarServices;
        _soundServices = soundServices;
        _respawnServices = respawnServices;
        _glowServices = glowMethod;
        _LeaderServices = leaderServices;
        _convarManager = _sharedSystem.GetConVarManager();
    }

    public void Init()
    {
        RegisterEvents();
    }

    public void RegisterEvents()
    {
        _gameEventManager.ListenEvent("player_hurt", OnPlayerHurt);
        _gameEventManager.ListenEvent("player_death", OnPlayerDeath);
        _gameEventManager.ListenEvent("player_spawn", OnPlayerSpawn);
        _gameEventManager.ListenEvent("round_end", OnRoundEnd);
        _gameEventManager.ListenEvent("cs_pre_restart", OnPreRestart);
        _gameEventManager.ListenEvent("round_start", OnRoundStart);
        _gameEventManager.ListenEvent("round_freeze_end", OnRoundFreezeEnd);
        _gameEventManager.ListenEvent("warmup_end", OnWarmupEnd);
        _gameEventManager.ListenEvent("weapon_fire", OnWeaponFired);
    }

    private void OnPlayerHurt(IGameEvent e)
    {
        var client = e.GetPlayerController("userid")?.GetGameClient();
        var attackerClient = e.GetPlayerController("attacker")?.GetGameClient();
        var weapon = e.GetString("weapon");

        if (client == null || attackerClient == null)
        {
            return;
        }

        if (_infect.IsInfectStarted() == false)
        {
            return;
        }

        var zmClient = _playerManager.GetOrCreatePlayer(client);
        var zmAttacker = _playerManager.GetOrCreatePlayer(attackerClient);

        var hitGroup = e.GetInt("hitgroup");

        if (zmClient.IsHuman() && zmAttacker.IsInfected())
        {
            if(_infect.IsTestMode())
            {      
                return;
            }

            if (weapon.Contains("knife"))
            {
                // Infect the player.
                _infect.InfectPlayer(client, attackerClient);
            }
        }
        else if (zmClient.IsInfected() && zmAttacker.IsHuman())
        {
            // Get weapon and calculate damage and knockback.
            var damage = e.GetInt("dmg_health");
            var pawn = client.GetPlayerController()?.GetPlayerPawn();

            zmAttacker.TotalDamage += damage;

            if(pawn != null)
            {
                if(Infect.CashMultiply > 0)
                {
                    var accountService = attackerClient.GetPlayerController()?.GetInGameMoneyService();

                    if(accountService != null)
                    {
                        accountService.Account += (int)Math.Ceiling(damage * Infect.CashMultiply);
                    }
                }

                _soundServices.ZombieHurtSound(pawn);
                _knockback.KnockbackClient(client, attackerClient, weapon, damage, hitGroup);
            }
        }
    }

    private void OnPlayerDeath(IGameEvent e)
    {
        //_infect.CheckGameStatus();

        var client = e.GetPlayerController("userid")?.GetGameClient();
        var controller = e.GetPlayerController("userid");

        if(client == null)
            return;

        if (controller != null && controller.IsValid())
        {
            // �p�G�O Leader�A���`������ Glow
            if (_LeaderServices.IsClientLeader(controller))
            {
                _glowServices.DisablePlayerGlow(controller);
            }
        }

        if(_playerManager.GetOrCreatePlayer(client).IsInfected())
        {
            var pawn = e.GetPlayerController("userid")?.GetPlayerPawn();

            if(pawn != null)
                _soundServices.EmitZombieSound(pawn, "zr.amb.zombie_die");
        }

        _respawnServices.InitRespawn(controller);
        _infect.CheckGameStatus();
        //_modSharp.PrintChannelAll(HudPrintChannel.Chat, $"Client {client?.Name ?? "Unknown Player"} killed by {attackerClient?.Name ?? "Unknown Player"}");
    }

    private void OnPreRestart(IGameEvent e)
    {
        _infect.OnRoundPreStart();
    }

    private void OnRoundFreezeEnd(IGameEvent e)
    {
        // start infection.
        // _modSharp.PrintChannelAll(HudPrintChannel.Chat, "Infect round freeze is called");
        _infect.OnRoundFreezeEnd();
        var timer = _convarManager.FindConVar("mp_roundtime")?.GetFloat() ?? 15.0f;
        _roundTimer = _modSharp.PushTimer(() =>
        {
            _modSharp.GetGameRules().TerminateRound(8.0f, RoundEndReason.RoundDraw, true);
        }, timer * 60.0f, GameTimerFlags.StopOnRoundEnd|GameTimerFlags.StopOnMapEnd);
    }

    private void OnRoundStart(IGameEvent e)
    {
        RoundEnded = false;
        //_modSharp.PrintChannelAll(HudPrintChannel.Chat, $"The round just started");
        _infect.OnRoundStart();

        if(_roundTimer != Guid.Empty)
        {
            _modSharp.StopTimer(_roundTimer);
            _roundTimer = Guid.Empty;
        }

        if(_cvarServices.CvarList["Cvar_RespawnEnabled"]?.GetBool() ?? false)
            RespawnServices.SetRespawnEnable(true);

        if(_cvarServices.CvarList["Cvar_RespawnTogglerEnable"]?.GetBool() ?? false)
            _respawnServices.SetupRespawnToggler();
    }

    private void OnRoundEnd(IGameEvent e)
    {
        RoundEnded = true;
        // _modSharp.PrintChannelAll(HudPrintChannel.Chat, $"The round just ended");
        RespawnServices.SetRespawnEnable(false);
        _infect.OnRoundEnd();
        foreach (var player in _playerManager.GetAllPlayers().Values)
            player.InfiniteAmmo = false;

        if(_roundTimer != Guid.Empty)
        {
            _modSharp.StopTimer(_roundTimer);
            _roundTimer = Guid.Empty;
        }
    }

    private void OnWarmupEnd(IGameEvent e)
    {
        _infect.SetInfectStarted(false);
    }

    private void OnPlayerSpawn(IGameEvent e)
    {
        var pawn = e.GetPlayerController("userid");
        var client = pawn?.GetGameClient();

        // _modSharp.PrintChannelAll(HudPrintChannel.Chat, $"Client {client?.Name ?? "Unknown Player"} Spawned");
        // _logger.LogInformation("PlayerSpawn: {Name}", client?.Name ?? "Unknown Player");

        // go apply spawn stuff.
        // ignore Spec and none team
        if (client == null)
            return;

        var playerPawn = pawn?.GetPlayerPawn();

        if(playerPawn == null)
            return;

        playerPawn.AllowTakesDamage = false;

        var team = pawn?.Team ?? CStrikeTeam.UnAssigned;

        if (team == CStrikeTeam.UnAssigned || team == CStrikeTeam.Spectator)
            return;

        var player = _playerManager.GetOrCreatePlayer(client);

        // we clear all player history here.
        player.PurchaseHistory.Clear();

            // infect or
        _modSharp.PushTimer(() =>
        {
            var teamRespawn = _cvarServices.CvarList["Cvar_RespawnTeam"]?.GetInt32() ?? 0;

            if (_infect.IsInfectStarted())
            {
                if(!_infect.IsTestMode())
                {
                    if(teamRespawn == 0)
                        _infect.InfectPlayer(client);

                    else if(teamRespawn == 1)
                        _infect.HumanizeClient(client);

                    else
                    {
                        var zombie = player.IsInfected();

                        if(zombie)
                            _infect.InfectPlayer(client);

                        else
                            _infect.HumanizeClient(client);
                    }
                }
                else
                {
                    _infect.HumanizeClient(client);
                }
            }
            // regardless of wtf happenned here, before infection start all player should be spawn as human.
            else
            {
                _infect.HumanizeClient(client);

                if(player.AllowExtraGrenade)
                {
                    pawn?.GetPlayerPawn()?.GiveNamedItem(EconItemId.Hegrenade);
                }
            }

            _ztele.OnPlayerSpawn(client);
            // this is for noblock
            var noblock = _cvarServices.CvarList["Cvar_InfectNoblockEnable"]?.GetBool() ?? false;

            // _modSharp.PrintToChatAll($"Noblock = {noblock}");

            if(noblock)
            {
                pawn?.GetPlayerPawn()?.SetCollisionGroup(CollisionGroupType.Debris);
            }
        }, 0.05f, GameTimerFlags.None | GameTimerFlags.StopOnMapEnd | GameTimerFlags.StopOnRoundEnd);
    }

    private void OnWeaponFired(IGameEvent fired)
    {
        var pawn = fired.GetPlayerPawn("userid");
        var client = fired.GetPlayerController("userid")?.GetGameClient();

        var weapon = pawn?.GetWeaponService()?.ActiveWeapon;
        // _modSharp.PrintToChatAll("Fired");

        if(weapon != null && (weapon.Slot == GearSlot.Rifle || weapon.Slot == GearSlot.Pistol))
        {
            //_modSharp.PrintToChatAll("Change");
            weapon.ReserveAmmo += 1;

            if(client != null && _playerManager.GetOrCreatePlayer(client).InfiniteAmmo)
            {
                weapon.Clip = weapon.MaxClip;
            }
        }
    }
}
