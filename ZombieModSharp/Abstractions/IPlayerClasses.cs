using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Abstractions;

public interface IPlayerClasses
{
    public void LoadConfig(string path);
    public void ApplyPlayerClassAttribute(IPlayerPawn? playerPawn, ClassAttribute classAttribute);
    public ClassAttribute? GetClassByName(string classname);
    public ClassAttribute? GetMotherZombieClass();
    void GetMenuManager(IModSharpModuleInterface<IMenuManager>? menuManager);
    void PlayerClassMenu(IGameClient client);
}