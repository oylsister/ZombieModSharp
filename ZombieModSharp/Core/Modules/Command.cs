using Sharp.Extensions.CommandManager;
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.ClientPreferences.Shared;
using Sharp.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using System.Runtime.InteropServices;
using ZombieModSharp.Abstractions;
using static ZombieModSharp.Abstractions.IGlowServices;

namespace ZombieModSharp.Core.Modules;

public class Command : ICommand
{
    private readonly IPlayerManager _playerManager;
    private readonly IZTele _ztele;
    private readonly IInfect _infect;
    private readonly ISharedSystem _sharedSystem;
    private readonly IModSharp _modsharp;
    private readonly ICommandManager _command;
    private readonly ICvarServices _cvarServices;
    private readonly IGrenadeEffect _grenadeEffect;
    private readonly ILeaderServices _leaderServices;
    private readonly IGlowServices _glowServices;
    private readonly IMarkerServices _markerServices;
    private readonly IPlayerClasses _playerClasses;

    private IAdminCommandRegistry? _adminManager;

    public Command(IPlayerManager playerManager, IZTele ztele, IInfect infect, ISharedSystem sharedSystem, ICommandManager command, ICvarServices cvarServices, IGrenadeEffect grenadeEffect, ILeaderServices leader, IGlowServices glowServices, IMarkerServices markerServices, IPlayerClasses playerClasses)
    {
        _playerManager = playerManager;
        _ztele = ztele;
        _infect = infect;
        _sharedSystem = sharedSystem;
        _modsharp = _sharedSystem.GetModSharp();
        _command = command;
        _cvarServices = cvarServices;
        _grenadeEffect = grenadeEffect;
        _leaderServices = leader;
        _glowServices = glowServices;
        _markerServices = markerServices;
        _playerClasses = playerClasses;
    }

    public void GetAdminManager(IModSharpModuleInterface<IAdminManager>? adminManager)
    {
        _adminManager = adminManager?.Instance?.GetCommandRegistry("ZombieModSharp");
    }

    public void PostInit()
    {
        _command.RegisterClientCommand("ztele", ZTeleCommand);
        _command.RegisterClientCommand("zsound", ZSoundCommand);
        _command.RegisterClientCommand("zclass", ZClassCommand);
        _command.RegisterClientCommand("vl", VoteLeaderCommand);
        _command.RegisterClientCommand("kev", KevlarCommand);
        _command.RegisterClientCommand("kevlar", KevlarCommand);

        _command.RegisterClientCommand("zspawn", ZSpawnCommand);
        _command.RegisterClientCommand("zammo", InfiniteAmmoCommand);
        _command.RegisterClientCommand("zknife", PowerKnifeCommand);
    }

