using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class HitGroup : IHitGroup
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<HitGroup> _logger;
    private float[] higroupDatas = { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };

    public HitGroup(ISharedSystem sharedSystem)
    {
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<HitGroup>();
    }

    public void LoadConfig(string path)
    {
        var configPath = Path.Combine(path, "hitgroups.jsonc");

        Dictionary<string, float>? hitgroupConfig = [];

        try
        {
            var jsonContent = File.ReadAllText(configPath);
            
            // Simple comment removal (basic implementation)
            var lines = jsonContent.Split('\n');
            var cleanedLines = lines.Select(line => 
            {
                var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
            });
            var cleanedJson = string.Join("\n", cleanedLines);

            hitgroupConfig = JsonSerializer.Deserialize<Dictionary<string, float>>(cleanedJson) ?? [];

            _logger.LogInformation("Successfully loaded {count} weapon configurations", hitgroupConfig.Count);
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Error: {ex}", ex.Message);
            return;
        }

        if (hitgroupConfig == null)
        {
            _logger.LogCritical("The hitgroups datas is null!");
            return;
        }

        /*
        for (int i = 0; i < higroupDatas.Length; i++)
        {
            if (hitgroupConfig.TryGetValue(i.ToString(), out var kb))
            {
                higroupDatas[i] = kb;
                _logger.LogInformation("Found hitgroup: {index} | Knockback: {kb}", i, kb);
            }
        }
        */
    }
    
    public float GetHitgroupKnockback(int hitgroup)
    {
        if (hitgroup >= higroupDatas.Length || hitgroup < 0)
            return 1.0f;

        return higroupDatas[hitgroup];
    }
}
