using Bullseye.Aiming;
using Bullseye.Ammo;
using Bullseye.RangedWeapon;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Bullseye;

internal class BullseyeModSystem : ModSystem
{
    public Aiming.ClientAiming? AimingSystem { get; private set; }
    public RangedWeapon.Synchronizer? Synchronizer { get; private set; }

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("Bullseye.aimingaccuracy", typeof(AimingAccuracyBehavior));
        api.RegisterCollectibleBehaviorClass("Bullseye.Throwable", typeof(ThrowableBehavior));
        api.RegisterCollectibleBehaviorClass("Bullseye.Bow", typeof(BowBehavior));
        api.RegisterCollectibleBehaviorClass("Bullseye.Spear", typeof(SpearBehavior));
        api.RegisterCollectibleBehaviorClass("Bullseye.Sling", typeof(SlingBehavior));
        api.RegisterCollectibleBehaviorClass("Bullseye.ThrowableStone", typeof(StoneBehavior));
        api.RegisterCollectibleBehaviorClass("Bullseye.Ammunition", typeof(AmmunitionBehavior));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _side = EnumAppSide.Client;
        AimingSystem = new(api);
        Synchronizer = new RangedWeapon.SynchronizerClient(api);
        HarmonyPatches.Patch(_harmonyId, AimingSystem);

        api.Input.RegisterHotKey("bullseye.ammotypeselect", Lang.Get("bullseye:select-ammo"), GlKeys.F, HotkeyType.GUIOrOtherControls);
        api.Gui.RegisterDialog(new AmmoSelectDialog(api));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _side = EnumAppSide.Server;
        Synchronizer = new RangedWeapon.SynchronizerServer(api);
    }

    public override void Dispose()
    {
        if (_side == EnumAppSide.Client) HarmonyPatches.Unpatch(_harmonyId);
    }

    private string _harmonyId = "bullseye";
    private EnumAppSide _side;
}
