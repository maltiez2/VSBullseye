using Bullseye.Aiming;
using Bullseye.Ammo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Bullseye.RangedWeapon;

internal class RangedWeaponBehavior : CollectibleBehavior
{
    public RangedWeaponBehavior(CollectibleObject collObj) : base(collObj) { }

    public string AmmoType => Stats?.AmmoType ?? "";

    public override void OnLoaded(ICoreAPI api)
    {
        Api = api;

        System = api.ModLoader.GetModSystem<BullseyeModSystem>();

        Aiming = System.AimingSystem;
        Config = api.ModLoader.GetModSystem<ConfigSystem>();
        SynchronizerClient = System.Synchronizer as SynchronizerClient;
        SynchronizerServer = System.Synchronizer as SynchronizerServer;

        Stats = collObj.Attributes.KeyExists("bullseyeWeaponStats") ? collObj.Attributes?["bullseyeWeaponStats"].AsObject<WeaponStats>() : new WeaponStats();

        if (api is ICoreClientAPI clientApi)
        {
            PrepareHeldInteractionHelp();

            AimTexPartCharge = new LoadedTexture(clientApi);
            AimTexFullCharge = new LoadedTexture(clientApi);
            AimTexBlocked = new LoadedTexture(clientApi);

            if (Stats?.aimTexPartChargePath != null) clientApi.Render.GetOrLoadTexture(new AssetLocation(Stats.aimTexPartChargePath), ref AimTexPartCharge);
            if (Stats?.aimTexFullChargePath != null) clientApi.Render.GetOrLoadTexture(new AssetLocation(Stats.aimTexFullChargePath), ref AimTexFullCharge);
            if (Stats?.aimTexBlockedPath != null) clientApi.Render.GetOrLoadTexture(new AssetLocation(Stats.aimTexBlockedPath), ref AimTexBlocked);
        }
        else
        {
            api.Event.RegisterEventBusListener(ServerHandleFire, 0.5, "bullseyeRangedWeaponFire");
        }
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (handHandling == EnumHandHandling.PreventDefault || handling == EnumHandling.PreventDefault || Stats == null) return;
        if (!CanStartAiming(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling)) return;

        ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, slot, true);
        if (ammoSlot == null) return;

        EntityProperties projectileEntity = GetProjectileEntityType(byEntity, slot, ammoSlot);
        if (projectileEntity == null) return;

        if (byEntity.World is IClientWorldAccessor && AimTexPartCharge != null && AimTexFullCharge != null && AimTexBlocked != null)
        {
            Aiming?.SetReticleTextures(AimTexPartCharge, AimTexFullCharge, AimTexBlocked);
        }

        SynchronizerServer?.SetLastEntityRangedChargeData(byEntity.EntityId, slot);

        byEntity.GetBehavior<AimingAccuracyBehavior>().Stats = Stats;

        // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
        byEntity.Attributes.SetInt("bullseyeAiming", 1);
        byEntity.Attributes.SetInt("bullseyeAimingCancel", 0);

        if (!Stats.AllowSprint)
        {
            byEntity.Controls.Sprint = false;
            byEntity.ServerControls.Sprint = false;
        }

