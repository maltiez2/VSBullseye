using Bullseye.RangedWeapon;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Bullseye.Ammo;

public sealed class AmmoSelectDialog : GuiDialog
{
    public override string ToggleKeyCombinationCode => "bullseye.ammotypeselect";
    public override bool PrefersUngrabbedMouse => false;

    public AmmoSelectDialog(ICoreClientAPI api) : base(api)
    {
        _inventory = new AmmoInventory(api);
        _clientApi = api;
    }

    public override bool TryOpen()
    {
        ItemSlot? activeHotbarSlot = _clientApi.World.Player.InventoryManager?.ActiveHotbarSlot;
        RangedWeaponBehavior? behaviorRangedWeapon = activeHotbarSlot?.Itemstack?.Collectible.GetCollectibleBehavior<RangedWeaponBehavior>(true);

        if (activeHotbarSlot == null || behaviorRangedWeapon?.AmmoType == null) return false;

        List<ItemStack> ammoStacks = behaviorRangedWeapon.GetAvailableAmmoTypes(activeHotbarSlot, _clientApi.World.Player);

        if (ammoStacks == null || ammoStacks.Count == 0) return false;

        _inventory.AmmoCategory = behaviorRangedWeapon.AmmoType;
        _inventory.SetAmmoStacks(ammoStacks);
        _inventory.SetSelectedAmmoItemStack(behaviorRangedWeapon.GetEntitySelectedAmmoType(_clientApi.World.Player.Entity));
        _inventory.PlayerEntity = capi.World.Player.Entity;

        return base.TryOpen();
    }
    public override void OnGuiOpened()
    {
        ClearComposers();

        int ammoStackCount = _inventory.Count;

        int maxItemsPerLine = 8;
        int widestLineItems = GameMath.Min(ammoStackCount, maxItemsPerLine);

        double unscaledSlotPaddedSize = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
        double lineWidth = widestLineItems * unscaledSlotPaddedSize;
        int lineCount = 1 + (ammoStackCount - (ammoStackCount % maxItemsPerLine)) / maxItemsPerLine;

        ElementBounds elementBounds = ElementBounds.Fixed(0.0, 0.0, lineWidth, lineCount * unscaledSlotPaddedSize);
        SingleComposer = capi.Gui.CreateCompo("ammotypeselect", ElementStdBounds.AutosizedMainDialog).AddShadedDialogBG(ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding / 2.0), withTitleBar: false).BeginChildElements();

        SingleComposer.AddItemSlotGrid(_inventory, null, 8, elementBounds, "inventoryAmmoSelectGrid");
        SingleComposer.Compose();
    }
    public override void Dispose() => capi = null;


    private readonly AmmoInventory _inventory;
    private readonly ICoreClientAPI _clientApi;
}
