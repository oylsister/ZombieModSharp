using Sharp.Shared;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using System;
using System.Collections.Generic;
using System.Text;
using ZombieModSharp.Shared;

namespace ZombieModSharp.Abstractions;

public interface ILeaderServices : ILeaderShared
{
    public bool AssignLeader(IPlayerController controller);
    public bool RemoveLeader(IPlayerController controller);

    public IEnumerable<IPlayerController> GetAllLeaders();

    public void ReloadLeaderList(ISharedSystem sharedSystem);

    public void UpdateClientClanTags();

    public void VoteLeader(IGameClient voter, IGameClient target);
}