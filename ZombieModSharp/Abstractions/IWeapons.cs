using Sharp.Shared.Objects;
using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Abstractions;

public interface IWeapons
{
    public void LoadConfig(string path);
    public void PurchaseWeapon(IGameClient client, WeaponData weapon);
    public bool TryPickupStackedGrenade(IGameClient client, string weaponEntityName);
    public float GetWeaponKnockback(string weaponentity);
    public WeaponAmmo? GetWeaponAmmo(string weaponentity);
    public bool IsWeaponRestricted(string weaponentity);
    public WeaponData GetWeaponDataWithEntityName(string weaponentity);
}
