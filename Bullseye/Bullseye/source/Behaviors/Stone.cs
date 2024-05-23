using Vintagestory.API.Common;

namespace Bullseye.RangedWeapon;

internal class StoneBehavior : RangedWeaponBehavior
{
    public StoneBehavior(CollectibleObject collObj) : base(collObj) { }

    protected override bool CanStartAiming(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (byEntity.Controls.ShiftKey) return false;

        return base.CanStartAiming(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
    }
}
