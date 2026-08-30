using Sharp.Modules.AdminManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Objects;

namespace ZombieModSharp.Abstractions;

public interface ICommand
{
    public void PostInit();
    public void RegisterAdminCommand();
    public void ReplyToCommand(IGameClient client, string text);
    void GetAdminManager(IModSharpModuleInterface<IAdminManager>? adminManager);
}