﻿using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using System.Globalization;

namespace Bullseye
{
	[Obsolete("Use BullseyeCollectibleBehaviorRangedWeapon instead")]
	public class BullseyeItemRangedWeapon : Item
	{
		protected BullseyeSystemClientAiming CoreClientSystem {get; private set;}
		protected BullseyeSystemCoreServer CoreServerSystem {get; private set;}
		protected BullseyeSystemConfig ConfigSystem {get; private set;}
		protected BullseyeSystemRangedWeapon RangedWeaponSystem {get; private set;}
		protected BullseyeRangedWeaponStats WeaponStats {get; private set;}

		protected ModelTransform DefaultFpHandTransform {get; private set;}

		protected LoadedTexture AimTexPartCharge;
		protected LoadedTexture AimTexFullCharge;
		protected LoadedTexture AimTexBlocked;

		private WorldInteraction[] interactions = Array.Empty<WorldInteraction>();

		public string AmmoType => WeaponStats.ammoType;

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);

			ConfigSystem = api.ModLoader.GetModSystem<BullseyeSystemConfig>();
			RangedWeaponSystem = api.ModLoader.GetModSystem<BullseyeSystemRangedWeapon>();

			DefaultFpHandTransform = FpHandTransform.Clone();

			WeaponStats = Attributes.KeyExists("bullseyeWeaponStats") ? Attributes?["bullseyeWeaponStats"].AsObject<BullseyeRangedWeaponStats>() : new BullseyeRangedWeaponStats();

