using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;


namespace ZombieModSharp.Core.Modules;

public class GlowServices : IGlowServices
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<GlowServices> _logger;

    // 改成用 ControllerIndex 當 key
    private readonly Dictionary<int, List<IBaseModelEntity>> _glowingEntities = new();

    public GlowServices(ISharedSystem sharedSystem, ILogger<GlowServices> logger)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
    }

    //public enum GlowVisibleMode
    //{
    //    OnlySelf, // Only the target player can see the Glow
    //    SameTeam, // Same team players can see the Glow
    //    ExceptTarget // Except the target player, all others can see the Glow
    //}

    public bool CreateGlow(IGameClient target, IPlayerPawn pawn, Color32 color, int maxDistance,
        IGlowServices.GlowVisibleMode mode)
    {
        var controllerIndex = target.ControllerIndex;

        if (_glowingEntities.ContainsKey(controllerIndex))
        {
            return false;
        }

        var model = GetModelNameSafe(pawn);
        if (string.IsNullOrEmpty(model))
            return false;

        var entityMgr = _sharedSystem.GetEntityManager();
        var transmitMgr = _sharedSystem.GetTransmitManager();

        var relayKv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "model", model },
            { "spawnflags", 256 },
            { "rendermode", (int)RenderMode.None },
            { "disablereceiveshadows", true },
            { "disableshadows", true },
            { "renderamt", 0 }
        };

        if (entityMgr.SpawnEntitySync<IBaseModelEntity>("prop_dynamic", relayKv) is not { } relay)
            return false;

        var glowKv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "model", model },
            { "spawnflags", 256 },
            { "disablereceiveshadows", true },
            { "disableshadows", true },
            { "glowcolor", $"{color.R} {color.G} {color.B} {color.A}" },
            { "glowrangemin", 0 },
            { "glowrange", maxDistance },
            { "glowteam", -1 },
            { "glowstate", 3 },
            { "renderamt", 0 },
            { "mindist", 0 },
            { "maxdist", 25565 }
        };

        if (entityMgr.SpawnEntitySync<IBaseModelEntity>("prop_dynamic", glowKv) is not { } glow)
        {
            relay.AcceptInput("Kill");
            return false;
        }

        relay.AcceptInput("FollowEntity", pawn, null, "!activator");
        glow.AcceptInput("FollowEntity", relay, null, "!activator");

        transmitMgr.AddEntityHooks(relay, false);
        transmitMgr.AddEntityHooks(glow, false);

        transmitMgr.SetEntityOwner(relay.Index, controllerIndex);
        transmitMgr.SetEntityOwner(glow.Index, controllerIndex);

        transmitMgr.SetEntityState(relay.Index, controllerIndex, false, -1);
        transmitMgr.SetEntityState(glow.Index, controllerIndex, false, -1);

        var players = _sharedSystem.GetEntityManager().FindPlayerControllers();

        switch (mode)
        {
            case IGlowServices.GlowVisibleMode.OnlySelf:
                transmitMgr.SetEntityState(relay.Index, controllerIndex, true, -1);
                transmitMgr.SetEntityState(glow.Index, controllerIndex, true, -1);
                break;

            case IGlowServices.GlowVisibleMode.SameTeam:
                var team = pawn.GetControllerAuto()?.Team;
                if (team.HasValue)
                {
                    foreach (var cCtrl in players)
                    {
                        if (cCtrl == null || !cCtrl.IsValid()) continue;
                        var gc = cCtrl.GetGameClient();
                        if (gc == null) continue;

                        if (cCtrl.Team == team.Value && gc.ControllerIndex != controllerIndex)
                        {
                            transmitMgr.SetEntityState(relay.Index, gc.ControllerIndex, true, -1);
                            transmitMgr.SetEntityState(glow.Index, gc.ControllerIndex, true, -1);
                        }
                    }
                }

                break;

            case IGlowServices.GlowVisibleMode.ExceptTarget:
                foreach (var cCtrl in players)
                {
                    if (cCtrl == null || !cCtrl.IsValid()) continue;
                    var gc = cCtrl.GetGameClient();
                    if (gc == null) continue;
                    if (gc.ControllerIndex == controllerIndex) continue;

                    transmitMgr.SetEntityState(relay.Index, gc.ControllerIndex, true, -1);
                    transmitMgr.SetEntityState(glow.Index, gc.ControllerIndex, true, -1);
                }

                break;
        }

        _glowingEntities[controllerIndex] = new List<IBaseModelEntity> { glow, relay };
        return true;
    }

    public void DisablePlayerGlow(IPlayerController controller)
    {
        if (controller == null || !controller.IsValid())
            return;

        var controllerIndex = controller.Index;

        if (!_glowingEntities.ContainsKey(controllerIndex))
        {
            return;
        }

        if (_glowingEntities.TryGetValue(controllerIndex, out var entities))
        {
            foreach (var ent in entities)
            {
                if (ent.IsValidEntity)
                    ent.AcceptInput("Kill");
            }

            _glowingEntities.Remove(controllerIndex);
            
        }
    }

    public void CleanupAll()
    {
        foreach (var kv in _glowingEntities)
        {
            foreach (var ent in kv.Value)
            {
                if (ent != null && ent.IsValidEntity)
                    ent.AcceptInput("Kill");
            }
        }

        _glowingEntities.Clear();
    }

    public string? GetModelNameSafe(IPlayerPawn pawn)
    {
        var body = pawn.GetBodyComponent();
        var sceneNode = body?.GetSceneNode();
        var skeleton = sceneNode?.AsSkeletonInstance;
        var modelState = skeleton?.GetModelState();
        return modelState?.ModelName;
    }
}


