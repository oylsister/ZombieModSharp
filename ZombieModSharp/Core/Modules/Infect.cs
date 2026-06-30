using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Enums;
using Microsoft.Extensions.Logging;
using ZombieModSharp.Abstractions;
using Sharp.Shared;
using ZombieModSharp.Abstractions.Entities;
using Sharp.Shared.Types;
using ZombieModSharp.Shared;

namespace ZombieModSharp.Core.Modules;

public class Infect : IInfect
{
    private readonly ISharedSystem _sharedSystem;
    private readonly IEntityManager _entityManager;
    private readonly IEventManager _eventManager;
    private readonly ILogger<Infect> _logger;
    private readonly IPlayerManager _player;
    private readonly IModSharp _modSharp;
    private readonly IPlayerClasses _playerClasses;
    private readonly ICvarServices _cvarServices;
    private readonly ISoundServices _soundServices;
    private readonly IZTele _ztele;
    private readonly ILeaderServices _leaderServices;
    private readonly IGlowServices _glowServices;
    private readonly IParticleManager _particleManager;
    private readonly IClientManager _clientManager;

    private bool InfectStarted = false;
    public static float CashMultiply = 1.0f;

    private Guid infectTimer = Guid.Empty;
    private Guid countdownTimer = Guid.Empty;


    public event DelegateInfectPlayer? OnClientInfect;
    public event DelegateHumanizeClient? OnClientHumanize;

    private bool _testMode = false;

    public Infect(ISharedSystem sharedSystem, ILogger<Infect> logger, IPlayerManager player, IPlayerClasses playerClasses, ICvarServices cvarServices, ISoundServices soundServices, IZTele zTele, ILeaderServices leaderServices, IGlowServices glowServices)
    {
        _sharedSystem = sharedSystem;
        _entityManager = _sharedSystem.GetEntityManager();
        _eventManager = _sharedSystem.GetEventManager();
        _logger = logger;
        _player = player;
        _modSharp = _sharedSystem.GetModSharp();
        _playerClasses = playerClasses;
        _cvarServices = cvarServices;
        _soundServices = soundServices;
        _ztele = zTele;
        _leaderServices = leaderServices;
        _glowServices = glowServices;
        _particleManager = _sharedSystem.GetParticleManager();
        _clientManager = _sharedSystem.GetClientManager();
    }

    public void InfectPlayer(IGameClient client, IGameClient? attacker = null, bool motherzombie = false, bool force = false)
    {
        if (client == null)
        {
            return;
        }

        if(IsTestMode() && !force)
        {
            _modSharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Infection is skipped because test mode is enabled.", new RecipientFilter(client));
            return;
        }

        var result = ZMS_OnClientInfect(client, attacker, motherzombie, force);

        if(result == EHookAction.SkipCallReturnOverride)
        {
            return;
        }

        if (!InfectStarted)
        {
            InfectStarted = true;
        }

        var zmPlayer = _player.GetOrCreatePlayer(client);
        zmPlayer.IsZombie = true;

        var clientController = client.GetPlayerController();

        if(clientController == null)
        {
            _logger.LogError("The client controller is null!");
            return;
        }

        if (_leaderServices.IsClientLeader(clientController))
        {
            _glowServices.DisablePlayerGlow(clientController);
        }

        clientController.SwitchTeam(CStrikeTeam.TE);

        var clientZombieClass = zmPlayer.ZombieClass;

        if(motherzombie)
        {
            if(_cvarServices.CvarList["Cvar_InfectMotherZombieSpawn"]?.GetBool() ?? false)
                _ztele.TeleportToSpawn(client);

            // we need to get mother zombie class from here.
            var motherZombieClass = _playerClasses.GetMotherZombieClass();

            if(motherZombieClass != null)
                clientZombieClass = motherZombieClass;
        }

        // implement model changed and health.
        var pawn = clientController.GetPlayerPawn();

        if (pawn == null)
        {
            _logger.LogError("The client controller is null!");
            return;
        }

        pawn.AllowTakesDamage = true;
        
        _soundServices.EmitZombieSound(pawn, "zr.amb.scream");
        _modSharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} You have been infected! Go pass it on to as many other players as you can.", new RecipientFilter(client));

        _playerClasses.ApplyPlayerClassAttribute(pawn, clientZombieClass!);

        // forcing drop all weapon.
        var weapons = pawn.GetWeaponService()?.GetMyWeapons();

        if (weapons == null)
        {
            _logger.LogError("Client weapon is null");
            return;
        }

        // drop all weapon that has hammerid.
        foreach (var weapon in weapons)
        {
            var entity = _entityManager.FindEntityByHandle(weapon)?.As<IBaseWeapon>();

            if (entity == null)
                continue;

            if (!string.IsNullOrEmpty(entity.HammerId))
            {
                pawn.DropWeapon(entity);
            }
        }

