using Sharp.Shared.Enums;
using Sharp.Shared.Objects;

namespace ZombieModSharp.Shared;

public delegate EHookAction DelegateInfectPlayer(IGameClient client, IGameClient? attacker = null, bool motherzombie = false, bool force = false);
public delegate EHookAction DelegateHumanizeClient(IGameClient client, bool force = false);

public interface IInfectShared
{
    const string Identity = nameof(IInfectShared);

    event DelegateInfectPlayer? OnClientInfect;
    event DelegateHumanizeClient? OnClientHumanize;
    void InfectPlayer(IGameClient client, IGameClient? attacker = null, bool motherzombie = false, bool force = false);
    void HumanizeClient(IGameClient client, bool force = false);
    bool IsClientInfect(IGameClient client);
    bool IsClientHuman(IGameClient client);
}
