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
        base.Start(api);

        if (api is ICoreClientAPI clientApi)
        {
            AimingSystem = new(clientApi);
            Synchronizer = new RangedWeapon.SynchronizerClient(clientApi);
        }

        if (api is ICoreServerAPI serverApi)
        {
            Synchronizer = new RangedWeapon.SynchronizerServer(serverApi);
        }
    }
}
