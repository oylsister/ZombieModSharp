using Microsoft.Extensions.Logging;
using Sharp.Extensions.GameEventManager;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Shared;

namespace ZombieModSharp.Core.Modules;

public class LeaderServices : ILeaderServices
{
    private readonly List<IPlayerController> _leaders = new();
    private ISharedSystem? _sharedSystem;
    private ILogger<LeaderServices> _logger;
    private readonly IGlowServices _glowMethod;
    private readonly IGameEventManager _gameEventManager;
    private readonly IPlayerManager _playerManager;
    private readonly IModSharp _modsharp;

    //Vote logics for leader side
    private readonly Dictionary<string, HashSet<string>> _voteMap = new();
    private readonly object _voteLock = new();
    public event Action<IPlayerController, bool>? OnLeaderStatusChanged;

    public LeaderServices(ISharedSystem sharedSystem, ILogger<LeaderServices> logger, IGlowServices glowMethod, IGameEventManager gameEventManager, IPlayerManager playerManager)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
        _glowMethod = glowMethod;
        _gameEventManager = gameEventManager;
        _playerManager = playerManager;
        _modsharp = _sharedSystem.GetModSharp();
    }

    public void VoteLeader(IGameClient voter, IGameClient target)
    {
        if (target == null || !target.IsValid || !target.IsConnected)
        {
            _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Invalid voter or target for leader vote.", new RecipientFilter(voter));
            return;
        }

        if (_leaders.Count >= 2)
        {
            _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Leader slots are full (max 2). Cannot assign {target.Name}.", new RecipientFilter(voter));
            return;
        }

        var player = _playerManager.GetOrCreatePlayer(target);
        var voterPlayer = _playerManager.GetOrCreatePlayer(voter);

        if(player.IsInfected())
        {
            _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} You cannot vote for a zombie.", new RecipientFilter(voter));
            return;
        }

        if(voterPlayer.IsInfected())
        {
            _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} You cannot vote as a zombie.", new RecipientFilter(voter));
            return;
        }

        if (IsClientLeader(target.GetPlayerController()))
        {
            _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Target player is already a leader.", new RecipientFilter(voter));
            return;
        }

        if(voterPlayer.LeaderVotedTarget == target)
        {
            _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} You have already voted {target.Name} for a leader.", new RecipientFilter(voter));
            return;
        }

        player.LeaderVoteCount += 1;
        voterPlayer.LeaderVotedTarget = target;

        // if vote count reaches threshold, assign leader
        var allPlayer = _playerManager.GetAllPlayers();
        int requiredVotes = allPlayer.Count / 8;

        if(requiredVotes == 0)
            requiredVotes = 1;

        _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} {voter.Name} voted {target.Name} for leader. Current votes: {player.LeaderVoteCount} / {requiredVotes}");

        if (player.LeaderVoteCount >= requiredVotes)
        {
            var targetController = target.GetPlayerController();

            if(targetController == null || !targetController.IsValid())
            {
                _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} Target player controller is invalid.", new RecipientFilter(voter));
                return;
            }

            if (AssignLeader(targetController))
            {
                _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} {target.Name} has been assigned as a leader!");
                player.LeaderVoteCount = 0; // reset vote count after becoming leader
                allPlayer.Where(p => p.Value.LeaderVotedTarget == target).ToList().ForEach(p => p.Value.LeaderVotedTarget = null); // reset votes for this target
            }
        }
    }

    public bool AssignLeader(IPlayerController controller)
    {
        if (!controller.IsValid())
            return false;

        if (_leaders.Count >= 2)
        {
            _modsharp.PrintToChatAll($"{ZombieModSharp.Prefix} Leader slots are full (max 2). Cannot assign {controller.PlayerName}.");
            return false;
        }

        if (!_leaders.Contains(controller))
        {
            _leaders.Add(controller);
            OnLeaderStatusChanged?.Invoke(controller, true);
            return true;
        }
        return false;
    }

    
    public bool RemoveLeader(IPlayerController controller)
    {
        if (controller == null || !controller.IsValid())
            return false;

        if (!_leaders.Remove(controller))
            return false;

        OnLeaderStatusChanged?.Invoke(controller, false);
        return true;
    }

    
    public void ClearAllLeaders()
    {
        foreach (var controller in _leaders.ToList())
            OnLeaderStatusChanged?.Invoke(controller, false);

        _leaders.Clear();
    }

    
    public bool IsClientLeader(IPlayerController? controller)
    {
        if (controller == null || !controller.IsValid())
            return false;

        return _leaders.Contains(controller);
    }

    
    public IEnumerable<IPlayerController> GetAllLeaders()
    {
        return _leaders.ToList();
    }

    
    public void ReloadLeaderList(ISharedSystem sharedSystem)
    {
        var refreshed = new List<IPlayerController>();

        foreach (var controller in sharedSystem.GetEntityManager().FindPlayerControllers())
        {
            if (controller != null && controller.IsValid() && _leaders.Contains(controller))
            {
                refreshed.Add(controller);
            }
        }

        _leaders.Clear();
        _leaders.AddRange(refreshed);
    }

    private IPlayerController? FindTargetPlayer(StringCommand command, IGameClient self)
    {
        if (command.ArgCount == 0)
            return self.GetPlayerController();

        var nameArg = command.GetArg(1).Trim();
        var players = _sharedSystem!.GetEntityManager().FindPlayerControllers();

        var exact = players.FirstOrDefault(p =>
            p.IsValid() &&
            p.IsConnected() &&
            !string.IsNullOrEmpty(p.GetGameClient()?.Name) &&
            p.GetGameClient()!.Name.Equals(nameArg, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
            return exact;

        var partial = players.FirstOrDefault(p =>
            p.IsValid() &&
            p.IsConnected() &&
            !string.IsNullOrEmpty(p.GetGameClient()?.Name) &&
            p.GetGameClient()!.Name.Contains(nameArg, StringComparison.OrdinalIgnoreCase));

        return partial;
    }

    public void UpdateClientClanTags()
    {
        try
        {
            if (_gameEventManager.CreateEvent("nextlevel_changed", false) is { } evt)
            {
                evt.FireToClients();
                evt.Dispose();
            }
        }
        catch (Exception)
        {
            // 忽略錯誤，避免伺服器崩潰
        }
    }
    
}
