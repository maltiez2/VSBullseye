using Bullseye.RangedWeapon;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Bullseye.Ammo;

internal interface IAmmoInventory
{
    string? AmmoCategory { get; set; }
    EntityPlayer? PlayerEntity { get; set; }

    void SetAmmoStacks(List<ItemStack> ammoStacks);
    void SetSelectedAmmoItemStack(ItemStack ammoItemStack);
}

internal sealed class AmmoInventory : InventoryBase, IAmmoInventory
{
    public AmmoInventory(ICoreAPI api) : base("inventoryAmmoSelect-" + (_dummyId++), api)
    {
    }

    public string? AmmoCategory { get; set; }
    public EntityPlayer? PlayerEntity { get; set; }

    public void SetAmmoStacks(List<ItemStack> ammoStacks)
    {
        if (ammoStacks.Count < Count)
        {
            _slots.RemoveRange(ammoStacks.Count, Count - ammoStacks.Count);
        }

        for (int i = 0; i < ammoStacks.Count; i++)
        {
            this[i].Itemstack = ammoStacks[i];
        }
    }

    public void SetSelectedAmmoItemStack(ItemStack ammoItemStack)
    {
        foreach (ItemSlot itemSlot in this)
        {
            itemSlot.HexBackgroundColor = itemSlot.Itemstack.Equals(Api.World, ammoItemStack, GlobalConstants.IgnoredStackAttributes) ? "#ff8080" : null;
        }
    }

    public override object ActivateSlot(int slotId, ItemSlot mouseSlot, ref ItemStackMoveOperation op)
    {
        if (Api is ICoreClientAPI clientApi && PlayerEntity != null && AmmoCategory != null)
        {
            RangedWeapon.SynchronizerClient? synchronizer = Api.ModLoader.GetModSystem<BullseyeModSystem>().Synchronizer as SynchronizerClient;

            ItemSlot selectedSlot = this[slotId];

            SynchronizerClient.EntitySetAmmoType(PlayerEntity, AmmoCategory, selectedSlot.Itemstack);
            synchronizer?.SendRangedWeaponAmmoSelectPacket(AmmoCategory, selectedSlot.Itemstack);

            clientApi.Gui.OpenedGuis.Find((dialog) => dialog is AmmoSelectDialog)?.TryClose();
        }

        return InvNetworkUtil.GetActivateSlotPacket(slotId, op);
    }

    public override ItemSlot this[int slotId]
    {
        get
        {
            if (slotId < 0) throw new ArgumentOutOfRangeException(nameof(slotId));
            if (slotId >= Count)
            {
                for (int i = Count; i <= slotId; i++)
                {
                    _slots.Add(NewSlot(i));
                }
            }
            return _slots[slotId];
        }
        set
        {
            if (slotId < 0) throw new ArgumentOutOfRangeException(nameof(slotId));
            if (slotId >= Count)
            {
                for (int i = Count; i <= slotId; i++)
                {
                    _slots.Add(NewSlot(i));
                }
            }
            _slots[slotId] = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
    public override int Count => _slots.Count;
    public override void FromTreeAttributes(ITreeAttribute tree) => _slots = SlotsFromTreeAttributes(tree, null).ToList();
    public override void ToTreeAttributes(ITreeAttribute tree) => SlotsToTreeAttributes(_slots.ToArray(), tree);
    public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack) => 0;

    protected override ItemSlot NewSlot(int i) => new AmmoSlot(this);

    private static int _dummyId = 1;
    private List<ItemSlot> _slots = new();
}

