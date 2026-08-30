using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Abstractions;

public interface IPlayerManager
{
    public Player GetOrCreatePlayer(IGameClient? client);
    public Dictionary<IGameClient, Player> GetAllPlayers();
    public void RemovePlayer(IGameClient client);
}