    public void RegisterAdminCommand()
    {
        _adminManager?.RegisterAdminCommand("infect", InfectCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("human", HumanizeCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("togglerespawn", ToggleRespawnCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("burnme", BurnTestCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("extragrenade", ExtraGrenadeTest, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("jl", OnLeaderCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("setleader", OnLeaderCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("leader", OnLeaderCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("ql", OnQuitLeaderCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("removeleader", OnQuitLeaderCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("pm", OnMarkerCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("dm", OnDisableMarkerCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("glow", OnGlowCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("disglow", OnDisableGlowCommand, ["admin:slay"]);
        _adminManager?.RegisterAdminCommand("testmode", OnTestModeCommand, ["admin:slay"]);
    }

    private void KevlarCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid) return;

        var controller = client.GetPlayerController();
        if (controller == null || !controller.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }

        var pawn = controller.GetPlayerPawn();
        if (pawn == null || !pawn.IsValid())
        {
            ReplyToCommand(client, "Can't find any player pawn");
            return;
        }

        if(!pawn.IsAlive)
        {
            ReplyToCommand(client, "You need to be alive to use this command!");
            return;
        }

        if(_infect.IsClientInfect(client))
        {
            ReplyToCommand(client, "Zombies can't buy kevlar!");
            return;
        }

        if(pawn.ArmorValue >= 100)
        {
            ReplyToCommand(client, "You already have full kevlar!");
            return;
        }

        pawn.ArmorValue = 100;
        ReplyToCommand(client, "You have purchased kevlar. Use this command again to purchase if you don't have one.");
        controller.GetInGameMoneyService()?.Account -= 1000;
    }

    private void VoteLeaderCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid) return;

        if (command.ArgCount < 1)
        {
            ReplyToCommand(client, "Usage: vl <target>");
            return;
        }

        var arg = command.GetArg(1);
        var target = GetTargets(client, arg).FirstOrDefault();

        if (target == null || !target.IsValid)
        {
            ReplyToCommand(client, "Can't find any player");
            return;
        }

        // 已經是 leader
        var targetController = target.GetPlayerController();
        if (targetController == null || !targetController.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }

        if (_leaderServices.IsClientLeader(targetController))
        {
            ReplyToCommand(client, $"{target.Name} is already a leader.");
            return;
        }

        _leaderServices.VoteLeader(client, target);
    }
    public void OnGlowCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid) return;

        var arg = command.GetArg(1);
        var target = GetTargets(client, arg).FirstOrDefault();

        if (target == null || !target.IsValid)
        {
            ReplyToCommand(client, "Can't find any player");
            return;
        }

        var controller = target.GetPlayerController();
        if (controller == null || !controller.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }
        var pawn = controller.GetPlayerPawn();

        if (pawn == null)
        {
            ReplyToCommand(client, $"Entity {controller.PlayerName} can't Glow");
            return;
        }

        byte randomByteR = (byte)Random.Shared.Next(0, 256);
        byte randomByteG = (byte)Random.Shared.Next(0, 256);

        _glowServices.CreateGlow(
            target,
            pawn,
            new Color32(randomByteR, randomByteG, 0, 255),
            0,
            IGlowServices.GlowVisibleMode.ExceptTarget
        );

        ReplyToCommand(client, $"{controller.PlayerName} Glow.");
        
    }

    public void OnDisableGlowCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid)
            return;

        var arg = command.GetArg(1);
        var target = GetTargets(client, arg).FirstOrDefault();

        if (target == null || !target.IsValid)
        {
            ReplyToCommand(client, "Can't find any player");
            return;
        }
        var controller = target.GetPlayerController();
        if (controller == null || !controller.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }

        _glowServices.DisablePlayerGlow(controller);
        ReplyToCommand(client, $"Player {controller.PlayerName} disable glow!");
        
    }
    private void OnMarkerCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid) return;

        var controller = client.GetPlayerController();
        if (controller is null || !controller.IsValid()) return;

        var pawn = controller.GetPawn();
        if (pawn == null || !pawn.IsValid() || !pawn.IsAlive)
        {
            ReplyToCommand(client, "You need to be alive and valid to place marker!");
            return;
        }

        const float maxDistance = 3000f;
        var eyePos = pawn.GetEyePosition();
        var eyeAngles = pawn.GetEyeAngles();
        var forward = eyeAngles.AnglesToVectorForward();
        var endPos = eyePos + forward * maxDistance;

        var trace = _sharedSystem.GetPhysicsQueryManager().TraceLineNoPlayers(
            eyePos,
            endPos,
            UsefulInteractionLayers.PlayerPing,
            (CollisionGroupType)3,
            TraceQueryFlag.All,
            InteractionLayers.None
        );

        var hitPos = trace.DidHit() ? trace.HitPoint : trace.EndPosition;
        var placePos = hitPos + new Vector(0, 0, 1.0f);

        if (_markerServices.CreateMarker(client, placePos))
            ReplyToCommand(client, "Placed the marker.");
        else
            ReplyToCommand(client, "Marker Placed failed.");
    }

    private void OnDisableMarkerCommand(IGameClient? client, StringCommand command)
    {
        _markerServices.DisableLastMarker();
        ReplyToCommand(client, "Your marker has been removed.");
        return;
    }


    public void OnLeaderCommand(IGameClient? client, StringCommand command)
    {
        if (command.ArgCount < 1)
        {
            ReplyToCommand(client, "Usage: ms_leader <target>");
            return;
        }

        var arg = command.GetArg(1);
        var target = GetTargets(client, arg).FirstOrDefault();
        
        if (target == null || !target.IsValid)
        {
            ReplyToCommand(client,"Can't find any player");
            return;
        }

        var target_controller = target.GetPlayerController();
        if (target_controller == null || !target_controller.IsValid())
        {
            ReplyToCommand(client,"Can't find any player controller");
            return;
        }

        if (_leaderServices.IsClientLeader(target_controller))
        {
            ReplyToCommand(client,$"{target.Name} is already a leader.");
            return;
        }

        if (_leaderServices.AssignLeader(target_controller))
        {
            ReplyToCommand(client,$"{target_controller.GetGameClient()?.Name} is leader now.");

            target_controller.SetClanTag(" [Leader]  ");
            _leaderServices.UpdateClientClanTags();
            var pawn = target_controller.GetPlayerPawn();
            if (pawn != null && pawn.IsValid())
            {
                var mode = IGlowServices.GlowVisibleMode.ExceptTarget;
                _glowServices.CreateGlow(
                    target_controller.GetGameClient()!,
                    pawn,
                    new Color32(0, 255, 0, 255),
                    5000,
                    mode
                );
            }
        }
        else
        {
            ReplyToCommand(client,$"Failed to assign leader to {target_controller.PlayerName}");
        }
        
    }

    public void OnQuitLeaderCommand(IGameClient? client, StringCommand command)
    {
        if (command.ArgCount < 1)
        {
            ReplyToCommand(client, "Usage: ms_removeleader <target>");
            return;
        }

        var arg = command.GetArg(1);
        var target = GetTargets(client, arg).FirstOrDefault();

        if (target == null || !target.IsValid)
        {
            ReplyToCommand(client, "Can't find any player");
            return;
        }

        var target_controller = target.GetPlayerController();
        if (target_controller == null || !target_controller.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }

        if (!_leaderServices.IsClientLeader(target_controller))
        {
            ReplyToCommand(client, $"{target_controller.PlayerName} is not a leader.");
            return;
        }

        if (_leaderServices.RemoveLeader(target_controller))
        {
            target_controller.SetClanTag("");
            _leaderServices.UpdateClientClanTags();
            ReplyToCommand(client, $"{target_controller.PlayerName} has quit being a leader.");
            _glowServices.DisablePlayerGlow(target_controller);
        }
        else
        {
            ReplyToCommand(client, $"Failed to quit leader for {target_controller.PlayerName}");
        }

        
    }

    private void ZTeleCommand(IGameClient client, StringCommand command)
    {
        var playerInfo = _playerManager.GetOrCreatePlayer(client);

        if (client == null || playerInfo == null)
            return;
        
        var allow = _cvarServices.CvarList["Cvar_ZTeleAllow"]?.GetBool();

        if(allow.HasValue && !allow.Value)
        {
            ReplyToCommand(client, "This feature is not available.");
            return;
        }

        var delay = _cvarServices.CvarList["Cvar_ZTeleDelay"]?.GetFloat();

        if(delay > 0)
        {
            ReplyToCommand(client, $"Teleport back to spawn in {delay} seconds.");
            _modsharp.PushTimer(new Func<TimerAction>(() => 
            {
                _ztele.TeleportToSpawn(client);
                return TimerAction.Continue;
            }), delay.Value, GameTimerFlags.StopOnRoundEnd|GameTimerFlags.StopOnMapEnd);
        }
        else
        {
            ReplyToCommand(client, $"Teleport back to spawn in.");
        }

        return;
    }

    private void InfectCommand(IGameClient? client, StringCommand command)
    {
        if (command.ArgCount < 1)
        {
            ReplyToCommand(client, "Usage: ms_infect <target>");
            return;
        }

        if(_modsharp.GetGameRules().IsWarmupPeriod && (!_cvarServices.CvarList["Cvar_InfectWarmupEnabled"]?.GetBool() ?? false))
        {
            ReplyToCommand(client, "The infection during the warmup is not available.");
            return;
        }

        if (_infect.IsTestMode())
        {
            ReplyToCommand(client, "The infection during the test mode is not available.");
            return;
        }

        var arg = command.GetArg(1);
        var target = GetTargets(client, arg);

        if (target == null || target.Count == 0)
        {
            ReplyToCommand(client, "No target is found");
            return;
        }

        var motherzombie = !_infect.IsInfectStarted();

        foreach (var player in target)
        {
            _infect.InfectPlayer(player, null, motherzombie, true);
            _modsharp.PrintChannelAll(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Admin {client?.Name} has infected {player.Name} via command");
        }

        return;
    }

    private void HumanizeCommand(IGameClient? client, StringCommand command)
    {
        if (command.ArgCount < 1)
        {
            ReplyToCommand(client, "Usage: ms_human <target>");
            return;
        }

        var arg = command.GetArg(1);
        var target = GetTargets(client, arg);

        if (target == null || target.Count == 0)
        {
            ReplyToCommand(client, "No target is found");
            return;
        }

        foreach (var player in target)
        {
            _infect.HumanizeClient(player, true);
            _modsharp.PrintChannelAll(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Admin {client?.Name} has revived {player.Name} via command");
        }

        return;
    }

    private void ZSoundCommand(IGameClient client, StringCommand command)
    {
        var player = _playerManager.GetOrCreatePlayer(client);
        var volume = player.SoundVolume;

        if(command.ArgCount < 1)
        {
            player.SoundEnabled = !player.SoundEnabled;
            // _modsharp.PrintToChatAll("This shit is not even one");
        }

        else
        {
            var arg = command.GetArg(1);

            // we need to check if arg is number or not.
            if(!int.TryParse(arg, out volume))
            {
                // we just keep the same value.
                volume = player.SoundVolume;
            }

            if(volume < 0 || volume > 100)
            {
                ReplyToCommand(client, $"Usage: ms_zsound <0-100>");
                return;
            }

            player.SoundVolume = volume;
        }

        var preferences = _sharedSystem.GetSharpModuleManager()
                                       .GetRequiredSharpModuleInterface<IClientPreference>(IClientPreference.Identity)
                                       .Instance!;
        preferences.SetCookie(client.SteamId, global::ZombieModSharp.ZombieModSharp.SoundEnabledCookieKey, player.SoundEnabled.ToString());
        preferences.SetCookie(client.SteamId, global::ZombieModSharp.ZombieModSharp.SoundVolumeCookieKey, player.SoundVolume.ToString());
        ReplyToCommand(client, $"You have{(player.SoundEnabled ? "\x05 Enabled" : "\x07 Disabled")}\x01 zombie sound. {(player.SoundEnabled ? $"And set volume to\x06 {player.SoundVolume}" : string.Empty)}");
    }

    private void ZClassCommand(IGameClient client, StringCommand command)
    {
        _playerClasses.PlayerClassMenu(client);
    }

    private void ToggleRespawnCommand(IGameClient? client, StringCommand command)
    {
        if(command.ArgCount < 1)
        {
            var enabled = RespawnServices.IsRespawnEnabled();
            RespawnServices.SetRespawnEnable(!enabled);

            _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} Respawn has been{(!enabled ? "\x07 Disabled" : "\x05 Enabled")}");
            return;
        }

        if(!int.TryParse(command.GetArg(1), out var arg))
        {
            ReplyToCommand(client, "Usage ms_togglerespawn <0-1>");
            return;
        }

        if(arg > 1 || arg < 0)
        {
            ReplyToCommand(client, "Usage ms_togglerespawn <0-1>");
            return;
        }

        RespawnServices.SetRespawnEnable(Convert.ToBoolean(arg));
        _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} Respawn has been{(Convert.ToBoolean(arg) ? "\x05 Enabled" : "\x07 Disabled")}");
    }

    private void BurnTestCommand(IGameClient? client, StringCommand command)
    {
        var player = client?.GetPlayerController()?.GetPlayerPawn();

        ReplyToCommand(client, "Test burn");
        _grenadeEffect.IgnitePawn(player, 1, 5);
    }

    private void ExtraGrenadeTest(IGameClient? client, StringCommand command)
    {
        var player = _playerManager.GetOrCreatePlayer(client);

        player.AllowExtraGrenade = !player.AllowExtraGrenade;
        ReplyToCommand(client, $"AllowExtraGrenade: {player.AllowExtraGrenade}");
    }

    private void InfiniteAmmoCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid) return;

        var allow = _cvarServices.CvarList["Cvar_ZAmmoAllow"]?.GetBool();
        if (allow.HasValue && !allow.Value)
        {
            ReplyToCommand(client, "This feature is not available.");
            return;
        }

        if (!_infect.IsInfectStarted())
        {
            ReplyToCommand(client, "You can only use this after infection has started!");
            return;
        }

        var controller = client.GetPlayerController();
        if (controller == null || !controller.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }

        var pawn = controller.GetPlayerPawn();
        if (pawn == null || !pawn.IsValid())
        {
            ReplyToCommand(client, "Can't find any player pawn");
            return;
        }

        if (!pawn.IsAlive)
        {
            ReplyToCommand(client, "You need to be alive to use this command!");
            return;
        }

        if (_infect.IsClientInfect(client))
        {
            ReplyToCommand(client, "Zombies can't use this!");
            return;
        }

        var player = _playerManager.GetOrCreatePlayer(client);

        if (player.InfiniteAmmo)
        {
            ReplyToCommand(client, "Infinite ammo is already active!");
            return;
        }

        var cost = _cvarServices.CvarList["Cvar_ZAmmoCost"]?.GetInt32() ?? 7500;
        var duration = _cvarServices.CvarList["Cvar_ZAmmoDuration"]?.GetFloat() ?? 15.0f;
        var usesPerRound = _cvarServices.CvarList["Cvar_ZAmmoUsesPerRound"]?.GetInt32() ?? 1;
        var money = controller.GetInGameMoneyService()?.Account;

        if (player.InfiniteAmmoUsesThisRound >= usesPerRound)
        {
            ReplyToCommand(client, $"You can only use infinite ammo {usesPerRound} time(s) per round!");
            return;
        }

        if (money < cost)
        {
            ReplyToCommand(client, $"You don't have enough cash! (Required: {cost}$)");
            return;
        }

        controller.GetInGameMoneyService()!.Account -= cost;
        player.InfiniteAmmo = true;
        player.InfiniteAmmoUsesThisRound++;
        ReplyToCommand(client, $"Infinite ammo activated for {duration} seconds!");
        
        _modsharp.PushTimer(new Func<TimerAction>(() =>
        {
            player.InfiniteAmmo = false;
            ReplyToCommand(client, "Infinite ammo has expired!");
            return TimerAction.Stop;
        }), duration, GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
    }

    private void PowerKnifeCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid) return;

        var allow = _cvarServices.CvarList["Cvar_ZKnifeAllow"]?.GetBool();
        if (allow.HasValue && !allow.Value)
        {
            ReplyToCommand(client, "This feature is not available.");
            return;
        }

        if (!_infect.IsInfectStarted())
        {
            ReplyToCommand(client, "You can only use this after infection has started!");
            return;
        }

        var controller = client.GetPlayerController();
        if (controller == null || !controller.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }

        var pawn = controller.GetPlayerPawn();
        if (pawn == null || !pawn.IsValid())
        {
            ReplyToCommand(client, "Can't find any player pawn");
            return;
        }

        if (!pawn.IsAlive)
        {
            ReplyToCommand(client, "You need to be alive to use this command!");
            return;
        }

        if (_infect.IsClientInfect(client))
        {
            ReplyToCommand(client, "Zombies can't use this!");
            return;
        }

        var player = _playerManager.GetOrCreatePlayer(client);

        if (player.PowerKnifeActive)
        {
            ReplyToCommand(client, "Power knife is already active!");
            return;
        }

        var cost = _cvarServices.CvarList["Cvar_ZKnifeCost"]?.GetInt32() ?? 10000;
        var duration = _cvarServices.CvarList["Cvar_ZKnifeDuration"]?.GetFloat() ?? 3.0f;
        var usesPerRound = _cvarServices.CvarList["Cvar_ZKnifeUsesPerRound"]?.GetInt32() ?? 1;
        var money = controller.GetInGameMoneyService()?.Account;

        if (player.PowerKnifeUsesThisRound >= usesPerRound)
        {
            ReplyToCommand(client, $"You can only use power knife {usesPerRound} time(s) per round!");
            return;
        }

        if (money < cost)
        {
            ReplyToCommand(client, $"You don't have enough cash! (Required: {cost}$)");
            return;
        }

        controller.GetInGameMoneyService()!.Account -= cost;
        player.PowerKnifeActive = true;
        player.PowerKnifeUsesThisRound++;
        ReplyToCommand(client, $"Power knife activated for {duration} seconds!");

        _modsharp.PushTimer(new Func<TimerAction>(() =>
        {
            player.PowerKnifeActive = false;
            ReplyToCommand(client, "Power knife has expired!");
            return TimerAction.Continue;
        }), duration, GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
    }

    private void ZSpawnCommand(IGameClient? client, StringCommand command)
    {
        if (client == null || !client.IsValid) return;

        var controller = client.GetPlayerController();
        if (controller == null || !controller.IsValid())
        {
            ReplyToCommand(client, "Can't find any player controller");
            return;
        }

        if(controller.Team == CStrikeTeam.Spectator || controller.Team == CStrikeTeam.UnAssigned)
         {
            ReplyToCommand(client, "You can't use this command in spectator!");
            return;
        }

        var pawn = controller.GetPlayerPawn();

        if (pawn == null || !pawn.IsValid())
        {
            ReplyToCommand(client, "Can't find any player pawn");
            return;
        }

        if(pawn.IsAlive)
        {
            ReplyToCommand(client, "You need to be dead to use this command!");
            return;
        }

        if(!RespawnServices.IsRespawnEnabled())
        {
            ReplyToCommand(client, "Respawn has been disabled.");
            return;
        }

        controller.Respawn();
    }

    private void OnTestModeCommand(IGameClient? client, StringCommand command)
    {
        if (command.ArgCount < 1)
        {
            ReplyToCommand(client, "Usage: testmode <0-1>");
            return;
        }

        if (!int.TryParse(command.GetArg(1), out var arg))
        {
            ReplyToCommand(client, "Usage: testmode <0-1>");
            return;
        }

        if (arg > 1 || arg < 0)
        {
            ReplyToCommand(client, "Usage: testmode <0-1>");
            return;
        }

        _infect.SetTestMode(arg == 1);
        ReplyToCommand(client, $"Admin {client?.Name} has{(arg == 1 ? "\x05 Enabled" : "\x07 Disabled")} \x01Test mode.");
    }

    public void ReplyToCommand(IGameClient? client, string text)
    {
        if (client == null)
        {
            Console.WriteLine(text);
            return;
        }

        else
        {
            var receiver = new RecipientFilter(client.Slot);
            _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} {text}", receiver);
        }
    }

