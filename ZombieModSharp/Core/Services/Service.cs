using Microsoft.Extensions.DependencyInjection;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Core.HookManager;
using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Core.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddZombieModSharpServices(this IServiceCollection services)
    {
        services.AddSingleton<IEvents, Events>()
            .AddSingleton<IPlayerManager, PlayerManager>()
            .AddSingleton<IInfect, Infect>()
            .AddSingleton<IListeners, Listeners>()
            .AddSingleton<IZTele, ZTele>()
            .AddSingleton<ICommand, Command>()
            .AddSingleton<IHooks, Hooks>()
            .AddSingleton<IKnockback, Knockback>()
            .AddSingleton<IWeapons, Weapons>()
            .AddSingleton<IConfigs, Configs>()
            .AddSingleton<IHitGroup, HitGroup>()
            .AddSingleton<ICvarServices, CvarServices>()
            .AddSingleton<IPlayerClasses, PlayerClasses>()
            .AddSingleton<IPrecacheManager, PrecacheManager>()
            .AddSingleton<ISoundServices, SoundServices>()
            .AddSingleton<IRespawnServices, RespawnServices>()
            .AddSingleton<IGrenadeEffect, GrenadeEffect>()
            .AddSingleton<IMarkerServices, MarkerServices>()
            .AddSingleton<IGlowServices, GlowServices>()
            .AddSingleton<ILeaderServices, LeaderServices>();

        return services;
    }
}