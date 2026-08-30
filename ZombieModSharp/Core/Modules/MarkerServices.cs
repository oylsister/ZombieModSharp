using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class MarkerServices : IMarkerServices
{
    private readonly List<IBaseEntity> _markers = new();
    private int _effectIndex = 0;

    private readonly string[] _effects = new[]
    {
        "particles/leader_a_1.vpcf",
        "particles/leader_b_1.vpcf",
        "particles/leader_c_1.vpcf",
        "particles/leader_d_1.vpcf"
    };

    // 4 Markers at most
    private const int MaxMarkers = 4;

    private readonly ISharedSystem _sharedSystem;
    private readonly ITransmitManager _transmitManager;
    private readonly IEntityManager _entityManager;

    public MarkerServices(ISharedSystem sharedSystem)
    {
        _sharedSystem = sharedSystem;
        _transmitManager = _sharedSystem.GetTransmitManager();
        _entityManager = _sharedSystem.GetEntityManager();
    }

    public bool CreateMarker(IGameClient client, Vector position)
    {
        var effectName = _effects[_effectIndex];
        _effectIndex = (_effectIndex + 1) % _effects.Length;

        var kv = new Dictionary<string, KeyValuesVariantValueItem>
        {
            { "origin", $"{position.X} {position.Y} {position.Z}" },
            { "effect_name", effectName },
            { "start_active", 1 },
            { "targetname", $"marker_{Guid.NewGuid()}" }
        };

        if (_entityManager.SpawnEntitySync<IBaseEntity>("info_particle_system", kv) is not { } particle)
            return false;

        _transmitManager.AddEntityHooks(particle, true);

        // Delete oldest marker if exceeding limit

        if (_markers.Count >= MaxMarkers)
        {
            var oldest = _markers[0];
            if (oldest != null && oldest.IsValidEntity)
            {
                oldest.AcceptInput("Stop");
                oldest.AcceptInput("Kill");
            }

            _markers.RemoveAt(0);
        }

        _markers.Add(particle);


        return true;
    }

    /// <summary>
    /// Delete the last created marker
    /// </summary>
    public void DisableLastMarker()
    {
        if (_markers.Count > 0)
        {
            var marker = _markers[^1];
            if (marker != null && marker.IsValidEntity)
            {
                marker.AcceptInput("Stop");
                marker.AcceptInput("Kill");
            }

            _markers.RemoveAt(_markers.Count - 1);
        }
    }

    /// <summary>
    /// cleanup all markers
    /// </summary>
    public void CleanupAll()
    {
        foreach (var marker in _markers)
            if (marker != null && marker.IsValidEntity)
            {
                marker.AcceptInput("Stop");
                marker.AcceptInput("Kill");
            }

        _markers.Clear();

        _effectIndex = 0;
    }
}