using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Core.HookManager;

public class CvarServices : ICvarServices
{
    private readonly ISharedSystem _sharedSystem;
    private readonly IKnockback _knockback;
    private readonly IModSharp _modsharp;
    private readonly IConVarManager _conVarManager;
    private readonly ILogger<CvarServices> _logger;

    // Declare here I guess
    public Dictionary<string, IConVar?> CvarList { get; set; } = [];

    public CvarServices(ISharedSystem sharedSystem, IKnockback knockback)
    {
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<CvarServices>();
        _modsharp = _sharedSystem.GetModSharp();
        _conVarManager = _sharedSystem.GetConVarManager();
        _knockback = knockback;
    }
    
    public void Init()
    {
        // we create convar 
        CvarList["Cvar_HumanDefault"] = _conVarManager.CreateConVar("zms_human_class_default", "human_default", "Default human class when player join", ConVarFlags.Release);
        CvarList["Cvar_ZombieDefault"] = _conVarManager.CreateConVar("zms_zombie_class_default", "zombie_default", "Default zombie class when player join", ConVarFlags.Release);

        CvarList["Cvar_InfectCountdown"] = _conVarManager.CreateConVar("zms_infect_countdown", 10.0f, 5.0f, 60.0f, "Infection Countdown", ConVarFlags.Release);
        CvarList["Cvar_InfectMotherZombieRatio"] = _conVarManager.CreateConVar("zms_infect_motherzombie_ratio", 7.0f, 1.0f, 63.0f, "Motherzombie ratio for fist infection", ConVarFlags.Release);
        CvarList["Cvar_InfectMinimumZombie"] = _conVarManager.CreateConVar("zms_infect_minimum_zombie", 1, 1, 63, "Minimum zombie to spawn on first infection.", ConVarFlags.Release); 
        CvarList["Cvar_InfectNoblockEnable"] =  _conVarManager.CreateConVar("zms_infect_noblock_enable", true, "Enable noblock between player or not.", ConVarFlags.Release);
        CvarList["Cvar_InfectMotherZombieSpawn"] = _conVarManager.CreateConVar("zms_infect_motherzombie_spawn", true, "Teleport motherzombie back to spawn.", ConVarFlags.Release);
        CvarList["Cvar_InfectKnockbackScale"] = _conVarManager.CreateConVar("zms_infect_knockback_scale", 1.0f, 0.01f, 100.0f, "Knockback scale for modifying", ConVarFlags.Release);
        CvarList["Cvar_InfectKnockbackJumpScale"] = _conVarManager.CreateConVar("zms_infect_knockback_jump_scale", 1.8f, 0.01f, 100.0f, "Knockback jump scale for modifying", ConVarFlags.Release);
        CvarList["Cvar_InfectKnockbackDynamicScaleMaxZombie"] = _conVarManager.CreateConVar("zms_infect_knockback_dynamic_scale_max_zombie", 63, 1, 64, "Number of zombie to reach maximum dynamic scale", ConVarFlags.Release);
        CvarList["Cvar_InfectKnockbackDynamicScale"] = _conVarManager.CreateConVar("zms_infect_knockback_dynamic_scale", 1.48f, 1.0f, 2.0f, "Set more than 1.0 will enable dynamic knockback scale based on how many human left.", ConVarFlags.Release);
        CvarList["Cvar_InfectWarmupEnabled"] = _conVarManager.CreateConVar("zms_infect_warmup_enabled", false, "Enable infection game during warmup or not", ConVarFlags.Release);
        CvarList["Cvar_InfectDamageCash"] = _conVarManager.CreateConVar("zms_infect_damage_cash", 1.0f, 0.0f, 100.0f, "Multiplier cash that will receive when damage the zombie.", ConVarFlags.Release);
        CvarList["Cvar_InfectHumanWinOverlay"] = _conVarManager.CreateConVar("zms_infect_human_win_overlay", "particles/oylsister/human_overlay.vpcf", "Overlay image path for human win in infection mode.", ConVarFlags.Release);
        CvarList["Cvar_InfectZombieWinOverlay"] = _conVarManager.CreateConVar("zms_infect_zombie_win_overlay", "particles/oylsister/zombie_overlay.vpcf", "Overlay image path for zombie win in infection mode.", ConVarFlags.Release);

        CvarList["Cvar_RespawnEnabled"] = _conVarManager.CreateConVar("zms_respawn_enabled", true, "Enable respawn during the round.", ConVarFlags.Release);
        CvarList["Cvar_RespawnDelay"] = _conVarManager.CreateConVar("zms_respawn_delay", 5.0f, "Respawn Delay timer after death.", ConVarFlags.Release);
        CvarList["Cvar_RespawnLateJoin"] = _conVarManager.CreateConVar("zms_respawn_late_join", true, "Allow player to join during the round.", ConVarFlags.Release);
        CvarList["Cvar_RespawnTeam"] = _conVarManager.CreateConVar("zms_respawn_team", 0, 0, 2, "Respawn Team [0 = Zombie|1 = Human|2 = based on player team]", ConVarFlags.Release);
        CvarList["Cvar_RespawnTogglerEnable"] = _conVarManager.CreateConVar("zms_respawn_toggler_enabled", true, "Enabled respawn toggler for ZE map.", ConVarFlags.Release);

        CvarList["Cvar_ZTeleAllow"] = _conVarManager.CreateConVar("zms_ztele_allow", true, "Allow Ztele command or not", ConVarFlags.Release);
        CvarList["Cvar_ZTeleDelay"] = _conVarManager.CreateConVar("zms_ztele_delay", 5.0f, "Delay timer before player can get teleported with ztele command", ConVarFlags.Release);

        CvarList["Cvar_ZAmmoAllow"] = _conVarManager.CreateConVar("zms_zammo_allow", true, "Allow zammo command or not", ConVarFlags.Release);
        CvarList["Cvar_ZAmmoCost"] = _conVarManager.CreateConVar("zms_zammo_cost", 7500, 0, 99999, "Cash cost to activate infinite ammo", ConVarFlags.Release);
        CvarList["Cvar_ZAmmoDuration"] = _conVarManager.CreateConVar("zms_zammo_duration", 15.0f, 1.0f, 120.0f, "Duration (seconds) infinite ammo lasts", ConVarFlags.Release);
        CvarList["Cvar_ZAmmoUsesPerRound"] = _conVarManager.CreateConVar("zms_zammo_uses_per_round", 1, 1, 120, "Maximum number of times each player can use zammo per round", ConVarFlags.Release);

        CvarList["Cvar_ZKnifeAllow"] = _conVarManager.CreateConVar("zms_zknife_allow", true, "Allow zknife command or not", ConVarFlags.Release);
        CvarList["Cvar_ZKnifeCost"] = _conVarManager.CreateConVar("zms_zknife_cost", 10000, 0, 99999, "Cash cost to activate power knife", ConVarFlags.Release);
        CvarList["Cvar_ZKnifeDuration"] = _conVarManager.CreateConVar("zms_zknife_duration", 3.0f, 1.0f, 120.0f, "Duration (seconds) power knife lasts", ConVarFlags.Release);
        CvarList["Cvar_ZKnifeDamage"] = _conVarManager.CreateConVar("zms_zknife_damage", 20000, 1, 1000000, "Damage dealt by power knife", ConVarFlags.Release);
        CvarList["Cvar_ZKnifeUsesPerRound"] = _conVarManager.CreateConVar("zms_zknife_uses_per_round", 1, 1, 120, "Maximum number of times each player can use zknife per round", ConVarFlags.Release);
        // we check if covar existed or not.
        
        _conVarManager.InstallChangeHook(CvarList["Cvar_InfectKnockbackScale"]!, OnConVarChange);
        _conVarManager.InstallChangeHook(CvarList["Cvar_InfectKnockbackJumpScale"]!, OnConVarChange);
        _conVarManager.InstallChangeHook(CvarList["Cvar_RespawnEnabled"]!, OnConVarChange);
        _conVarManager.InstallChangeHook(CvarList["Cvar_InfectDamageCash"]!, OnConVarChange);
        var ShakeHead = _conVarManager.FindConVar("mp_flinch_punch_scale", true);
        if (ShakeHead != null)
        {
            ShakeHead.Set(0);
        }

        AutoExecConfigFile();
    }

