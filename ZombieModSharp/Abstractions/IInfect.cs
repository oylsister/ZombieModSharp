using Sharp.Shared.Objects;
using Shop.Shared;
using ZombieModSharp.Shared;

namespace ZombieModSharp.Abstractions;

public interface IInfect : IInfectShared
{
    public void OnRoundPreStart();
    public void OnRoundStart();
    public void OnRoundEnd();
    public void OnRoundFreezeEnd();
    public void CheckGameStatus();
    public bool IsInfectStarted();
    public void SetInfectStarted(bool result);
    public void SetTestMode(bool result);
    public bool IsTestMode();
    void SetShopKnifeApi(IShopKnifeApi? shopKnifeApi);
}
