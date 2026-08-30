using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class RespawnServices : IRespawnServices
{
    private readonly ISharedSystem _sharedSystem;
    private readonly IModSharp _modsharp;
    private readonly ICvarServices _cvarServices;
    private readonly IEntityManager _entityManager;

    private static bool RespawnEnabled = true;
    private IBaseEntity? _respawnToggle;

    public RespawnServices(ISharedSystem sharedSystem, ICvarServices cvarServices)
    {
        _sharedSystem = sharedSystem;
        _modsharp = _sharedSystem.GetModSharp();
        _cvarServices = cvarServices;
        _entityManager = _sharedSystem.GetEntityManager();
    }

    public void InitRespawn(IPlayerController? client)
    {
        if (client == null) return;

        if (!RespawnEnabled) return;

        var delay = _cvarServices.CvarList["Cvar_RespawnDelay"]?.GetFloat() ?? 5.0f;

        _modsharp.PushTimer(() => { RespawnClient(client); }, delay,
            GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
    }

    public void RespawnClient(IPlayerController client)
    {
        if (!RespawnEnabled) return;

        var playerPawn = client.GetPlayerPawn();

        if (playerPawn == null) return;

        if (playerPawn.IsAlive) return;

        if (playerPawn.Team == CStrikeTeam.Spectator || playerPawn.Team == CStrikeTeam.UnAssigned ||
            client.Team == CStrikeTeam.Spectator || client.Team == CStrikeTeam.UnAssigned)
            return;

        client.Respawn();
    }

    public static bool IsRespawnEnabled()
    {
        return RespawnEnabled;
    }

    public static void SetRespawnEnable(bool set = true)
    {
        RespawnEnabled = set;
    }

    public void SetupRespawnToggler()
    {
        var kv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "targetname", "zr_toggle_respawn" }
        };

        _respawnToggle = _entityManager.SpawnEntitySync<IBaseEntity>("logic_relay", kv);
    }

    public IBaseEntity? GetRespawnToggler()
    {
        return _respawnToggle;
    }
}