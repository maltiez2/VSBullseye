using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Bullseye;

internal enum AimingType
{
    Cursor,
    Camera,
    Vanilla
}

internal enum WeaponsType
{
    Bow,
    Spear,
    Throwable,
    Sling
}

internal class ClientSideSettings
{
    public bool ReticleScaling { get; set; } = false;
    public AimingType BowAimingType { get; set; } = AimingType.Cursor;
    public AimingType SpearAimingType { get; set; } = AimingType.Cursor;
    public AimingType ThrowableAimingType { get; set; } = AimingType.Cursor;
    public AimingType SlingAimingType { get; set; } = AimingType.Cursor;
    public bool InvertMouseYAxis { get; set; } = false;
    public float FieldOfView { get; set; } = 70f;
}

internal class ServerSideSettings
{
    public float AimDifficulty { get; set; } = 1f;
    public float BowDamage { get; set; } = 1f;
    public float SpearDamage { get; set; } = 1f;
    public float ThrowableDamage { get; set; } = 1f;
    public float SlingDamage { get; set; } = 1f;
}

internal class ConfigSystem : ModSystem
{
    public ClientSideSettings ClientSettings { get; set; } = new();
    public ServerSideSettings ServerSettings { get; set; } = new();
    
    public event Action<string>? SettingChanged;

    public override void Start(ICoreAPI api)
    {
        api.Event.RegisterEventBusListener(OnSettingChanged, filterByEventName: "configlib:bullseye-continued:setting-changed");
        api.Event.RegisterEventBusListener(OnSettingChanged, filterByEventName: "configlib:bullseye-continued:setting-loaded");

        _api = api;
    }

    public AimingType GetAimingType(WeaponsType weaponType)
    {
        return weaponType switch
        {
            WeaponsType.Bow => ClientSettings.BowAimingType,
            WeaponsType.Spear => ClientSettings.SpearAimingType,
            WeaponsType.Throwable => ClientSettings.ThrowableAimingType,
            WeaponsType.Sling => ClientSettings.SlingAimingType,
            _ => AimingType.Vanilla
        };
    }
    public float GetDamage(WeaponsType weaponType)
    {
        return weaponType switch
        {
            WeaponsType.Bow => ServerSettings.BowDamage,
            WeaponsType.Spear => ServerSettings.SpearDamage,
            WeaponsType.Throwable => ServerSettings.ThrowableDamage,
            WeaponsType.Sling => ServerSettings.SlingDamage,
            _ => 1
        };
    }

    private ICoreAPI? _api;

    private void OnSettingChanged(string eventName, ref EnumHandling handling, IAttribute data)
    {
        handling = EnumHandling.Handled;

        if (data is not TreeAttribute dataTree) return;

        string setting = dataTree.GetString("setting");

        SettingChanged?.Invoke(setting);

        _api?.Logger.VerboseDebug($"[Bullseye] Set '{setting}' to '{dataTree.GetAsString("value")}'");
        
        switch (setting)
        {
            case "AimDifficulty":
                ServerSettings.AimDifficulty = dataTree.GetFloat("value");
                break;
            case "BowDamage":
                ServerSettings.BowDamage = dataTree.GetFloat("value");
                break;
            case "SpearDamage":
                ServerSettings.SpearDamage = dataTree.GetFloat("value");
                break;
            case "ThrowableDamage":
                ServerSettings.ThrowableDamage = dataTree.GetFloat("value");
                break;
            case "SlingDamage":
                ServerSettings.SlingDamage = dataTree.GetFloat("value");
                break;
            case "ReticleScaling":
                ClientSettings.ReticleScaling = dataTree.GetBool("value");
                break;
            case "BowAimingType":
                ClientSettings.BowAimingType = (AimingType)dataTree.GetInt("value");
                break;
            case "SpearAimingType":
                ClientSettings.SpearAimingType = (AimingType)dataTree.GetInt("value");
                break;
            case "ThrowableAimingType":
                ClientSettings.ThrowableAimingType = (AimingType)dataTree.GetInt("value");
                break;
            case "SlingAimingType":
                ClientSettings.SlingAimingType = (AimingType)dataTree.GetInt("value");
                break;
            case "InvertMouseYAxis":
                ClientSettings.InvertMouseYAxis = dataTree.GetBool("value");
                break;
            case "FieldOfView":
                ClientSettings.FieldOfView = dataTree.GetFloat("value");
                break;
        }
    }
}