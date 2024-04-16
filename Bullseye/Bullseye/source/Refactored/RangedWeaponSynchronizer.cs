using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Bullseye.RangedWeapon;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal struct FirePacket
{
    public int ItemId { get; set; }
    public double AimX { get; set; }
    public double AimY { get; set; }
    public double AimZ { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal struct AmmoSelectPacket
{
    public string AmmoCategory { get; set; }
    public byte[] AmmoItemStack { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
internal struct AmmoSyncPacket
{
    public byte[] SelectedAmmoTree { get; set; }
}


internal abstract class Synchronizer
{
    public void StartEntityCooldown(long entityId)
    {
        CooldownByEntityId[entityId] = World.ElapsedMilliseconds;
    }
    public float GetEntityCooldownTime(long entityId)
    {
        return CooldownByEntityId.TryGetValue(entityId, out _) ? (World.ElapsedMilliseconds - CooldownByEntityId[entityId]) / 1000f : 0;
    }
    public bool HasEntityCooldownPassed(long entityId, double cooldownTime)
    {
        return !CooldownByEntityId.TryGetValue(entityId, out _) || World.ElapsedMilliseconds > CooldownByEntityId[entityId] + (cooldownTime * 1000);
    }
    public float GetEntityChargeStart(long entityId)
    {
        return RangedChargeStartByEntityId.TryGetValue(entityId, out long chargeStartMilliseconds) ? chargeStartMilliseconds / 1000f : 0;
    }
    public static void EntitySetAmmoType(EntityAgent entity, string ammoCategory, ItemStack ammoItemStack)
    {
        ITreeAttribute treeAttribute = entity.Attributes.GetOrAddTreeAttribute("bullseyeSelectedAmmo");

        treeAttribute.SetItemstack(ammoCategory, ammoItemStack);
    }

    protected Synchronizer(ICoreAPI api)
    {
        World = api.World;
    }

    protected IWorldAccessor World;
    protected Dictionary<long, long> CooldownByEntityId = new();
    /// <summary>
    /// RangedChargeStartByEntityId is NOT synchronized between server and client! Each will have different values.<br/>
    /// This is desirable because client and server both report different world.ElapsedMilliseconds.
    /// </summary>
    protected Dictionary<long, long> RangedChargeStartByEntityId = new();
}

internal sealed class SynchronizerServer : Synchronizer
{
    public SynchronizerServer(ICoreServerAPI api) : base(api)
    {
        _serverApi = api;
        _serverNetworkChannel = api.Network.RegisterChannel("bullseyeitem")
            .RegisterMessageType<FirePacket>()
            .RegisterMessageType<AmmoSelectPacket>()
            .RegisterMessageType<AmmoSyncPacket>()
            .SetMessageHandler<FirePacket>(OnServerRangedWeaponFire)
            .SetMessageHandler<AmmoSelectPacket>(OnServerRangedWeaponAmmoSelect);
        _serverApi.Event.PlayerNowPlaying += OnPlayerNowPlaying;
    }

    public void SetLastEntityRangedChargeData(long entityId, ItemSlot itemSlot)
    {
        _lastRangedSlotByEntityId[entityId] = itemSlot;
        RangedChargeStartByEntityId[entityId] = World.ElapsedMilliseconds;
    }
    public ItemSlot? GetLastEntityRangedItemSlot(long entityId)
    {
        return _lastRangedSlotByEntityId.TryGetValue(entityId, out ItemSlot? itemSlot) ? itemSlot : null;
    }


    private readonly ICoreServerAPI _serverApi;
    private readonly IServerNetworkChannel _serverNetworkChannel;
    private readonly Dictionary<long, ItemSlot?> _lastRangedSlotByEntityId = new();

    private void OnPlayerNowPlaying(IServerPlayer byPlayer)
    {
        if (byPlayer.Entity?.Attributes?.GetTreeAttribute("bullseyeSelectedAmmo") is not TreeAttribute treeAttribute) return;
        _serverNetworkChannel.SendPacket(new AmmoSyncPacket() { SelectedAmmoTree = treeAttribute.ToBytes() }, byPlayer);
    }
    private void OnServerRangedWeaponFire(IServerPlayer fromPlayer, FirePacket packet)
    {
        TreeAttribute tree = new();
        tree.SetLong("entityId", fromPlayer.Entity.EntityId);
        tree.SetInt("itemId", packet.ItemId);
        tree.SetDouble("aimX", packet.AimX);
        tree.SetDouble("aimY", packet.AimY);
        tree.SetDouble("aimZ", packet.AimZ);

        _serverApi.Event.PushEvent("bullseyeRangedWeaponFire", tree);
    }
    private void OnServerRangedWeaponAmmoSelect(IServerPlayer fromPlayer, AmmoSelectPacket packet)
    {
        ItemStack ammoItemStack = new();

        using (MemoryStream stream = new(packet.AmmoItemStack))
        {
            BinaryReader reader = new(stream);
            ammoItemStack.FromBytes(reader);
        }

        ammoItemStack.ResolveBlockOrItem(_serverApi.World);

        EntitySetAmmoType(fromPlayer.Entity, packet.AmmoCategory, ammoItemStack);
    }
}

internal sealed class SynchronizerClient : Synchronizer
{
    public SynchronizerClient(ICoreClientAPI api) : base(api)
    {
        _clientApi = api;

        _clientNetworkChannel = api.Network.RegisterChannel("bullseyeitem")
        .RegisterMessageType<FirePacket>()
        .RegisterMessageType<AmmoSelectPacket>()
        .RegisterMessageType<AmmoSyncPacket>()
        .SetMessageHandler<AmmoSyncPacket>(OnClientRangedWeaponAmmoSync);

        api.Event.AfterActiveSlotChanged += (changeEventArgs) =>
        {
            if (changeEventArgs.ToSlot > api.World.Player.InventoryManager?.GetHotbarInventory().Count) return;

            // @TODO Figure out what it is meant to do
        };
    }

    public void SendRangedWeaponFirePacket(int itemId, Vec3d targetVec)
    {
        _clientNetworkChannel.SendPacket(new FirePacket()
        {
            ItemId = itemId,
            AimX = targetVec.X,
            AimY = targetVec.Y,
            AimZ = targetVec.Z
        });
    }
    public void SendRangedWeaponAmmoSelectPacket(string ammoCategory, ItemStack ammoItemStack)
    {
        byte[] itemStackData;

        using (MemoryStream stream = new())
        {
            BinaryWriter writer = new(stream);
            ammoItemStack.ToBytes(writer);
            itemStackData = stream.ToArray();
        }

        _clientNetworkChannel.SendPacket(new AmmoSelectPacket()
        {
            AmmoCategory = ammoCategory,
            AmmoItemStack = itemStackData
        });
    }

    
    private readonly ICoreClientAPI _clientApi;
    private readonly IClientNetworkChannel _clientNetworkChannel;

    private void OnClientRangedWeaponAmmoSync(AmmoSyncPacket packet)
    {
        _clientApi.World.Player.Entity.Attributes.SetAttribute("bullseyeSelectedAmmo", TreeAttribute.CreateFromBytes(packet.SelectedAmmoTree));
    }
}