        OnAimingStart(slot, byEntity);
        handHandling = EnumHandHandling.PreventDefault;
        handling = EnumHandling.PreventDefault;
    }
    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Synchronizer == null || Stats == null) return false;

        if (!Synchronizer.HasEntityCooldownPassed(byEntity.EntityId, Stats.CooldownTime))
        {
            handling = EnumHandling.PreventSubsequent;
            return false;
        }

        if (byEntity.Attributes.GetInt("bullseyeAiming") == 1)
        {
            OnAimingStep(secondsUsed, slot, byEntity);

            if (byEntity.World is IClientWorldAccessor)
            {
                // Show different reticle if we are ready to shoot
                // - Show white "full charge" reticle if the accuracy is fully calmed down, + a little leeway to let the reticle calm down fully
                // - Show yellow "partial charge" reticle if the bow is ready for a snap shot, but accuracy is still poor
                // --- OR if the weapon was held so long that accuracy is starting to get bad again, for weapons that have it
                // - Show red "blocked" reticle if the weapon can't shoot yet
                bool showBlocked = secondsUsed < GetEntityChargeTime(byEntity);
                bool showPartCharged = secondsUsed < Stats.accuracyStartTime / byEntity.Stats.GetBlended("rangedWeaponsSpeed") + Stats.aimFullChargeLeeway;
                showPartCharged = showPartCharged || secondsUsed > Stats.accuracyOvertimeStart + Stats.accuracyStartTime && Stats.accuracyOvertime > 0;

                if (Aiming != null)
                {
                    if (showBlocked)
                    {
                        Aiming.Renderer.AimingState = WeaponAimingState.Blocked;
                    }
                    else if (showPartCharged)
                    {
                        Aiming.Renderer.AimingState = WeaponAimingState.PartCharge;
                    }
                    else
                    {
                        Aiming.Renderer.AimingState = WeaponAimingState.FullCharge;
                    }
                }
            }

            handling = EnumHandling.PreventDefault;
        }

        return true;
    }
    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        if (byEntity.Attributes.GetInt("bullseyeAimingCancel") == 1) return true;

        byEntity.Attributes.SetInt("bullseyeAiming", 0);

        if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
        {
            byEntity.Attributes.SetInt("bullseyeAimingCancel", 1);
        }

        OnAimingCancel(secondsUsed, slot, byEntity, cancelReason);

        handled = EnumHandling.PreventDefault;

        return true;
    }
    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (byEntity.Attributes.GetInt("bullseyeAimingCancel") == 1)
        {
            handling = EnumHandling.PreventDefault;
            return;
        }
        byEntity.Attributes.SetInt("bullseyeAiming", 0);

        EntityPlayer entityPlayer = byEntity as EntityPlayer;

        if (!byEntity.Alive || secondsUsed < GetChargeNeeded(Api, byEntity))
        {
            OnAimingCancel(secondsUsed, slot, byEntity, !byEntity.Alive ? EnumItemUseCancelReason.Death : EnumItemUseCancelReason.ReleasedMouse);
            handling = EnumHandling.PreventDefault;
            return;
        }

        if (Api.Side == EnumAppSide.Server)
        {
            // Just to make sure animations etc. get stopped if a shot looks legit on the server but was stopped on the client
            Api.Event.RegisterCallback((ms) =>
            {
                if (byEntity.Attributes.GetInt("bullseyeAiming") == 0)
                {
                    OnAimingCancel(secondsUsed, slot, byEntity, !byEntity.Alive ? EnumItemUseCancelReason.Death : EnumItemUseCancelReason.ReleasedMouse);
                }
            }, 500);
        }
        else
        {
            if (Aiming != null)
            {
                Vec3d targetVec = Aiming.TargetVec;
                Shoot(slot, byEntity, targetVec);
                SynchronizerClient?.SendRangedWeaponFirePacket(collObj.Id, targetVec);
            }
        }

        handling = EnumHandling.PreventDefault;
    }
    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling) => _interactions;
    public override void OnUnloaded(ICoreAPI api)
    {
        AimTexFullCharge?.Dispose();
        AimTexPartCharge?.Dispose();
        AimTexBlocked?.Dispose();
    }

    public virtual bool CanUseAmmoSlot(ItemSlot checkedSlot)
    {
        if (checkedSlot.Itemstack.ItemAttributes?["ammoTypes"]?[AmmoType]?.Exists ?? false) return true;

        return AmmoType == checkedSlot.Itemstack.ItemAttributes?["ammoType"].AsString();
    }
    public virtual List<ItemStack> GetAvailableAmmoTypes(ItemSlot slot, IClientPlayer forPlayer)
    {
        if (AmmoType == null)
        {
            return null;
        }

        List<ItemStack> ammoTypes = new();

        forPlayer.Entity.WalkInventory((invslot) =>
        {
            if (invslot is ItemSlotCreative) return true;

            if (invslot.Itemstack != null && CanUseAmmoSlot(invslot))
            {
                ItemStack ammoStack = ammoTypes.Find(itemstack => itemstack.Equals(Api.World, invslot.Itemstack, GlobalConstants.IgnoredStackAttributes));

                if (ammoStack == null)
                {
                    ammoStack = invslot.Itemstack.GetEmptyClone();
                    ammoStack.StackSize = invslot.StackSize;
                    ammoTypes.Add(ammoStack);
                }
                else
                {
                    ammoStack.StackSize += invslot.StackSize;
                }
            }

            return true;
        });

        if (ammoTypes.Count <= 0)
        {
            return null;
        }

        ammoTypes.Sort((ItemStack X, ItemStack Y) =>
        {
            float xDamage = X.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;
            float yDamage = Y.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;

            // Ascending sort by damage, or by name if damage is equal
            return (xDamage - yDamage) switch
            {
                > 0 => 1,
                < 0 => -1,
                _ => String.Compare(X.GetName(), Y.GetName())
            };
        });

        return ammoTypes;
    }
    public virtual ItemStack GetEntitySelectedAmmoType(EntityAgent entity)
    {
        if (AmmoType == null)
        {
            return null;
        }

        ITreeAttribute treeAttribute = entity.Attributes.GetTreeAttribute("bullseyeSelectedAmmo");

        ItemStack resultItemstack = treeAttribute?.GetItemstack(AmmoType, null);
        resultItemstack?.ResolveBlockOrItem(Api.World);

        return resultItemstack;
    }
    public virtual void OnAimingStart(ItemSlot slot, EntityAgent byEntity) { }
    public virtual void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity) { }
    public virtual void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) { }
    public virtual void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity) { }
    public virtual ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot, bool isStartCheck = false)
    {
        if (AmmoType == null || byEntity == null || weaponSlot.Itemstack == null) return null;

        ItemSlot ammoSlot = null;
        ItemStack selectedAmmoType = GetEntitySelectedAmmoType(byEntity);

        byEntity.WalkInventory((invslot) =>
        {
            if (invslot == null || invslot is ItemSlotCreative) return true;

            if (invslot.Itemstack != null && CanUseAmmoSlot(invslot))
            {
                // If we found the selected ammo type or no ammo type is specifically selected, return the first one we find
                if (selectedAmmoType == null || invslot.Itemstack.Equals(Api.World, selectedAmmoType, GlobalConstants.IgnoredStackAttributes))
                {
                    ammoSlot = invslot;
                    return false;
                }

                // Otherwise just get the first ammo stack we find
                if (ammoSlot == null)
                {
                    ammoSlot = invslot;
                }
            }

            return true;
        });

        return ammoSlot;
    }
    public virtual float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        float damage = 0f;

        if (ammoSlot?.Itemstack?.Collectible != null && Stats != null)
        {
            AmmunitionBehavior cbAmmunition = ammoSlot.Itemstack.Collectible.GetCollectibleBehavior<AmmunitionBehavior>(true);
            damage = cbAmmunition != null ? cbAmmunition.GetDamage(ammoSlot, Stats.AmmoType, byEntity.World) : ammoSlot.Itemstack.ItemAttributes?["damage"].AsFloat(0) ?? 0f;
        }

        // Weapon modifiers
        damage *= (1f + weaponSlot.Itemstack?.Collectible?.Attributes?["damagePercent"].AsFloat(0) ?? 0f);
        damage += weaponSlot.Itemstack?.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0;
        damage *= byEntity.Stats.GetBlended("rangedWeaponsDamage");

        return damage;
    }
    public virtual float GetProjectileVelocity(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        return Stats == null ? 0 : (Stats.ProjectileVelocity + (ammoSlot.Itemstack?.ItemAttributes?["velocityModifier"].AsFloat(0f) ?? 0f)) * byEntity.Stats.GetBlended("bowDrawingStrength");
    }
    public virtual float GetProjectileSpread(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        return Stats == null ? 0 : Stats.ProjectileSpread + (ammoSlot.Itemstack?.ItemAttributes?["spreadModifier"].AsFloat(0f) ?? 0f);
    }
    public virtual float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0f;
    public virtual float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0.1f;
    public virtual int GetProjectileDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0;
    public virtual EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
    {
        throw new NotImplementedException($"[Bullseye] Ranged weapon CollectibleBehavior of {collObj.Code} has no implementation for GetProjectileEntityType()!");
    }
    public virtual int GetWeaponDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0;

    public void Shoot(ItemSlot weaponSlot, EntityAgent byEntity, Vec3d targetVec)
    {
        byEntity.Attributes.SetInt("bullseyeAiming", 0);

        ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, weaponSlot);
        if (ammoSlot == null) return;

        EntityProperties projectileEntityType = GetProjectileEntityType(byEntity, weaponSlot, ammoSlot);
        if (projectileEntityType == null) return;

        float damage = GetProjectileDamage(byEntity, weaponSlot, ammoSlot);
        float speed = GetProjectileVelocity(byEntity, weaponSlot, ammoSlot);
        float spread = GetProjectileSpread(byEntity, weaponSlot, ammoSlot);
        float dropChance = GetProjectileDropChance(byEntity, weaponSlot, ammoSlot);
        float weight = GetProjectileWeight(byEntity, weaponSlot, ammoSlot);

        int ammoDurabilityCost = GetProjectileDurabilityCost(byEntity, weaponSlot, ammoSlot);
        int weaponDurabilityCost = GetWeaponDurabilityCost(byEntity, weaponSlot, ammoSlot);

        // If we need to damage the projectile by more than 1 durability per shot, do it here, but leave at least 1 durability
        ammoDurabilityCost = DamageProjectile(byEntity, weaponSlot, ammoSlot, ammoDurabilityCost);

        Vec3d velocity = GetVelocityVector(byEntity, targetVec, speed, spread);

        ItemStack ammoStack = ammoSlot.TakeOut(1);
        ammoSlot.MarkDirty();

        Entity projectileEntity = CreateProjectileEntity(byEntity, projectileEntityType, ammoStack, damage, dropChance, weight, ammoDurabilityCost);
        if (projectileEntity == null)
        {
            Api.Logger.Error($"[Bullseye] Ranged weapon {collObj.Code} tried to shoot, but failed to create the projectile entity!");
            return;
        }

        // Used in vanilla spears but feels awful, might redo later with proper offset to the right
        //projectileEntity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0));
        projectileEntity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
        projectileEntity.ServerPos.Motion.Set(velocity);

        projectileEntity.Pos.SetFrom(projectileEntity.ServerPos);
        projectileEntity.World = byEntity.World;

        FinalizeProjectileEntity(projectileEntity, byEntity);

        byEntity.World.SpawnEntity(projectileEntity);

        Synchronizer?.StartEntityCooldown(byEntity.EntityId);

        OnShot(weaponSlot, projectileEntity, byEntity);

        if (weaponDurabilityCost > 0)
        {
            weaponSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, weaponSlot, weaponDurabilityCost);
        }
    }
    public static Vec3d Vec3GetPerpendicular(Vec3d original)
    {
        int vecHighest = Math.Abs(original.X) > Math.Abs(original.Y) ? 0 : 1;
        vecHighest = Math.Abs(original[vecHighest]) > Math.Abs(original.Z) ? vecHighest : 2;

        int vecLowest = Math.Abs(original.X) < Math.Abs(original.Y) ? 0 : 1;
        vecLowest = Math.Abs(original[vecLowest]) < Math.Abs(original.Z) ? vecLowest : 2;

        int vecMiddle = Math.Abs(original.X) < Math.Abs(original[vecHighest]) ? 0 : -1;
        vecMiddle = vecMiddle < 0
                    || (Math.Abs(original.Y) > Math.Abs(original[vecMiddle]) && Math.Abs(original.Y) < Math.Abs(original[vecHighest])) ? 1 : vecMiddle;
        vecMiddle = (Math.Abs(original.Z) > Math.Abs(original[vecMiddle]) && Math.Abs(original.Z) < Math.Abs(original[vecHighest])) ? 2 : vecMiddle;

        Vec3d perp = new();
        perp[vecHighest] = original[vecMiddle];
        perp[vecMiddle] = -original[vecHighest];
        return perp.Normalize();
    }


    protected ICoreAPI? Api;
    protected ConfigSystem? Config;
    protected WeaponStats? Stats;
    protected Aiming.ClientAiming? Aiming;
    protected BullseyeModSystem? System;
    protected RangedWeapon.Synchronizer? Synchronizer => Api?.Side == EnumAppSide.Client ? SynchronizerClient : SynchronizerServer;
    protected RangedWeapon.SynchronizerClient? SynchronizerClient;
    protected RangedWeapon.SynchronizerServer? SynchronizerServer;

    protected LoadedTexture? AimTexPartCharge;
    protected LoadedTexture? AimTexFullCharge;
    protected LoadedTexture? AimTexBlocked;

    protected float GetEntityChargeTime(Entity entity)
    {
        return Stats == null ? 0 : Stats.ChargeTime / entity.Stats.GetBlended("rangedWeaponsSpeed");
    }
    protected virtual void PrepareHeldInteractionHelp()
    {
        if (collObj.Attributes["interactionLangCode"].AsString() != null)
        {
            string interactionsKey = $"{collObj.Attributes["interactionCollectibleCode"].AsString("")}RangedInteractions";

            _interactions = ObjectCacheUtil.GetOrCreate(Api, interactionsKey, () =>
            {
                List<ItemStack> stacks = null;

                if (collObj.Attributes["interactionCollectibleCode"].AsString() != null)
                {
                    stacks = new List<ItemStack>();

                    foreach (CollectibleObject obj in Api.World.Collectibles)
                    {
                        if (obj.Code.Path.StartsWith(collObj.Attributes["interactionCollectibleCode"].AsString()))
                        {
                            stacks.Add(new ItemStack(obj));
                        }
                    }
                }

                return new WorldInteraction[]
                {
                    new()
                    {
                        ActionLangCode = collObj.Attributes["interactionLangCode"].AsString(),
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks?.ToArray()
                    }
                };
            });
        }
    }
    protected virtual Entity CreateProjectileEntity(EntityAgent byEntity, EntityProperties type, ItemStack ammoStack, float damage, float dropChance, float weight, int ammoDurabilityCost)
    {
        /* 
			/ Aaaaaa why don't EntityThrownStone and EntityThrownBeenade inherit from EntityProjectile
			/ Tyron pls
			/
			/ Anyways, we check for all vanilla entity types here just in case some mod tries to make beenades shootable from the sling, or something like that
			*/
        Entity projectileEntity = byEntity.World.ClassRegistry.CreateEntity(type);

        if (projectileEntity is EntityProjectile entityProjectile)
        {
            // Unlike other projectiles, EntityProjectile applies the class bonus damage itself, so we need to reduce it here
            damage /= byEntity.Stats.GetBlended("rangedWeaponsDamage");

            entityProjectile.FiredBy = byEntity;
            entityProjectile.Damage = damage;
            entityProjectile.ProjectileStack = ammoStack;
            entityProjectile.DropOnImpactChance = dropChance;
            entityProjectile.DamageStackOnImpact = ammoDurabilityCost > 0;
            entityProjectile.Weight = weight;
        }
        else if (projectileEntity is EntityThrownStone entityThrownStone)
        {
            entityThrownStone.FiredBy = byEntity;
            entityThrownStone.Damage = damage;
            entityThrownStone.ProjectileStack = ammoStack;
        }
        else if (projectileEntity is EntityThrownBeenade entityThrownBeenade)
        {
            entityThrownBeenade.FiredBy = byEntity;
            entityThrownBeenade.Damage = damage;
            entityThrownBeenade.ProjectileStack = ammoStack;
        }

        return projectileEntity;
    }
    protected virtual Entity FinalizeProjectileEntity(Entity projectileEntity, EntityAgent byEntity)
    {
        if (projectileEntity is EntityProjectile entityProjectile)
        {
            entityProjectile.SetRotation();

#if DEBUG
				if (byEntity.World.Side == EnumAppSide.Server && byEntity is EntityPlayer entityPlayer)
				{
					api.ModLoader.GetModSystem<BullseyeSystemDebug>().SetFollowArrow(entityProjectile, entityPlayer);
				}
#endif
        }

        return projectileEntity;
    }
    protected int DamageProjectile(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot, int ammoDurabilityCost)
    {
        if (GetProjectileDurabilityCost(byEntity, weaponSlot, ammoSlot) > 1)
        {
            int durability = weaponSlot.Itemstack.Attributes.GetInt("durability", collObj.Durability);

            ammoDurabilityCost = ammoDurabilityCost >= durability ? durability : ammoDurabilityCost;

            weaponSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, weaponSlot, ammoDurabilityCost - 1);
        }

        return ammoDurabilityCost;
    }
    protected Vec3d GetVelocityVector(EntityAgent byEntity, Vec3d targetVec, float projectileSpeed, float spread)
    {
        // Might as well reuse these attributes for now
        double spreadAngle = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1);
        double spreadMagnitude = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1);

        // Code for zeroing and random spread - took some effort to get it right at all angles, including straight up
        // Zeroing
        Vec3d groundVec = new Vec3d(GameMath.Cos(byEntity.SidedPos.Yaw), 0, GameMath.Sin(targetVec.Z)).Normalize();
        Vec3d up = new(0, 1, 0);

        Vec3d horizontalAxis = groundVec.Cross(up);

        double[] matrix = Mat4d.Create();
        Mat4d.Rotate(matrix, matrix, Stats?.ZeroingAngle ?? 0 * GameMath.DEG2RAD, new double[] { horizontalAxis.X, horizontalAxis.Y, horizontalAxis.Z });
        double[] matrixVec = new double[] { targetVec.X, targetVec.Y, targetVec.Z, 0 };
        matrixVec = Mat4d.MulWithVec4(matrix, matrixVec);

        Vec3d zeroedTargetVec = new(matrixVec[0], matrixVec[1], matrixVec[2]);

        // Random spread - uses an older, less elegant method, but I'm not touching this anymore :p
        Vec3d perp = Vec3GetPerpendicular(zeroedTargetVec);
        Vec3d perp2 = zeroedTargetVec.Cross(perp);

        double angle = spreadAngle * (GameMath.PI * 2f);
        double offsetAngle = spreadMagnitude * spread * GameMath.DEG2RAD;

        double magnitude = GameMath.Tan(offsetAngle);

        Vec3d deviation = magnitude * perp * GameMath.Cos(angle) + magnitude * perp2 * GameMath.Sin(angle);
        Vec3d newAngle = (zeroedTargetVec + deviation) * (zeroedTargetVec.Length() / (zeroedTargetVec.Length() + deviation.Length()));

        Vec3d velocity = newAngle * projectileSpeed * GlobalConstants.PhysicsFrameTime;

        // What the heck? Server's SidedPos.Motion is somehow twice that of client's!
        velocity += Api.Side == EnumAppSide.Client ? byEntity.SidedPos.Motion : byEntity.SidedPos.Motion / 2;

        if (byEntity.MountedOn is Entity mountedEntity)
        {
            velocity += Api.Side == EnumAppSide.Client ? mountedEntity.SidedPos.Motion : mountedEntity.SidedPos.Motion / 2;
        }

        return velocity;
    }
    protected float GetChargeNeeded(ICoreAPI api, EntityAgent entity)
    {
        // slightly longer charge on client, for safety in case of desync
        return api.Side == EnumAppSide.Server ? GetEntityChargeTime(entity) : GetEntityChargeTime(entity) + 0.1f;
    }
    protected virtual bool CanStartAiming(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey) return false;

        if (Synchronizer?.HasEntityCooldownPassed(byEntity.EntityId, Stats?.CooldownTime ?? 0) == false)
        {
            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
            return false;
        }

        return true;
    }



    private WorldInteraction[] _interactions = Array.Empty<WorldInteraction>();

    private void ServerHandleFire(string eventName, ref EnumHandling handling, IAttribute data)
    {
        TreeAttribute? tree = data as TreeAttribute;
        int itemId = tree?.GetInt("itemId") ?? 0;

        if (itemId == collObj.Id && SynchronizerServer != null && tree != null)
        {
            long entityId = tree.GetLong("entityId");

            ItemSlot? itemSlot = SynchronizerServer.GetLastEntityRangedItemSlot(entityId);

            if (Api?.World.GetEntityById(entityId) is EntityAgent byEntity && SynchronizerServer.GetEntityChargeStart(entityId) + GetEntityChargeTime(byEntity) < Api.World.ElapsedMilliseconds / 1000f && byEntity.Alive && itemSlot != null)
            {
                Vec3d targetVec = new(tree.GetDouble("aimX"), tree.GetDouble("aimY"), tree.GetDouble("aimZ"));

                Shoot(itemSlot, byEntity, targetVec);

                handling = EnumHandling.PreventSubsequent;
            }
        }
    }
}