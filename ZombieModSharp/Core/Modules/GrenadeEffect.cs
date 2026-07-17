using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class GrenadeEffect : IGrenadeEffect
{
    private readonly IPlayerManager _playerManager;
    private readonly ISharedSystem _sharedSystem;
    private readonly IEntityManager _entityManager;
    private readonly IModSharp _modsharp;
    private readonly ILogger<GrenadeEffect> _logger;

    public GrenadeEffect(IPlayerManager playerManager, ISharedSystem sharedSystem)
    {
        _playerManager = playerManager;
        _sharedSystem = sharedSystem;
        _entityManager = _sharedSystem.GetEntityManager();
        _modsharp = _sharedSystem.GetModSharp();
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<GrenadeEffect>();
    }

    public bool IgnitePawn(IPlayerPawn? playerPawn, int damage = 1, float duration = 1)
    {
        if(playerPawn == null)
            return false;

        var particle = _entityManager.FindEntityByHandle(playerPawn.EffectEntityHandle)?.As<IBaseParticle>();

        if(particle != null)
        {
            particle.DissolveStartTime = _modsharp.GetGlobals().CurTime + duration;
            return true;
        }

        var kv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "effect_name", "particles/cs2fixes/napalm_fire.vpcf" },
            { "start_active", 1 },
        };

        particle = _entityManager.SpawnEntitySync<IBaseParticle>("info_particle_system", kv);
        
        try 
        {
            if(particle == null)
            {
                //_modsharp.PrintToChatAll("Is fucking null");
                return false;
            }

            particle.GetControlPointEntities()[0] = playerPawn.Handle;
            particle.DissolveStartTime = _modsharp.GetGlobals().CurTime + duration;
            particle.Teleport(playerPawn.GetAbsOrigin());
            particle.AcceptInput("SetParent", playerPawn, null, "!activator");

            // _modsharp.PrintToChatAll("It does work for some reason");

            playerPawn.EffectEntityHandle = particle.Handle;

            _modsharp.PushTimer(new Func<TimerAction>(() => 
            {
                if(playerPawn == null || !playerPawn.IsValid())
                    return TimerAction.Stop;

                if(particle == null || !particle.IsValidEntity)
                    return TimerAction.Stop;

                if(!particle.Classname.StartsWith("info_part"))
                {
                    _logger.LogError("Unexpceted entity is found in particle.");
                    return TimerAction.Stop;
                }

                if(particle.DissolveStartTime <= _modsharp.GetGlobals().CurTime || !playerPawn.IsAlive)
                {
                    particle.AcceptInput("Stop");
                    particle.AddIOEvent(0.1f, "Kill");
                    return TimerAction.Stop;
                }

                ApplyDamage(playerPawn, damage);
                playerPawn.VelocityModifier = 40.0f / 100.0f;
                return TimerAction.Continue;
            }), 0.5, GameTimerFlags.Repeatable|GameTimerFlags.StopOnRoundEnd|GameTimerFlags.StopOnMapEnd);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {ex}", ex.Message);
            return false;
        }

        return true;
    }

    private void ApplyDamage(IPlayerPawn playerPawn, int damage)
    {
        var activeWeapon = playerPawn.GetActiveWeapon();

        var info = new TakeDamageInfo (playerPawn)
        {
            Inflictor = activeWeapon!.Handle,
            Ability = activeWeapon.Handle,
            Damage = damage,
            DamageType = DamageFlagBits.Fall,
            TakeDamageFlags = TakeDamageFlags.IgnoreArmor
        };

        playerPawn.DispatchTraceAttack(info, true);
    }

    public void ApplyFreeze(IBaseEntity grenade, float distanceLimit, float duration)
    {
        if(grenade == null || !grenade.IsValid())
            return;
            
        var grenadePos = grenade.GetAbsOrigin();

        EmitFreezeEffect(grenadePos);

        foreach(var client in _playerManager.GetAllPlayers().Where(p => p.Value.IsInfected()))
        {
            var pawn = client.Key.GetPlayerController()?.GetPlayerPawn();

            if(pawn == null || !pawn.IsAlive)
                continue;

            var pos = pawn.GetAbsOrigin();

            var distance = GetDistance(grenadePos, pos);

            if(distance <= distanceLimit)
            {
                pawn.SetMoveType(MoveType.None);
                pawn.Teleport();

                _modsharp.PushTimer(() =>
                {
                    pawn.SetMoveType(MoveType.Walk);
                }, duration, GameTimerFlags.StopOnMapEnd);
            }
        }

        // we can't get flashed.
        grenade.Kill();
    }

    public void ApplyLightGrenade(IBaseEntity grenade, float duration)
    {
        if(grenade == null || !grenade.IsValid())
            return;

        var pos = grenade.GetAbsOrigin();
        var kv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "inner_cone", 1 },
            { "cone", 80 },
            { "brightness", 1 },
            { "spotlight_radius", 150.0f },
            { "pitch", 90 },
            { "style", 1 },
            { "_light", "255 255 255 255" },
            { "distance", 1000.0f }
        };

        var entity = _entityManager.SpawnEntitySync("light_dynamic", kv);
        entity?.Teleport(pos);
        entity?.AddIOEvent(duration, "Kill");

        grenade.Kill();
    }

    private void EmitFreezeEffect(Vector position)
    {
        var kv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "effect_name", "particles/oylsister/freeze_beacon.vpcf" },
            { "tint_cp", 1 },
            { "start_active", true }
        };

        var entity = _entityManager.SpawnEntitySync("info_particle_system", kv);
        entity?.Teleport(position);
        entity?.AddIOEvent(1.0f, "Kill");
    }

    private float GetDistance(Vector a, Vector b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}