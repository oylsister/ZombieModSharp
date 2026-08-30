using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace ZombieModSharp;

internal sealed class InterfaceBridge
{
    public string SharpPath { get; }
    public string RootPath { get; }
    public string DllPath { get; }
    public string DataPath { get; }
    public string ConfigPath { get; }
    public Version GameVersion { get; }
    public Version Version { get; }
    public FileVersionInfo FileVersion { get; }
    public DateTime FileTime { get; }
    public IEventManager EventManager { get; }
    public IEntityManager EntityManager { get; }
    public IClientManager ClientManager { get; }
    public IConVarManager ConVarManager { get; }
    public ITransmitManager TransmitManager { get; }
    public IHookManager HookManager { get; }
    public IFileManager FileManager { get; }
    public ISchemaManager SchemaManager { get; }
    public IEconItemManager EconItemManager { get; }
    public ISoundManager SoundManager { get; }
    public IModSharp ModSharp { get; }
    public IPhysicsQueryManager PhysicsQuery { get; }
    public IGameData GameData { get; }
    public ILoggerFactory LoggerFactory { get; }

    private readonly ILogger<InterfaceBridge> _logger;

    public InterfaceBridge(string dllPath, string sharpPath, Version version, ISharedSystem sharedSystem)
    {
        SharpPath = sharpPath;
        DllPath = dllPath;
        RootPath = Path.GetFullPath(Path.Combine(sharpPath, ".."));
        DataPath = Path.GetFullPath(Path.Combine(sharpPath, "data"));
        ConfigPath = Path.GetFullPath(Path.Combine(sharpPath, "configs"));
        GameVersion = GetGameVersion(sharpPath);
        Version = version;
        EventManager = sharedSystem.GetEventManager();
        EntityManager = sharedSystem.GetEntityManager();
        ClientManager = sharedSystem.GetClientManager();
        ConVarManager = sharedSystem.GetConVarManager();
        TransmitManager = sharedSystem.GetTransmitManager();
        HookManager = sharedSystem.GetHookManager();
        FileManager = sharedSystem.GetFileManager();
        SchemaManager = sharedSystem.GetSchemaManager();
        EconItemManager = sharedSystem.GetEconItemManager();
        SoundManager = sharedSystem.GetSoundManager();
        ModSharp = sharedSystem.GetModSharp();
        PhysicsQuery = sharedSystem.GetPhysicsQueryManager();
        GameData = sharedSystem.GetModSharp().GetGameData();
        LoggerFactory = sharedSystem.GetLoggerFactory();
        FileVersion = FileVersionInfo.GetVersionInfo(Path.Combine(dllPath, "ZombieModSharp.dll"));
        FileTime = GetSelfDBuildTime(dllPath);

        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory(ConfigPath);

        _logger = sharedSystem.GetLoggerFactory().CreateLogger<InterfaceBridge>();
    }

    private static Version GetGameVersion(string root)
    {
        const string prefix = "PatchVersion=";

        var patch = Path.Combine(root, "..", "csgo", "steam.inf");

        if (!File.Exists(patch)) throw new FileNotFoundException("Could not found steam.inf");

        try
        {
            var text = File.ReadAllLines(patch, Encoding.UTF8);

            foreach (var line in text)
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var pv = line.Replace(prefix, "", StringComparison.OrdinalIgnoreCase).TrimEnd();

                    return Version.Parse(pv);
                }

            throw new InvalidDataException("Invalid steam.inf");
        }
        catch (Exception e)
        {
            throw new InvalidDataException("Could not read steam.inf", e);
        }
    }

    private DateTime GetSelfDBuildTime(string dllPath)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var attr in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                if (attr.Key.Equals("BuildTime", StringComparison.OrdinalIgnoreCase) && attr.Value is not null)
                    return DateTime.Parse(attr.Value);

            throw new TypeAccessException("Could not found BuildTime In [AssemblyMetadata]");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get timestamp");

            return File.GetLastWriteTime(Path.Combine(dllPath, "StarDance.dll"));
        }
    }
}