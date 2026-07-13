using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class SoundServices : ISoundServices
{
    private readonly ISharedSystem _sharedSystem;
    private readonly IModSharp _modsharp;
    private readonly ILogger<SoundServices> _logger;
    private readonly IPlayerManager _playerManager;
    
    public SoundServices(ISharedSystem sharedSystem, IModSharp modSharp, IPlayerManager playerManager)
    {
        _sharedSystem = sharedSystem;
        _modsharp = _sharedSystem.GetModSharp();
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<SoundServices>();
        _sharedSystem.GetSoundManager();
        _playerManager = playerManager;
    }

    public void ZombieMoan(IPlayerPawn player)
    {
        var gameClient = player.GetController()?.GetGameClient();

        if(gameClient == null)
        {
            _logger.LogError("Client is null!");
            return;
        }

        var client = _playerManager.GetOrCreatePlayer(gameClient);

        if(_modsharp.IsValidTimer(client.MoanTimer))
            _modsharp.StopTimer(client.MoanTimer);

        client.MoanTimer = _modsharp.PushTimer(new Func<TimerAction>(() =>
        {
            if(!player.IsAlive || client.IsHuman() || !client.IsInfected())
                return TimerAction.Stop;

            EmitZombieSound(player, "zr.amb.zombie_voice_idle");
            return TimerAction.Continue;
        }), 15.0f, GameTimerFlags.Repeatable | GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
    }

    public void ZombieHurtSound(IPlayerPawn player)
    {
        var gameClient = player.GetController()?.GetGameClient();

        if(gameClient == null)
        {
            _logger.LogError("Client is null!");
            return;
        }

        var client = _playerManager.GetOrCreatePlayer(gameClient);

        if(client.ZombiePainTime < _modsharp.EngineTime())
        {
            EmitZombieSound(player, "zr.amb.zombie_pain");
            client.ZombiePainTime = (float)_modsharp.EngineTime() + 15.0f;
        }
    }

    public void EmitZombieSound(IBaseEntity source, string sound)
    {
        var allPlayer = _playerManager.GetAllPlayers().Where(p => p.Value.SoundEnabled && p.Value.SoundVolume > 0);

        foreach(var player in allPlayer)
        {
            var volume = player.Value.SoundVolume / 100f;
            source.EmitSound(sound, volume, new RecipientFilter(player.Key));
        }
    }
}
