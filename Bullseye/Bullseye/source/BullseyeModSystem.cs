using OpenTK.Windowing.GraphicsLibraryFramework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Bullseye;

internal class BullseyeModSystem : ModSystem
{
    public Aiming.ClientAiming? AimingSystem;
    public RangedWeapon.Synchronizer? Synchronizer;

    public override void Start(ICoreAPI api)
    {

    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _side = EnumAppSide.Client;
        AimingSystem = new(api);
        Synchronizer = new RangedWeapon.SynchronizerClient(api);
        HarmonyPatches.Patch(_harmonyId, AimingSystem);
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
