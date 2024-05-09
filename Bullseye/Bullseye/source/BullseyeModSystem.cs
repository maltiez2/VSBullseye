using Bullseye.Old;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Bullseye;

internal class BullseyeModSystem : ModSystem
{
    public Aiming.ClientAiming? AimingSystem;
    public RangedWeapon.Synchronizer? Synchronizer;

    public override void Start(ICoreAPI api)
    {
        /*api.RegisterEntityBehaviorClass("Bullseye.aimingaccuracy", typeof(BullseyeEntityBehaviorAimingAccuracy));
        api.RegisterCollectibleBehaviorClass("Bullseye.Throwable", typeof(BullseyeCollectibleBehaviorThrowable));
        api.RegisterCollectibleBehaviorClass("Bullseye.Bow", typeof(BullseyeCollectibleBehaviorBow));
        api.RegisterCollectibleBehaviorClass("Bullseye.Spear", typeof(BullseyeCollectibleBehaviorSpear));
        api.RegisterCollectibleBehaviorClass("Bullseye.Sling", typeof(BullseyeCollectibleBehaviorSling));
        api.RegisterCollectibleBehaviorClass("Bullseye.ThrowableStone", typeof(BullseyeCollectibleBehaviorThrowableStone));
        api.RegisterCollectibleBehaviorClass("Bullseye.Ammunition", typeof(BullseyeCollectibleBehaviorAmmunition));*/
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