        pawn.RemoveAllItems();
        pawn.GiveNamedItem(EconItemId.KnifeTe);

        if (attacker == null)
            return;

        var attackerPlayer = _player.GetOrCreatePlayer(attacker);
        attackerPlayer.TotalInfect += 1;

        _soundServices.ZombieMoan(pawn);
        CheckGameStatus();

        // Fire fake event here.
        var fakeEvent = _eventManager.CreateEvent("player_death", true);

        if (fakeEvent == null)
        {
            return;
        }

        try
        {
            fakeEvent.SetPlayer("userid", client.Slot);
            fakeEvent.SetPlayer("attacker", attacker.Slot);
            fakeEvent.SetString("weapon", "weapon_knife");

            fakeEvent.FireToClients();
        }
        catch (Exception ex)
        {
            // Ignore
            _logger.LogCritical("Failed to fire fake player_death event: {Exception}", ex);
        }
        finally
        {
            fakeEvent.Dispose();
        }
    }

    public void HumanizeClient(IGameClient client, bool force = false)
    {
        if (client == null)
        {
            return;
        }

        var result = ZMS_OnClientHumanize(client, force);

        if (result == EHookAction.SkipCallReturnOverride)
        {
            return;
        }

        var zmPlayer = _player.GetOrCreatePlayer(client);
        zmPlayer.IsZombie = false;

        var clientController = client.GetPlayerController();

        if(clientController == null)
        {
            _logger.LogError("The client controller is null!");
            return;
        }

        clientController.SwitchTeam(CStrikeTeam.CT);

        if(force)
            _modSharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} The merciful gods (known as admins) have resurrected your soul, find some cover!", new RecipientFilter(client));

        // implement model changed and health.
        var pawn = clientController.GetPlayerPawn();

        if (pawn == null)
        {
            _logger.LogError("The client controller is null!");
            return;
        }

        pawn.AllowTakesDamage = true;

        _playerClasses.ApplyPlayerClassAttribute(pawn, zmPlayer.HumanClass!);
    }

    public void OnRoundPreStart()
    {
        var allPlayers = _player.GetAllPlayers();

        foreach (var kvp in allPlayers)
        {
            var client = kvp.Key;
            var zmPlayer = kvp.Value;

            zmPlayer.IsZombie = false;
            zmPlayer.TotalDamage = 0;
            zmPlayer.TotalInfect = 0;

            var controller = client.GetPlayerController();
            if (controller == null)
            {
                continue;
            }

            if(controller.Team == CStrikeTeam.Spectator || controller.Team == CStrikeTeam.UnAssigned)
                continue;

            controller.SwitchTeam(CStrikeTeam.CT);
        }
    }

    public void OnRoundStart()
    {
        _modSharp.PrintChannelAll(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Current game mode is \x05Humans vs. Zombies\x01, the goal for zombies is to infect all humans by knifing them.");
    }

    public void OnRoundFreezeEnd()
    {
        if(_modSharp.GetGameRules().IsWarmupPeriod && (!_cvarServices.CvarList["Cvar_InfectWarmupEnabled"]?.GetBool() ?? false))
        {
            return;
        }

        // start countdown.
        InitialCountDown();
        //_modSharp.PrintChannelAll(HudPrintChannel.Chat, "Infect round freeze is called");
    }

    public void OnRoundEnd()
    {
        // gotta stop infection timer if still running.
        InfectStarted = false;

        if(infectTimer != Guid.Empty)
        {
            _modSharp.StopTimer(infectTimer);
            infectTimer = Guid.Empty;
        }

        if(countdownTimer != Guid.Empty)
        {
            _modSharp.StopTimer(countdownTimer);
            countdownTimer = Guid.Empty;
        }

        var allPlayers = _player.GetAllPlayers();

        foreach (var kvp in allPlayers)
        {
            var client = kvp.Key;
            var zmPlayer = kvp.Value;

            if (zmPlayer.MotherZombieStatus == MotherZombieStatus.Chosen)
                zmPlayer.MotherZombieStatus = MotherZombieStatus.Last;
        }

        // Top defender, which we take from 3 client where they're actually do something with it.
        var topDefender = allPlayers.OrderByDescending(p => p.Value.TotalDamage).Take(3).Where(p => p.Value.TotalDamage > 0).ToList();
        var topInfect = allPlayers.OrderByDescending(p => p.Value.TotalInfect).Take(3).Where(p => p.Value.TotalInfect > 0).ToList();

        // Print to all player.
        if(topDefender.Count > 0)
        {
            _modSharp.PrintToChatAll($" \x0C+++++++++++++++++ \x04[TOP DEFENDER] \x0C+++++++++++++++++");
            for(int i = 0; i < topDefender.Count; i++)
            {
                _modSharp.PrintToChatAll($" \x06{i+1}. {topDefender[i].Key.Name} - \x07{topDefender[i].Value.TotalDamage} DMG");

                // we give them reward based on next round.
                if(i < 2)
                {
                    // extra grenade.
                    allPlayers[topDefender[i].Key].AllowExtraGrenade = true;
                }

                allPlayers[topDefender[i].Key].MotherZombieImmune = true;
            }
        }

        if(topInfect.Count > 0)
        {
            // infector side
            _modSharp.PrintToChatAll($" \x10+++++++++++++++++ \x07[TOP INFECTOR] \x10+++++++++++++++++");
            for(int i = 0; i < topInfect.Count; i++)
            {
                _modSharp.PrintToChatAll($" \x09{i+1}. {topInfect[i].Key.Name} - \x07{topInfect[i].Value.TotalInfect} Infected");
            }
        }
    }

    public void CheckGameStatus()
    {
        // if CT count is 0, end the round.
        if (!InfectStarted)
        {
            return;
        }

        if (IsTestMode())
        {
            return;
        }

        var allClient = _clientManager.GetGameClients();

        int ctCount = 0;
        int tCount = 0;

        foreach (var client in allClient)
        {
            var controller = client.GetPlayerController();
            if (controller == null)
            {
                continue;
            }

            var isAlive = controller.GetPlayerPawn()?.IsAlive ?? false;

            if (controller.Team == CStrikeTeam.CT && isAlive)
            {
                ctCount++;
            }
            else if (controller.Team == CStrikeTeam.TE && isAlive)
            {
                tCount++;
            }
        }

        if (ctCount <= 0 && tCount > 0)
        {
            InfectStarted = false;
            _modSharp.GetGameRules().TerminateRound(8.0f, RoundEndReason.TerroristsWin);
            var zombieOverlay = _cvarServices.CvarList["Cvar_InfectZombieWinOverlay"]?.GetString() ?? "";
            var team = _entityManager.GetGlobalCStrikeTeam(CStrikeTeam.TE);
            team?.Score += 1;

            if(!string.IsNullOrEmpty(zombieOverlay))
            {
                foreach (var client in allClient)
                {
                    var controller = client.GetPlayerController();
                    if (controller == null)
                    {
                        continue;
                    }

                    var pawn = controller.GetPlayerPawn();

                    if(pawn == null)
                    {
                        continue;
                    }
                    
                    _particleManager.DispatchParticleEffect(zombieOverlay, ParticleAttachmentType.MainView, pawn, 0, true, new(client));
                };
            }
        }

        else if (tCount <= 0 && ctCount > 0)
        {
            InfectStarted = false;
            _modSharp.GetGameRules().TerminateRound(8.0f, RoundEndReason.CTsWin);
            var humanOverlay = _cvarServices.CvarList["Cvar_InfectHumanWinOverlay"]?.GetString() ?? "";
            var team = _entityManager.GetGlobalCStrikeTeam(CStrikeTeam.CT);
            team?.Score += 1;

            if(!string.IsNullOrEmpty(humanOverlay))
            {
                foreach (var client in allClient)
                {
                    var controller = client.GetPlayerController();
                    if (controller == null)
                    {
                        continue;
                    }

                    var pawn = controller.GetPlayerPawn();

                    if(pawn == null)
                    {
                        continue;
                    }
                    _particleManager.DispatchParticleEffect(humanOverlay, ParticleAttachmentType.MainView, pawn, 0, true, new(client));
                };
            }
        }
    }

    private void InitialCountDown()
    {
        var timerCount = _cvarServices.CvarList["Cvar_InfectCountdown"]?.GetFloat() ?? 15.0f;

        if(infectTimer != Guid.Empty)
        {
            _modSharp.StopTimer(infectTimer);
            infectTimer = Guid.Empty;
        }

        if(countdownTimer != Guid.Empty)
        {
            _modSharp.StopTimer(countdownTimer);
            countdownTimer = Guid.Empty;
        }

        infectTimer = _modSharp.PushTimer(new Func<TimerAction>(() =>
        {
            try
            {
                if (!IsInfectStarted())
                    InfectMotherZombie();
                    
                return TimerAction.Continue;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
                return TimerAction.Stop;
            }
        }), timerCount, GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);

        _modSharp.PrintChannelAll(HudPrintChannel.Hint, $"First infection start in {timerCount} seconds");

        countdownTimer = _modSharp.PushTimer(new Func<TimerAction>(() =>
        {
            try
            {
                timerCount--;
                _modSharp.PrintChannelAll(HudPrintChannel.Hint, $"First infection start in {timerCount} seconds");

                if (timerCount <= 0)
                    return TimerAction.Stop;

                if (IsInfectStarted())
                    return TimerAction.Stop;

                return TimerAction.Continue;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
                return TimerAction.Stop;
            }
        }), 1.0f, GameTimerFlags.Repeatable | GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
    }

    private void InfectMotherZombie()
    {
        if(IsTestMode())
        {
            _modSharp.PrintChannelAll(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Mother Zombie infection is skipped because test mode is enabled.");
            return;
        }

        // Get All Player with motherzombie status, and alive.
        var candidate = _player.GetAllPlayers().Where(p => p.Value.MotherZombieStatus == MotherZombieStatus.None
            && (p.Key.GetPlayerController()?.GetPlayerPawn()?.IsAlive ?? false) && p.Value.MotherZombieImmune == false);

        // we could just use all player and count them but this sometime unfair for player who has to fight for spectator
        var totalPlayer = _player.GetAllPlayers().Where(p => p.Key.GetPlayerController()?.IsAlive ?? false).Count();

        // 63 / 7 = 9 zombies
        var ratio = _cvarServices.CvarList["Cvar_InfectMotherZombieRatio"]?.GetFloat() ?? 7.0f;
        int requireZm = (int)Math.Round(totalPlayer / ratio);

        // if zombie is less than 0 then make one.
        if (requireZm <= 0)
            requireZm = _cvarServices.CvarList["Cvar_InfectMinimumZombie"]?.GetInt32() ?? 1;

        int made = 0;

        // this part is mother zombie reset, if candidate is less than zombie requirement.
        if (requireZm > candidate.Count())
        {
            // if any candidate left here.
            if (candidate.Any())
            {
                // we made them motherzombie right the way.
                foreach (var player in candidate)
                {
                    // we count how many mother zombie is made.
                    made++;

                    if(player.Key.GetPlayerController()?.Team == CStrikeTeam.Spectator || player.Key.GetPlayerController()?.Team == CStrikeTeam.UnAssigned)
                        continue;

                    InfectPlayer(player.Key, null, true, false);

                    // chosen for this round.
                    player.Value.MotherZombieStatus = MotherZombieStatus.Chosen;
                }
            }

            // at the end of the round chosen mother zombie will get reset to Last. so we reset it.
            foreach (var player in _player.GetAllPlayers().Where(p => p.Value.MotherZombieStatus == MotherZombieStatus.Last))
            {
                player.Value.MotherZombieStatus = MotherZombieStatus.None;
            }

            // getting candidate again.
            candidate = _player.GetAllPlayers().Where(p => p.Value.MotherZombieStatus == MotherZombieStatus.None
                && (p.Key.GetPlayerController()?.IsAlive ?? false) && p.Value.MotherZombieImmune == false);

            // tell them that we have reset cycle.
            _modSharp.PrintChannelAll(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Mother Zombie has been reset.");
        }

        // now in this case if mother zombie is enough from reset cycle, then we should just stop.
        if (requireZm - made <= 0)
            return;

        // random and shuffle them
        var random = new Random();
        var shuffledCandidates = candidate.OrderBy(x => random.Next()).ToList();
        var selectedMotherZombies = shuffledCandidates.Take(requireZm - made); // take from the started. and it should deducted with already made zombie count.

        // loop again.
        foreach (var player in selectedMotherZombies)
        {
            if(player.Key.GetPlayerController()?.Team == CStrikeTeam.Spectator || player.Key.GetPlayerController()?.Team == CStrikeTeam.UnAssigned)
                continue;
                
            InfectPlayer(player.Key, null, true, false);
            player.Value.MotherZombieStatus = MotherZombieStatus.Chosen;
        }

        foreach (var player in _player.GetAllPlayers().Where(p => p.Value.MotherZombieImmune || p.Value.AllowExtraGrenade))
        {
            if(player.Value.MotherZombieImmune)
            {
                _modSharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} You have mother zombie immunity from top defender!", new RecipientFilter(player.Key));
                player.Value.MotherZombieImmune = false;
            }

            player.Value.AllowExtraGrenade = false;
        }
    }

    public bool IsClientInfect(IGameClient client)
    {
        return _player.GetOrCreatePlayer(client).IsInfected();
    }

    public bool IsClientHuman(IGameClient client)
    {
        return _player.GetOrCreatePlayer(client).IsHuman();
    }

    public EHookAction? ZMS_OnClientInfect(IGameClient client, IGameClient? attacker = null, bool motherzombie = false, bool force = false)
    {
        return OnClientInfect?.Invoke(client, attacker, motherzombie, force);
    }

    public EHookAction? ZMS_OnClientHumanize(IGameClient client, bool force = false)
    {
        return OnClientHumanize?.Invoke(client, force);
    }

    public bool IsInfectStarted()
    {
        return InfectStarted;
    }

    public void SetInfectStarted(bool result)
    {
        InfectStarted = result;
    }

    public void SetTestMode(bool result)
    {
        _testMode = result;
    }

    public bool IsTestMode()
    {
        return _testMode;
    }
}