    public void Shutdown()
    {
        _conVarManager.RemoveChangeHook(CvarList["Cvar_InfectKnockbackScale"]!, OnConVarChange);
        _conVarManager.RemoveChangeHook(CvarList["Cvar_InfectKnockbackJumpScale"]!, OnConVarChange);
        _conVarManager.RemoveChangeHook(CvarList["Cvar_RespawnEnabled"]!, OnConVarChange);
        _conVarManager.RemoveChangeHook(CvarList["Cvar_InfectDamageCash"]!, OnConVarChange);
    }

    private void OnConVarChange(IConVar convar)
    {
        if(convar.Name == "zms_infect_knockback_scale")
        {
            var scale = convar.GetFloat();
            _knockback.SetKnockbackScale(scale);
            //_modsharp.PrintToChatAll($"ConVar: zms_infect_knockback_scale set to {scale}");
            _logger.LogInformation("Scale is set to {scale}", scale);
        }

        if(convar.Name == "zms_infect_knockback_jump_scale")
        {
            var scale = convar.GetFloat();
            _knockback.SetJumpKnockbackScale(scale);
            //_modsharp.PrintToChatAll($"ConVar: zms_infect_knockback_jump_scale set to {scale}");
            _logger.LogInformation("Jump Scale is set to {scale}", scale);
        }

        if(convar.Name == "zms_respawn_enabled")
        {
            var enabled = convar.GetBool();
            RespawnServices.SetRespawnEnable(enabled);
        }

        if(convar.Name == "zms_infect_damage_cash")
        {
            Infect.CashMultiply = convar.GetFloat();
        }
    }

