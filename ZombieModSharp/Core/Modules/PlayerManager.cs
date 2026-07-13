using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Abstractions.Entities;

namespace ZombieModSharp.Core.Modules;

public class PlayerManager : IPlayerManager
{
    private Dictionary<IGameClient, Player> _players { get; set; } = new();

    public PlayerManager()
    {
        _players = new Dictionary<IGameClient, Player>();
    }

    public Player GetOrCreatePlayer(IGameClient? client)
    {
        if(client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }
        
        if (_players.ContainsKey(client))
        {
            return _players[client];
        }

        var player = new Player();
        _players.Add(client, player);
        return player;
    }

    public Dictionary<IGameClient, Player> GetAllPlayers()
    {
        return _players;
    }

    public void RemovePlayer(IGameClient client)
    {
        _players.Remove(client);
    }
}

// Enhanced ZMPlayer class with all the merged functionality
public class Player
{
    public Player()
    {
        IsZombie = false;
        MotherZombieStatus = MotherZombieStatus.None;

    }

    // Core zombie state
    public bool IsZombie { get; set; } = false;
    public MotherZombieStatus MotherZombieStatus { get; set; } = MotherZombieStatus.None;
    
    // Spawn information
    public Vector? SpawnPoint { get; set; } = null;
    public Vector? SpawnRotation { get; set; } = null;

    // player classes
    public ClassAttribute? HumanClass { get; set; }
    public ClassAttribute? ZombieClass { get; set; }
    public ClassAttribute? ActiveClass { get; set; }

    // Convenience methods
    public bool IsHuman() => !IsZombie;
    public bool IsInfected() => IsZombie;

    // sound section.
    public bool SoundEnabled { get; set; }
    public int SoundVolume { get; set; } = 100;
    public Guid MoanTimer { get; set; }
    public float ZombiePainTime { get; set; }

    // weapon purchase count
    public Dictionary<string, int> PurchaseHistory { get; set; } = [];

    // Top defender and Top infection.
    public int TotalDamage { get; set; }
    public int TotalInfect { get; set; }
    public bool AllowExtraGrenade { get; set; } = false;
    public bool MotherZombieImmune { get; set; } = false;

    public Guid RegenerationTimer { get; set; } = Guid.Empty;
    public int LeaderVoteCount { get; set; } = 0;
    public IGameClient? LeaderVotedTarget { get; set; } = null;
    public bool InfiniteAmmo { get; set; } = false;
}
