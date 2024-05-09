using Bullseye.RangedWeapon;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Bullseye.Ammo;

public class AmmoSlot : ItemSlot
{
    public AmmoSlot(InventoryBase inventory) : base(inventory)
    {
    }

    public override bool CanTake() => false;
    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) => false;
    public override bool CanHold(ItemSlot sourceSlot) => false;
    public override bool TryFlipWith(ItemSlot itemSlot) => false;
    public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op) { /* Just empty */ }
    public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op) => 0;
    public override int TryPutInto(IWorldAccessor world, ItemSlot sinkSlot, int quantity = 1) => 0;

    public override string GetStackDescription(IClientWorldAccessor world, bool extendedDebugInfo)
    {
        string stackDescription = base.GetStackDescription(world, extendedDebugInfo);

        if (inventory is not IAmmoInventory inventoryAmmoSelect || inventoryAmmoSelect.PlayerEntity == null) return stackDescription;

        RangedWeaponBehavior? behaviorRangedWeapon = inventoryAmmoSelect.PlayerEntity.RightHandItemSlot.Itemstack?.Item.GetCollectibleBehavior<RangedWeaponBehavior>(true);
        if (behaviorRangedWeapon == null) return stackDescription;

        float weaponDamage = behaviorRangedWeapon.GetProjectileDamage(inventoryAmmoSelect.PlayerEntity, inventoryAmmoSelect.PlayerEntity.RightHandItemSlot, this);
        float breakChance = 1f - behaviorRangedWeapon.GetProjectileDropChance(inventoryAmmoSelect.PlayerEntity, inventoryAmmoSelect.PlayerEntity.RightHandItemSlot, this);
        string weaponName = behaviorRangedWeapon.collObj.GetHeldItemName(inventoryAmmoSelect.PlayerEntity.RightHandItemSlot.Itemstack);

        if (breakChance > 0f && breakChance < 1f)
        {
            stackDescription += "\n" + Lang.Get("bullseye:weapon-ranged-total-damage", weaponDamage, breakChance * 100f, weaponName);
        }
        else
        {
            stackDescription += "\n" + Lang.Get("bullseye:weapon-ranged-total-damage-no-drops", weaponDamage, weaponName);

            if (breakChance >= 1f) stackDescription += "\n" + Lang.Get("bullseye:projectile-always-breaks");
        }

        return stackDescription;
    }

    protected override void FlipWith(ItemSlot withSlot) { /* Just empty */ }
}
