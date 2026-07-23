namespace ZombieModSharp.Abstractions;

public interface IMapSettings
{
    void LoadConfig(string path);
    void SetCurrentMap(string mapName);
    string GetCurrentLevel();
    bool AreBoostCommandsDisabled();
    bool SetCurrentMapLevel(string level);
}