    // we create convar file here.
    private void AutoExecConfigFile()
    {
        // search for path first.
        var gamePath = _sharedSystem.GetModSharp().GetGamePath();
        var configPath = Path.Combine(gamePath, "cfg", "zombiemodsharp");

        if (!Directory.Exists(configPath))
        {
            _logger.LogWarning("Path {config} is not existed, create new one.", configPath);
            Directory.CreateDirectory(configPath);
        }

        var configFile = Path.Combine(configPath, "zombiemodsharp.cfg");

        if(!File.Exists(configFile))
        {
            // create new one
            _modsharp.InvokeFrameActionAsync(async () => { 
                await CreateNewConfigFileAsync(configFile);
                _modsharp.ServerCommand($"exec zombiemodsharp/zombiemodsharp.cfg");
            });
        }

        else
            _modsharp.ServerCommand($"exec zombiemodsharp/zombiemodsharp.cfg");
    }

    private async Task CreateNewConfigFileAsync(string path)
    {
        if (File.Exists(path))
        {
            _logger.LogWarning("Config file is already existed!");
            return;
        }

        using (var configFile = new StreamWriter(path, false, System.Text.Encoding.UTF8))
        {
            await configFile.WriteLineAsync($"// This file is generated by ZombieModSharp.dll at {DateTime.Today}");
            await configFile.WriteLineAsync();

            foreach (var convar in CvarList)
            {
                if (convar.Value == null)
                    continue;

                try
                {
                    _logger.LogInformation("Writing convar: {Name}", convar.Value.Name);
                    await CreateConVarLineAsync(configFile, convar.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing convar: {Name}", convar.Value.Name);
                    throw;
                }
            }
        }
    }

    private async Task CreateConVarLineAsync(StreamWriter configFile, IConVar conVar)
    {
        if (configFile == null)
        {
            _logger.LogCritical("[CreateConVarLine] Config File is null");
            return;
        }

        var command = conVar.Name;
        var defaultValue = conVar.GetString();
        var description = conVar.HelpString;

        await configFile.WriteLineAsync($"// {description}");
        await configFile.WriteLineAsync($"// -");
        await configFile.WriteLineAsync($"// Default: {defaultValue}");
        await configFile.WriteLineAsync($"{command} {defaultValue}");
        await configFile.WriteLineAsync();
    }
}
