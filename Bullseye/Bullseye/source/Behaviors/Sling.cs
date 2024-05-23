﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Bullseye.RangedWeapon;

internal class SlingBehavior : RangedWeaponBehavior
{
    public SlingBehavior(CollectibleObject collObj) : base(collObj) { }

    private CharacterSystem characterSystem;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        Stats.WeaponType = WeaponsType.Sling;

        characterSystem = api.ModLoader.GetModSystem<CharacterSystem>();
    }

    public override void OnAimingStart(ItemSlot slot, EntityAgent byEntity)
    {
        if (byEntity.World is IClientWorldAccessor)
        {
            slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
        }

        slot.Itemstack.Attributes.SetInt("renderVariant", 1);

        byEntity.AnimManager.StartAnimation("slingaimbalearic");

        IPlayer byPlayer = null;
        if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
        byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-draw"), byEntity, byPlayer, false, 8);
    }

    public override void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
    {
        int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 4), 0, 3);
        int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 0);

        slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
        slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

        if (prevRenderVariant != renderVariant)
        {
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
        }
    }

    public override void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason)
    {
        if (byEntity == null) return;

        if (cancelReason != EnumItemUseCancelReason.ReleasedMouse || secondsUsed < GetChargeNeeded(Api, byEntity))
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack?.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            byEntity.AnimManager.StopAnimation("slingaimbalearic");
        }
    }

    /*protected override void SetAimTransform(ICoreClientAPI capi, ModelTransform transform)
    {
        Vec2f currentAim = CoreClientSystem.GetCurrentAim();

        float secondsUsed = (api.World.ElapsedMilliseconds - RangedWeaponSystem.GetEntityChargeStart(capi.World.Player.Entity.EntityId)) / 1000f;

        transform.Rotation.X = secondsUsed * 360f / 0.75f;
        transform.Rotation.Y = DefaultFpHandTransform.Rotation.Y - (currentAim.X / 15f);
    }*/

    public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        float damage = base.GetProjectileDamage(byEntity, weaponSlot, ammoSlot);
        damage *= Config?.GetDamage(WeaponsType.Sling) ?? 1f;

        if (byEntity is EntityPlayer entityPlayer && (characterSystem?.HasTrait(entityPlayer.Player, "improviser") ?? false))
        {
            // Good candidate for a GetEntityPlayerTraits extension method in TeaLib?
            string playerClass = byEntity.WatchedAttributes.GetString("characterClass");

            double rangedWeaponsDamage = 1f;

            if (characterSystem.characterClassesByCode.TryGetValue(playerClass, out CharacterClass characterClass))
            {
                IEnumerable<string> traitCodes = characterClass.Traits.Concat(entityPlayer.WatchedAttributes.GetStringArray("extraTraits", Array.Empty<string>()));

                foreach (string traitCode in traitCodes)
                {
                    if (!characterSystem.TraitsByCode.TryGetValue(traitCode, out Trait trait)) continue;
                    if (!trait.Attributes.TryGetValue("rangedWeaponsDamage", out double rangedWeaponsDamageMod)) continue;
                    if (rangedWeaponsDamageMod >= 0) continue;

                    rangedWeaponsDamage += rangedWeaponsDamageMod;
                }
            }

            damage /= (float)rangedWeaponsDamage;
        }

        return damage;
    }

    public override float GetProjectileVelocity(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        float velocity = base.GetProjectileVelocity(byEntity, weaponSlot, ammoSlot);

        if (byEntity is EntityPlayer entityPlayer && (characterSystem?.HasTrait(entityPlayer.Player, "improviser") ?? false))
        {
            // Good candidate for a GetEntityPlayerTraits extension method in TeaLib?
            string playerClass = byEntity.WatchedAttributes.GetString("characterClass");

            double totalDrawingStrength = 1f;

            if (characterSystem.characterClassesByCode.TryGetValue(playerClass, out CharacterClass characterClass))
            {
                IEnumerable<string> traitCodes = characterClass.Traits.Concat(entityPlayer.WatchedAttributes.GetStringArray("extraTraits", Array.Empty<string>()));

                foreach (string traitCode in traitCodes)
                {
                    if (!characterSystem.TraitsByCode.TryGetValue(traitCode, out Trait trait)) continue;
                    if (!trait.Attributes.TryGetValue("bowDrawingStrength", out double bowDrawingStrengthMod)) continue;
                    if (bowDrawingStrengthMod >= 0) continue;

                    totalDrawingStrength += bowDrawingStrengthMod;
                }
            }

            velocity /= (float)totalDrawingStrength;
        }

        return velocity;
    }

    public override float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        float breakChance = 0.5f;

        if (ammoSlot.Itemstack.ItemAttributes != null)
        {
            if (ammoSlot.Itemstack.ItemAttributes.KeyExists("averageLifetimeDamage"))
            {
                breakChance = 1f / (ammoSlot.Itemstack.ItemAttributes["averageLifetimeDamage"].AsFloat() / GetProjectileDamage(byEntity, weaponSlot, ammoSlot));
            }
            else
            {
                breakChance = ammoSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);
            }
        }

        return 1f - breakChance;
    }

    public override EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        string entityCode = ammoSlot.Itemstack.ItemAttributes["projectileEntityCode"].AsString();

        return (entityCode != null) ? byEntity.World.GetEntityType(new AssetLocation(entityCode)) : null;
    }

    public override int GetWeaponDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        return 1;
    }

    public override void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity)
    {
        byEntity.AnimManager.StopAnimation("slingaimbalearic");

        byEntity.World.RegisterCallback((dt) => slot.Itemstack?.Attributes.SetInt("renderVariant", 2), 250);
        byEntity.World.RegisterCallback((dt) =>
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
            }
            slot.Itemstack?.Attributes.SetInt("renderVariant", 0);
        }, 450);

        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

        byPlayer?.InventoryManager.BroadcastHotbarSlot();
        byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/sling1"), byEntity, byPlayer, false, 8, 0.25f);

        byEntity.AnimManager.StartAnimation("slingthrowbalearic");

        byEntity.World.RegisterCallback((dt) => byEntity.AnimManager.StopAnimation("slingthrowbalearic"), 400);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (inSlot.Itemstack.ItemAttributes == null) return;

        float dmg = inSlot.Itemstack.ItemAttributes["damage"].AsFloat(0) * Config?.GetDamage(WeaponsType.Sling) ?? 1f;
        if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("piercing-damage"));

        float dmgPercent = inSlot.Itemstack.ItemAttributes["damagePercent"].AsFloat(0) * 100f;
        if (dmgPercent != 0) dsc.AppendLine((dmgPercent > 0 ? "+" : "") + Lang.Get("bullseye:weapon-bonus-damage-ranged", dmgPercent));
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);

        characterSystem = null;
    }
}