			if (api.Side == EnumAppSide.Server)
			{
				CoreServerSystem = api.ModLoader.GetModSystem<BullseyeSystemCoreServer>();

				api.Event.RegisterEventBusListener(ServerHandleFire, 0.5, "bullseyeRangedWeaponFire");
			}
			else
			{
				CoreClientSystem = api.ModLoader.GetModSystem<BullseyeSystemClientAiming>();

				ICoreClientAPI capi = api as ICoreClientAPI;

				PrepareHeldInteractionHelp();

				AimTexPartCharge = new LoadedTexture(capi);
				AimTexFullCharge = new LoadedTexture(capi);
				AimTexBlocked = new LoadedTexture(capi);

				if (WeaponStats.aimTexPartChargePath != null) capi.Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexPartChargePath), ref AimTexPartCharge);
				if (WeaponStats.aimTexFullChargePath != null) capi.Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexFullChargePath), ref AimTexFullCharge);
				if (WeaponStats.aimTexBlockedPath != null) capi.Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexBlockedPath), ref AimTexBlocked);
			}
		}

		public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
		{
			return null;
		}

		// Keeping this as backup in case OnBeforeRender has some hidden flaw
		/*public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
		{
			if (byEntity.World is IClientWorldAccessor)
			{
				float transformFraction;

				if (!RangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, WeaponStats.cooldownTime))
				{
					float cooldownRemaining = WeaponStats.cooldownTime - RangedWeaponSystem.GetEntityCooldownTime(byEntity.EntityId);
					float transformTime = 0.25f;

					transformFraction = WeaponStats.weaponType != BullseyeRangedWeaponType.Throw ? 
						GameMath.Clamp((WeaponStats.cooldownTime - cooldownRemaining) / transformTime, 0f, 1f) : 1f;
					transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);
				}
				else
				{
					transformFraction = 0;
				}

				FpHandTransform.Translation.Y = defaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);
			}
		}*/

		public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
		{
			base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

			if (target == EnumItemRenderTarget.HandFp)
			{
				float transformFraction;

				if (!RangedWeaponSystem.HasEntityCooldownPassed(capi.World.Player.Entity.EntityId, WeaponStats.cooldownTime))
				{
					float cooldownRemaining = WeaponStats.cooldownTime - RangedWeaponSystem.GetEntityCooldownTime(capi.World.Player.Entity.EntityId);
					float transformTime = 0.25f;

					transformFraction = WeaponStats.weaponType != BullseyeRangedWeaponType.Throw ? 
						GameMath.Clamp((WeaponStats.cooldownTime - cooldownRemaining) / transformTime, 0f, 1f) : 1f;
					transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);
				}
				else
				{
					transformFraction = 0;
				}

				renderinfo.Transform.Translation.Y = DefaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);

				if (CoreClientSystem.Aiming)
				{
					SetAimTransform(capi, renderinfo.Transform);
				}
				else
				{
					renderinfo.Transform.Rotation.Set(DefaultFpHandTransform.Rotation);
				}
			}
		}

		protected virtual void SetAimTransform(ICoreClientAPI capi, ModelTransform transform)
		{
			Vec2f currentAim = CoreClientSystem.GetCurrentAim();

			transform.Rotation.X = DefaultFpHandTransform.Rotation.X - (currentAim.Y / 15f); 
			transform.Rotation.Y = DefaultFpHandTransform.Rotation.Y - (currentAim.X / 15f);
		}

		/*public virtual List<ItemStack> GetAvailableAmmoTypes(ItemSlot slot, IClientPlayer forPlayer) 
		{
			return null;
		}*/

		public virtual List<ItemStack> GetAvailableAmmoTypes(ItemSlot slot, IClientPlayer forPlayer)
		{
			if (AmmoType == null)
			{
				return null;
			}

			List<ItemStack> ammoTypes = new List<ItemStack>();

			forPlayer.Entity.WalkInventory((invslot) =>
			{
				if (invslot is ItemSlotCreative) return true;

				if (invslot.Itemstack != null && AmmoType == invslot.Itemstack.ItemAttributes?["ammoType"].AsString())
				{
					ItemStack ammoStack = ammoTypes.Find(itemstack => itemstack.Id == invslot.Itemstack.Id);

					if (ammoStack == null)
					{
						ammoStack = new ItemStack(api.World.GetItem(invslot.Itemstack.Id));
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

			ammoTypes.Sort((ItemStack X, ItemStack Y) => {
				float xDamage = X.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;
				float yDamage = Y.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;

				// Sort by damage, or by name if damage is equal
				//return xDamage > yDamage ? 1 : (xDamage < yDamage ? -1 : String.Compare(X.GetName(), Y.GetName())); 
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
			resultItemstack?.ResolveBlockOrItem(api.World);

			return resultItemstack;
		}

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
		{
			base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

			if (!RangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, WeaponStats.cooldownTime))
			{
				handling = EnumHandHandling.NotHandled;
				return;
			}

			ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, slot, true);
			if (ammoSlot == null) return;

			EntityProperties projectileEntity = GetProjectileEntityType(byEntity, slot, ammoSlot);
			if (projectileEntity == null) return;

			if (byEntity.World is IClientWorldAccessor)
			{
				CoreClientSystem.SetRangedWeaponStats(WeaponStats);
				CoreClientSystem.SetReticleTextures(AimTexPartCharge, AimTexFullCharge, AimTexBlocked);
			}

			RangedWeaponSystem.SetLastEntityRangedChargeData(byEntity.EntityId, slot);

			byEntity.GetBehavior<BullseyeEntityBehaviorAimingAccuracy>().SetRangedWeaponStats(WeaponStats);

			// Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
			byEntity.Attributes.SetInt("bullseyeAiming", 1);
			byEntity.Attributes.SetInt("aimingCancel", 0);

			if (!WeaponStats.allowSprint) 
			{
				byEntity.Controls.Sprint = false;
				byEntity.ServerControls.Sprint = false;
			}

			OnAimingStart(slot, byEntity);
			handling = EnumHandHandling.PreventDefault;
		}

		public virtual void OnAimingStart(ItemSlot slot, EntityAgent byEntity) {}

		public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
		{
			base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);

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
					bool showPartCharged = secondsUsed < WeaponStats.accuracyStartTime / byEntity.Stats.GetBlended("rangedWeaponsSpeed") + WeaponStats.aimFullChargeLeeway;
					showPartCharged = showPartCharged || secondsUsed > WeaponStats.accuracyOvertimeStart + WeaponStats.accuracyStartTime && WeaponStats.accuracyOvertime > 0;

					CoreClientSystem.WeaponReadiness = showBlocked ? BullseyeEnumWeaponReadiness.Blocked : 
														showPartCharged ? BullseyeEnumWeaponReadiness.PartCharge : BullseyeEnumWeaponReadiness.FullCharge;
				}
			}
			
			return true;
		}

		public virtual void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity) {}

		public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
		{
			base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);

			byEntity.Attributes.SetInt("bullseyeAiming", 0);

			if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
			{
				byEntity.Attributes.SetInt("aimingCancel", 1);
			}

			OnAimingCancel(secondsUsed, slot, byEntity, cancelReason);

			return true;
		}

		public virtual void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) {}

		public virtual ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot, bool isStartCheck = false)
		{
			if (AmmoType == null || byEntity == null || weaponSlot.Itemstack == null) return null;

			ItemSlot ammoSlot = null;

			ItemStack ammoType = GetEntitySelectedAmmoType(byEntity);

			byEntity.WalkInventory((invslot) =>
			{
				if (invslot == null || invslot is ItemSlotCreative) return true;

				if (invslot.Itemstack != null && AmmoType == invslot.Itemstack.ItemAttributes?["ammoType"].AsString())
				{
					// If we found the selected ammo type or no ammo type is specifically selected, return the first one we find
					if (ammoType == null || invslot.Itemstack.Equals(api.World, ammoType, GlobalConstants.IgnoredStackAttributes))
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
			return 0f;
		}

		public virtual float GetProjectileSpeed(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return WeaponStats.projectileVelocity + (ammoSlot.Itemstack?.Collectible.Attributes?["speedModifier"].AsFloat(0f) ?? 0f);
		}

		public virtual float GetProjectileSpread(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return WeaponStats.projectileSpread + (ammoSlot.Itemstack?.Collectible.Attributes?["spreadModifier"].AsFloat(0f) ?? 0f);
		}

		public virtual float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return 1.1f;
		}

		public virtual float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return 0.1f;
		}

		public virtual int GetProjectileDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return 0;
		}

		public virtual EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			throw new NotImplementedException(String.Format("Item {0} does not have implementation for GetProjectileEntityType()!", Code));
		}

		public virtual int GetWeaponDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return 0;
		}

		public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
		{
			base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);

			if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
			byEntity.Attributes.SetInt("bullseyeAiming", 0);

			EntityPlayer entityPlayer = byEntity as EntityPlayer;

			if (!byEntity.Alive || secondsUsed < GetChargeNeeded(api, byEntity))
			{
				OnAimingCancel(secondsUsed, slot, byEntity, !byEntity.Alive ? EnumItemUseCancelReason.Death : EnumItemUseCancelReason.ReleasedMouse);
				return;
			}

			if (api.Side == EnumAppSide.Server) 
			{
				// Just to make sure animations etc. get stopped if a shot looks legit on the server but was stopped on the client
				api.Event.RegisterCallback((ms) => 
				{
					if (byEntity.Attributes.GetInt("bullseyeAiming") == 0)
				 	{ 
						OnAimingCancel(secondsUsed, slot, byEntity, !byEntity.Alive ? EnumItemUseCancelReason.Death : EnumItemUseCancelReason.ReleasedMouse);
					}
				}, 500);
			}
			else
			{
				Vec3d targetVec = CoreClientSystem.TargetVec;

				Shoot(slot, byEntity, targetVec);

				RangedWeaponSystem.SendRangedWeaponFirePacket(Id, targetVec);
			}
		}

		protected float GetChargeNeeded(ICoreAPI api, EntityAgent entity)
		{
			// slightly longer charge on client, for safety in case of desync
			return api.Side == EnumAppSide.Server ? GetEntityChargeTime(entity) : GetEntityChargeTime(entity) + 0.1f;
		}

		public void Shoot(ItemSlot slot, EntityAgent byEntity, Vec3d targetVec)
		{
			byEntity.Attributes.SetInt("bullseyeAiming", 0);

			ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, slot);
			if (ammoSlot == null) return;

			float damage = GetProjectileDamage(byEntity, slot, ammoSlot);
			float speed = GetProjectileSpeed(byEntity, slot, ammoSlot);
			float spread = GetProjectileSpread(byEntity, slot, ammoSlot);
			float dropChance = GetProjectileDropChance(byEntity, slot, ammoSlot);
			float weight = GetProjectileWeight(byEntity, slot, ammoSlot);
			int ammoDurabilityCost = GetProjectileDurabilityCost(byEntity, slot, ammoSlot);

			EntityProperties type = GetProjectileEntityType(byEntity, slot, ammoSlot);
			if (type == null) return;

			int weaponDurabilityCost = GetWeaponDurabilityCost(byEntity, slot, ammoSlot);

			// If we need to damage the projectile by more than 1 durability per shot, do it here, but leave at least 1 durability
			if (GetProjectileDurabilityCost(byEntity, slot, ammoSlot) > 1)
			{
				int durability = slot.Itemstack.Attributes.GetInt("durability", Durability);

				ammoDurabilityCost--;
				ammoDurabilityCost = ammoDurabilityCost >= durability ? durability - 1 : ammoDurabilityCost;

				slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, ammoDurabilityCost);
			}

			ItemStack stack = ammoSlot.TakeOut(1);
			ammoSlot.MarkDirty();

			// Aaaaaa why doesn't EntityThrownStone inherit from EntityProjectile
			// Tyron pls
			//EntityProjectile entityProjectile = byEntity.World.ClassRegistry.CreateEntity(type) as EntityProjectile;
			Entity projectileEntity = byEntity.World.ClassRegistry.CreateEntity(type);

			EntityProjectile entityProjectile = projectileEntity as EntityProjectile;

			if (entityProjectile != null)
			{
				entityProjectile.FiredBy = byEntity;
				entityProjectile.Damage = damage;
				entityProjectile.ProjectileStack = stack;
				entityProjectile.DropOnImpactChance = dropChance;
				entityProjectile.DamageStackOnImpact = ammoDurabilityCost > 0;
				entityProjectile.Weight = weight;
			}
			else if (projectileEntity is EntityThrownStone entityThrownStone)
			{
				entityThrownStone.FiredBy = byEntity;
				entityThrownStone.Damage = damage;
				entityThrownStone.ProjectileStack = stack;
			}

			// Might as well reuse these attributes for now
			double spreadAngle = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1);
			double spreadMagnitude = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1);

			// Code for zeroing and random spread - took some effort to get it right at all angles, including straight up
			// Zeroing
			Vec3d groundVec = new Vec3d(GameMath.Cos(byEntity.SidedPos.Yaw), 0, GameMath.Sin(targetVec.Z)).Normalize();
			Vec3d up = new Vec3d(0, 1, 0);

			Vec3d horizAxis = groundVec.Cross(up);

			double[] matrix = Mat4d.Create();
			Mat4d.Rotate(matrix, matrix, WeaponStats.zeroingAngle * GameMath.DEG2RAD, new double[] {horizAxis.X, horizAxis.Y, horizAxis.Z});
			double[] matrixVec = new double[] {targetVec.X, targetVec.Y, targetVec.Z, 0};
			matrixVec = Mat4d.MulWithVec4(matrix, matrixVec);

			Vec3d zeroedTargetVec = new Vec3d(matrixVec[0], matrixVec[1], matrixVec[2]);

			// Random spread - uses an older, less elegant method, but I'm not touching this anymore :p
			Vec3d perp = BullseyeMathHelper.Vec3GetPerpendicular(zeroedTargetVec);
			Vec3d perp2 = zeroedTargetVec.Cross(perp);

			double angle = spreadAngle * (GameMath.PI * 2f);
			double offsetAngle = spreadMagnitude * spread * GameMath.DEG2RAD;

			double magnitude = GameMath.Tan(offsetAngle);

			Vec3d deviation = magnitude * perp * GameMath.Cos(angle) + magnitude * perp2 * GameMath.Sin(angle);
			Vec3d newAngle = (zeroedTargetVec + deviation) * (zeroedTargetVec.Length() / (zeroedTargetVec.Length() + deviation.Length()));

			Vec3d velocity = newAngle * byEntity.Stats.GetBlended("bowDrawingStrength") * (speed * GlobalConstants.PhysicsFrameTime);

			// What the heck? Server's SidedPos.Motion is somehow twice that of client's!
			velocity += api.Side == EnumAppSide.Client ? byEntity.SidedPos.Motion : byEntity.SidedPos.Motion / 2;

			if (byEntity.MountedOn is Entity mountedEntity)
			{
				velocity += api.Side == EnumAppSide.Client ? mountedEntity.SidedPos.Motion : mountedEntity.SidedPos.Motion / 2;
			}

			// Used in vanilla spears but feels awful, might redo later with zeroing and proper offset to the right
			//projectileEntity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0));
			projectileEntity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
			projectileEntity.ServerPos.Motion.Set(velocity);

			projectileEntity.Pos.SetFrom(projectileEntity.ServerPos);
			projectileEntity.World = byEntity.World;

			if (entityProjectile != null)
			{
				entityProjectile.SetRotation();

				#if DEBUG
				if (byEntity.World.Side == EnumAppSide.Server && byEntity is EntityPlayer entityPlayer)
				{
					api.ModLoader.GetModSystem<BullseyeSystemDebug>().SetFollowArrow(entityProjectile, entityPlayer);
				}
				#endif
			}
			
			byEntity.World.SpawnEntity(projectileEntity);

			RangedWeaponSystem.StartEntityCooldown(byEntity.EntityId);

			OnShot(slot, projectileEntity, byEntity);

			if (weaponDurabilityCost > 0)
			{
				slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, weaponDurabilityCost);
			}
		}

		public virtual void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity) {}

		private void ServerHandleFire(string eventName, ref EnumHandling handling, IAttribute data)
		{
			TreeAttribute tree = data as TreeAttribute;
			int itemId = tree.GetInt("itemId");

			if (itemId == Id)
			{
				long entityId = tree.GetLong("entityId");

				ItemSlot itemSlot = RangedWeaponSystem.GetLastEntityRangedItemSlot(entityId);
				EntityAgent byEntity =  api.World.GetEntityById(entityId) as EntityAgent;

				if (RangedWeaponSystem.GetEntityChargeStart(entityId) + GetEntityChargeTime(byEntity) < api.World.ElapsedMilliseconds / 1000f && byEntity.Alive && itemSlot != null)
				{
					Vec3d targetVec = new Vec3d(tree.GetDouble("aimX"), tree.GetDouble("aimY"), tree.GetDouble("aimZ"));

					Shoot(itemSlot, byEntity, targetVec);
					
					handling = EnumHandling.PreventSubsequent;
				}
			}
		}

		public float GetEntityChargeTime(Entity entity)
		{
			return WeaponStats.chargeTime / entity.Stats.GetBlended("rangedWeaponsSpeed");
		}

		public virtual void PrepareHeldInteractionHelp()
		{
			if (Attributes["interactionLangCode"].AsString() != null)
			{
				string interactionsKey = $"{Attributes["interactionCollectibleCode"].AsString("")}RangedInteractions";

				interactions = ObjectCacheUtil.GetOrCreate(api, interactionsKey, () =>
				{
					List<ItemStack> stacks = null;

					if (Attributes["interactionCollectibleCode"].AsString() != null)
					{
						stacks = new List<ItemStack>();

						foreach (CollectibleObject obj in api.World.Collectibles)
						{
							if (obj.Code.Path.StartsWith(Attributes["interactionCollectibleCode"].AsString()))
							{
								stacks.Add(new ItemStack(obj));
							}
						}
					}

					return new WorldInteraction[]
					{
						new WorldInteraction()
						{
							ActionLangCode = Attributes["interactionLangCode"].AsString(),
							MouseButton = EnumMouseButton.Right,
							Itemstacks = stacks?.ToArray()
						}
					};
				});
			}
		}

		public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
		{
			return interactions?.Append(base.GetHeldInteractionHelp(inSlot));
		}

		public override void OnUnloaded(ICoreAPI api)
		{
			base.OnUnloaded(api);

			AimTexFullCharge?.Dispose();
			AimTexPartCharge?.Dispose();
			AimTexBlocked?.Dispose();
		}
	}
}