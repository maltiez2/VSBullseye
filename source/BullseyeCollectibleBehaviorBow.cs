﻿using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Bullseye
{
    public class BullseyeCollectibleBehaviorBow : BullseyeCollectibleBehaviorRangedWeapon
    {
        private int _bowDrawAnimation;

        public BullseyeCollectibleBehaviorBow(CollectibleObject collObj) : base(collObj) { }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            _properties = properties;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            WeaponStats.weaponType = BullseyeRangedWeaponType.Bow;

            PrepareAnimations();
            SetOffsets();
        }


        public override void OnAimingStart(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);

                /*collObj.GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.StartAnimation(new AnimationMetaData()
				{
					Animation = "Draw",
					Code = "draw",
					AnimationSpeed = 0.5f / GetChargeNeeded(api, byEntity),
					EaseOutSpeed = 6,
					EaseInSpeed = 15
				});*/
            }

            StartDrawAnimation(GetChargeNeeded(api, byEntity));

            slot.Itemstack.Attributes.SetInt("renderVariant", 1);

            byEntity.AnimManager.StartAnimation("bowaim");

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-draw"), byEntity, byPlayer, false, 8);
        }

        public override void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            //if (byEntity.World is IClientWorldAccessor)
            {
                // Vanilla is broken, only shows 2 out of 3 charged states
                int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 3f / GetChargeNeeded(api, byEntity)), 0, 4);
                int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 0);

                slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
                slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

                if (prevRenderVariant != renderVariant)
                {
                    (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                }
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                SetAimAnimation(api as ICoreClientAPI);
            }
        }

        public override void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason)
        {
            if (byEntity == null) return;

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");

                ResetAimAnimation(api as ICoreClientAPI);
            }

            slot.Itemstack?.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse || secondsUsed < GetChargeNeeded(api, byEntity))
            {
                byEntity.AnimManager.StopAnimation("bowaim");

                if (byEntity.Api.Side == EnumAppSide.Client)
                {
                    collObj.GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.StopAnimation("draw");
                }
            }
        }

        public override bool CanUseAmmoSlot(ItemSlot checkedSlot)
        {
            return base.CanUseAmmoSlot(checkedSlot) || checkedSlot.Itemstack.Collectible.Code.Path.StartsWith("arrow-");
        }

        public override ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot, bool isStartCheck = false)
        {
            ItemSlot arrowSlot = base.GetNextAmmoSlot(byEntity, weaponSlot, isStartCheck);

            if (isStartCheck && arrowSlot != null)
            {
                weaponSlot.Itemstack?.TempAttributes?.SetItemstack("loadedAmmo", arrowSlot.Itemstack);

                if (api is ICoreClientAPI capi)
                {
                    ItemRenderInfo renderInfo = capi.Render.GetItemStackRenderInfo(arrowSlot, EnumItemRenderTarget.Ground, 0);
                    renderInfo.Transform = renderInfo.Transform.Clone();

                    // Scale arrows down - ground model of arrows is 21 voxels long, but in bows, the arrows are only 15 units long
                    float originalArrowSize = 21f;
                    float bowArrowSize = 15f;
                    float groundScaleFactor = bowArrowSize / originalArrowSize;

                    float arrowScale = weaponSlot.Itemstack?.Collectible?.Attributes?["arrowScale"].AsFloat(1) ?? 1f;

                    renderInfo.Transform.Translation.Z = -(bowArrowSize / 2f) * arrowScale;
                    renderInfo.Transform.ScaleXYZ.X = arrowScale * groundScaleFactor;
                    renderInfo.Transform.ScaleXYZ.Y = arrowScale * groundScaleFactor;
                    renderInfo.Transform.ScaleXYZ.Z = arrowScale * groundScaleFactor;

                    collObj.GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.SetAttachedRenderInfo(renderInfo);
                }
            }

            return arrowSlot;
        }

        public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
        {
            float damage = base.GetProjectileDamage(byEntity, weaponSlot, ammoSlot);
            damage *= ConfigSystem?.GetSyncedConfig()?.ArrowDamage ?? 1f;

            return damage;
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

            if (entityCode != null) return byEntity.World.GetEntityType(new AssetLocation(entityCode));

            // Fallback for modded arrows that aren't made explicitly compatible
            return byEntity.World.GetEntityType(new AssetLocation("arrow-" + ammoSlot.Itemstack.Collectible.Variant["material"]));
        }

        public override int GetWeaponDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
        {
            return 1;
        }

        public override void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");

                collObj.GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.StopAnimation("draw", true);
                collObj.GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.SetAttachedRenderInfo(null);
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);
            byEntity.AnimManager.StartAnimation("bowhit");

            api.Event.RegisterCallback((ms) =>
            {
                byEntity.AnimManager.StopAnimation("bowaim");
            }, 500);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack.ItemAttributes == null) return;

            float dmg = inSlot.Itemstack.ItemAttributes["damage"].AsFloat(0) * ConfigSystem?.GetSyncedConfig()?.ArrowDamage ?? 1f;
            if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("piercing-damage"));

            float dmgPercent = inSlot.Itemstack.ItemAttributes["damagePercent"].AsFloat(0) * 100f;
            if (dmgPercent != 0) dsc.AppendLine((dmgPercent > 0 ? "+" : "") + Lang.Get("bullseye-continued:weapon-bonus-damage-ranged", dmgPercent));
        }

        private JsonObject _properties;
        private float _followFactor_X = 0.07f;
        private float _followFactor_Y = 0.06f;
        private float _followOffset_X = 0.0f;
        private float _followOffset_Y = 0.0f;
        private float _followTpFactor = 0.5f;
        private float _followSmoothFactor = 0.3f;
        private float _timeModifier = 0.7f;


        private void SetOffsets()
        {
            _followFactor_X = _properties["followHorizontal"].AsFloat(_followFactor_X);
            _followFactor_Y = _properties["followVertical"].AsFloat(_followFactor_Y);
            _followOffset_X = _properties["offsetHorizontal"].AsFloat(_followOffset_X);
            _followOffset_Y = _properties["offsetVertical"].AsFloat(_followOffset_Y);
        }

        protected void SetAimAnimation(ICoreClientAPI capi)
        {
            Vec2f currentAim = CoreClientSystem.GetCurrentAim();

            float Y = 45.0f - currentAim.X * _followFactor_X + _followOffset_X;
            float Z = 45.0f + currentAim.Y * _followFactor_Y + _followOffset_Y;
            float Ytp = 45.0f - currentAim.X * _followFactor_X * _followTpFactor + _followOffset_X;
            float Ztp = 45.0f + currentAim.Y * _followFactor_Y * _followTpFactor + _followOffset_Y;
        }

        protected void ResetAimAnimation(ICoreClientAPI capi)
        {
            PrepareAnimations();
        }

        private void StartDrawAnimation(float timeModifier)
        {

        }

        private bool _animationsReady = false;
        private void PrepareAnimations()
        {
            if (_animationsReady) return;
            _animationsReady = true;
            if (api.Side == EnumAppSide.Server) return;

        }
    }
}
