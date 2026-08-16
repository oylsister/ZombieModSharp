using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class Knockback : IKnockback
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<Knockback> _logger;
    private readonly IPlayerManager _player;
    private readonly IWeapons _weapons;
    private readonly IHitGroup _hitgroup;
    private readonly IModSharp _modsharp;

    private float KnockbackScale = 1.0f;
    private float DynamicKnockbackScale = 1.0f;
    private float KnockbackJumpScale = 1.0f;

    public Knockback(ISharedSystem sharedSystem, ILogger<Knockback> logger, IPlayerManager player, IWeapons weapons, IHitGroup hitGroup)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
        _player = player;
        _weapons = weapons;
        _hitgroup = hitGroup;
        _modsharp = _sharedSystem.GetModSharp();
    }

    public void KnockbackClient(IGameClient client, IGameClient attacker, string weapon, float damage, int hitGroup)
    {
        if (weapon.Contains("hegrenade"))
            return;

        var playerPawn = client.GetPlayerController()?.GetPlayerPawn();

        if (playerPawn == null)
            return;

        if (weapon.Contains("inferno"))
        {
            playerPawn.VelocityModifier = 40.0f / 100.0f;
            return;
        }

        if (client == null || attacker == null)
            return;

        var clientPlayer = _player.GetOrCreatePlayer(client);
        var attackerPlayer = _player.GetOrCreatePlayer(attacker);
            
        // knockback is for zombie only.
        if (!attackerPlayer.IsHuman() || !clientPlayer.IsInfected())
            return;

        var attackerPawn = attacker.GetPlayerController()?.GetPlayerPawn();

        if (attackerPawn == null)
        {
            _logger?.LogError("attacker pawn is null!");
            return;
        }

        // for more precise weapon, we need to get item defenition name.
        var weaponentity = attackerPawn.GetActiveWeapon();

        if(weaponentity != null && weaponentity.IsValidEntity)
        {
            if(weaponentity.Slot == GearSlot.Knife)
                weapon = "knife";

            else
            {
                weapon = weaponentity.As<IBaseWeapon>().GetItemDefinitionName();
                if (weapon.StartsWith("weapon_"))
                    weapon = weapon.Substring(7); // Remove the "weapon_" prefix
            }
        }

        var attackerEye = attackerPawn.GetEyeAngles();
        var foward = attackerEye.AnglesToVectorForward();

        var classKnockback = _player.GetOrCreatePlayer(client).ActiveClass?.Knockback ?? 3.0f;
        var weaponknockback = _weapons.GetWeaponKnockback(weapon);
        var hitgroupsKnockback = _hitgroup.GetHitgroupKnockback(hitGroup);

        // _modsharp.PrintToChatAll($"KB data: {weaponknockback:F2} | {hitgroupsKnockback:F2} | {classKnockback:F2} | {KnockbackScale:F2} | {DynamicKnockbackScale:F2} | {KnockbackJumpScale:F2}");

        var pushVelocity = foward * damage * classKnockback * weaponknockback * hitgroupsKnockback * KnockbackScale * DynamicKnockbackScale;

        if(playerPawn.GroundEntity == null)
        {
            pushVelocity *= KnockbackJumpScale;
        }

        // _modsharp.PrintToChatAll($"KB data: {weapon} | {pushVelocity.Length():F2}");

        playerPawn.ApplyAbsVelocityImpulse(pushVelocity);
    }

    public void SetKnockbackScale(float scale)
    {
        if(scale < 0)
        {
            KnockbackScale = 1.0f;
            return;
        }

        KnockbackScale = scale;
    }

    public void SetDynamicKnockbackScale(float scale)
    {
        if(scale < 0)
        {
            DynamicKnockbackScale = 1.0f;
            return;
        }

        DynamicKnockbackScale = scale;
    }

    public void SetJumpKnockbackScale(float scale)
    {
        if(scale < 0)
        {
            KnockbackJumpScale = 1.0f;
            return;
        }

        KnockbackJumpScale = scale;
    }
}