    private List<IGameClient> GetTargets(IGameClient? sender, string target)
    {
        var targets = new List<IGameClient>();

        if (string.Equals(target, "@all", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(_playerManager.GetAllPlayers().Select(p => p.Key));
        }
        else if (string.Equals(target, "@me", StringComparison.OrdinalIgnoreCase))
        {
            if (sender != null)
                targets.Add(sender);
        }
        else if (string.Equals(target, "@zombies", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(_playerManager.GetAllPlayers().Where(p => p.Value.IsInfected()).Select(p => p.Key));
        }
        else if (string.Equals(target, "@humans", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(_playerManager.GetAllPlayers().Where(p => !p.Value.IsInfected()).Select(p => p.Key));
        }
        else if (string.Equals(target, "@ct", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(_playerManager.GetAllPlayers().Where(p =>
                p.Key.GetPlayerController()?.Team == CStrikeTeam.CT).Select(p => p.Key));
        }
        else if (string.Equals(target, "@t", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(_playerManager.GetAllPlayers().Where(p =>
                p.Key.GetPlayerController()?.Team == CStrikeTeam.TE).Select(p => p.Key));
        }
        else if (string.Equals(target, "@bot", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(_playerManager.GetAllPlayers().Where(p => p.Key.IsFakeClient && !p.Key.IsHltv).Select(p => p.Key));
        }

        // find the name of 
        else
        {
            targets.AddRange(_playerManager.GetAllPlayers().Where(p => p.Key.Name.Contains(target, StringComparison.OrdinalIgnoreCase)).Select(p => p.Key));
        }

        return targets;
    }
}
