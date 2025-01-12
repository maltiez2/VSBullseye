﻿using System;
using System.Text;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Bullseye
{
	[Obsolete("Use BullseyeCollectibleBehaviorAmmunition instead")]
	public class BullseyeItemAmmunition : Item
	{
		public virtual float GetDamage(ItemSlot inSlot, IWorldAccessor world)
		{
			return inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
		}

		public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

			// ItemSlotAmmo displays data with relevant weapon and class bonuses applied, so we don't have to
			if (inSlot is BullseyeItemSlotAmmo) return;

			if (inSlot.Itemstack.Collectible.Attributes == null) return;

			float damage = GetDamage(inSlot, world);
			if (damage != 0) dsc.AppendLine(damage + Lang.Get("game:piercing-damage"));

			float averageLifetimeDamage = 0f;
			float breakChance = 0f;

			if (inSlot.Itemstack.Collectible.Attributes.KeyExists("averageLifetimeDamage"))
			{
				averageLifetimeDamage = inSlot.Itemstack.ItemAttributes["averageLifetimeDamage"].AsFloat();
				breakChance = damage / averageLifetimeDamage;
			}
			else
			{
				breakChance = inSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0);
			}

			if (breakChance == 0f) return;
			if (breakChance >= 1f)
			{
				dsc.AppendLine(Lang.Get("bullseye-continued:projectile-always-breaks"));
				return;
			}

			dsc.AppendLine(Lang.Get("bullseye-continued:projectile-break-chance", breakChance * 100f));

			// Disabled until I can find a way to make this not confusing for players
			/*if (averageLifetimeDamage > 0)
			{
				dsc.AppendLine($"({Lang.Get("bullseye-continued:lifetime-projectile-damage", averageLifetimeDamage)})");
			}*/
		}
	}
}
