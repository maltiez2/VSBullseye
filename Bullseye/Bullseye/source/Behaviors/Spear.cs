using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Bullseye.RangedWeapon;

internal class SpearBehavior : RangedWeaponBehavior
{
    public SpearBehavior(CollectibleObject collObj) : base(collObj) { }

    public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        return base.GetProjectileDamage(byEntity, weaponSlot, ammoSlot) * Config?.GetDamage(WeaponsType.Spear) ?? 1f;
    }

    public override float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0.3f;
    public override int GetProjectileDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 3;
    public override float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 1.1f;

    public override EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        string entityCode = collObj.Attributes["projectileEntityCode"].AsString() ?? collObj.Attributes["spearEntityCode"].AsString();

        return byEntity.World.GetEntityType(new AssetLocation(entityCode));
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (inSlot.Itemstack?.ItemAttributes == null) return;

        float damage = inSlot.Itemstack.ItemAttributes["damage"].AsFloat(0) * Config?.GetDamage(WeaponsType.Spear) ?? 1f;

        dsc.AppendLine(damage + Lang.Get("piercing-damage-thrown"));
    }
}
