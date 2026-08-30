using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZombieModSharp.Abstractions;

public interface IGlowServices
{
    public enum GlowVisibleMode
    {
        OnlySelf, // Only the target player can see the Glow
        SameTeam, // Same team players can see the Glow
        ExceptTarget // Except the target player, all others can see the Glow
    }

    public bool CreateGlow(IGameClient target, IPlayerPawn pawn, Color32 color, int maxDistance,
        GlowVisibleMode mode);

    public void DisablePlayerGlow(IPlayerController controller);

    public void CleanupAll();

    public string? GetModelNameSafe(IPlayerPawn pawn);
}