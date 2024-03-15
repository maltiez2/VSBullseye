using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Bullseye.Old;

public class BullseyeSystemRangedWeapon : ModSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BullseyeRangedWeaponFirePacket
    {
        public int itemId;
        public double aimX;
        public double aimY;
        public double aimZ;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BullseyeRangedWeaponAmmoSelectPacket
    {
        public string ammoCategory;
        public byte[] ammoItemStack;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BullseyeRangedWeaponAmmoSyncPacket
    {
        public byte[] selectedAmmoTree;
    }

    // Server
    private ICoreServerAPI sapi;
    private IServerNetworkChannel serverNetworkChannel;

    private Dictionary<long, ItemSlot> lastRangedSlotByEntityId = new();

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        world = api.World;

        serverNetworkChannel = api.Network.RegisterChannel("bullseyeitem")
        .RegisterMessageType<BullseyeRangedWeaponFirePacket>()
        .RegisterMessageType<BullseyeRangedWeaponAmmoSelectPacket>()
        .RegisterMessageType<BullseyeRangedWeaponAmmoSyncPacket>()
        .SetMessageHandler<BullseyeRangedWeaponFirePacket>(OnServerRangedWeaponFire)
        .SetMessageHandler<BullseyeRangedWeaponAmmoSelectPacket>(OnServerRangedWeaponAmmoSelect);

        sapi.Event.PlayerNowPlaying += Event_PlayerNowPlaying;
    }

    private void Event_PlayerNowPlaying(IServerPlayer byPlayer)
    {
        if (byPlayer.Entity?.Attributes?.GetTreeAttribute("bullseyeSelectedAmmo") is TreeAttribute treeAttribute)
        {
            serverNetworkChannel.SendPacket(new BullseyeRangedWeaponAmmoSyncPacket()
            {
                selectedAmmoTree = treeAttribute.ToBytes()
            }, byPlayer);
        }
    }

    public void OnServerRangedWeaponFire(IServerPlayer fromPlayer, BullseyeRangedWeaponFirePacket packet)
    {
        TreeAttribute tree = new();
        tree.SetLong("entityId", fromPlayer.Entity.EntityId);
        tree.SetInt("itemId", packet.itemId);
        tree.SetDouble("aimX", packet.aimX);
        tree.SetDouble("aimY", packet.aimY);
        tree.SetDouble("aimZ", packet.aimZ);

        sapi.Event.PushEvent("bullseyeRangedWeaponFire", tree);
    }

    public void OnServerRangedWeaponAmmoSelect(IServerPlayer fromPlayer, BullseyeRangedWeaponAmmoSelectPacket packet)
    {
        ItemStack ammoItemStack = new();

        using (MemoryStream ms = new(packet.ammoItemStack))
        {
            BinaryReader reader = new(ms);
            ammoItemStack.FromBytes(reader);
        }

        ammoItemStack.ResolveBlockOrItem(sapi.World);

        EntitySetAmmoType(fromPlayer.Entity, packet.ammoCategory, ammoItemStack);
    }

    // Client
    ICoreClientAPI capi;
    IClientNetworkChannel clientNetworkChannel;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        world = api.World;

        clientNetworkChannel = api.Network.RegisterChannel("bullseyeitem")
        .RegisterMessageType<BullseyeRangedWeaponFirePacket>()
        .RegisterMessageType<BullseyeRangedWeaponAmmoSelectPacket>()
        .RegisterMessageType<BullseyeRangedWeaponAmmoSyncPacket>()
        .SetMessageHandler<BullseyeRangedWeaponAmmoSyncPacket>(OnClientRangedWeaponAmmoSync);

        capi.Event.AfterActiveSlotChanged += (changeEventArgs) =>
        {
            if (changeEventArgs.ToSlot > capi.World.Player.InventoryManager?.GetHotbarInventory().Count) return;


        };
    }

    public void SendRangedWeaponFirePacket(int itemId, Vec3d targetVec)
    {
        clientNetworkChannel.SendPacket(new BullseyeRangedWeaponFirePacket()
        {
            itemId = itemId,
            aimX = targetVec.X,
            aimY = targetVec.Y,
            aimZ = targetVec.Z
        });
    }

    public void SendRangedWeaponAmmoSelectPacket(string ammoCategory, ItemStack ammoItemStack)
    {
        byte[] itemStackData;

        using (MemoryStream ms = new())
        {
            BinaryWriter writer = new(ms);
            ammoItemStack.ToBytes(writer);
            itemStackData = ms.ToArray();
        }

        clientNetworkChannel.SendPacket(new BullseyeRangedWeaponAmmoSelectPacket()
        {
            ammoCategory = ammoCategory,
            ammoItemStack = itemStackData
        });
    }

    public void OnClientRangedWeaponAmmoSync(BullseyeRangedWeaponAmmoSyncPacket packet)
    {
        capi.World.Player.Entity.Attributes.SetAttribute("bullseyeSelectedAmmo", TreeAttribute.CreateFromBytes(packet.selectedAmmoTree));
    }

    // Common
    private IWorldAccessor world;

    private Dictionary<long, long> cooldownByEntityId = new();
    // rangedChargeStartByEntityId is NOT synchronised between server and client! Each will have different values, and this is desirable because
    // client and server both report different world.ElapsedMilliseconds
    private Dictionary<long, long> rangedChargeStartByEntityId = new();

    public override void Start(ICoreAPI api)
    {
    }

    public void StartEntityCooldown(long entityId)
    {
        cooldownByEntityId[entityId] = world.ElapsedMilliseconds;
    }

    public float GetEntityCooldownTime(long entityId)
    {
        return cooldownByEntityId.TryGetValue(entityId, out long cooldownMilliseconds) ? (world.ElapsedMilliseconds - cooldownByEntityId[entityId]) / 1000f : 0;
    }

    public bool HasEntityCooldownPassed(long entityId, double cooldownTime)
    {
        return cooldownByEntityId.TryGetValue(entityId, out long cooldownMilliseconds) ? world.ElapsedMilliseconds > cooldownByEntityId[entityId] + (cooldownTime * 1000) : true;
    }

    public void SetLastEntityRangedChargeData(long entityId, ItemSlot itemSlot)
    {
        lastRangedSlotByEntityId[entityId] = itemSlot;
        rangedChargeStartByEntityId[entityId] = world.ElapsedMilliseconds;
    }

    public ItemSlot GetLastEntityRangedItemSlot(long entityId)
    {
        return lastRangedSlotByEntityId.TryGetValue(entityId, out ItemSlot itemSlot) ? itemSlot : null;
    }

    public float GetEntityChargeStart(long entityId)
    {
        return rangedChargeStartByEntityId.TryGetValue(entityId, out long chargeStartMilliseconds) ? chargeStartMilliseconds / 1000f : 0;
    }

    public void EntitySetAmmoType(EntityAgent entity, string ammoCategory, ItemStack ammoItemStack)
    {
        ITreeAttribute treeAttribute = entity.Attributes.GetOrAddTreeAttribute("bullseyeSelectedAmmo");

        treeAttribute.SetItemstack(ammoCategory, ammoItemStack);
    }

    public override void Dispose()
    {
        sapi = null;
        serverNetworkChannel = null;

        capi = null;
        clientNetworkChannel = null;

        world = null;
    }
}