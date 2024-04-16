using Bullseye.Old;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace Bullseye.New.Aiming;

internal class WeaponStats
{
    public WeaponsType WeaponType = WeaponsType.Bow;

    #region ClientAiming
    public float VerticalAccuracyMultiplier = 1f;
    public float HorizontalAccuracyMultiplier = 1f;
    public float AimDriftFrequency = 0.001f;
    public float AimDrift = 150f;
    public int AimTwitchDuration = 300;
    public float AimTwitch = 40f;
    public float HorizontalLimit = 0.125f;
    public float VerticalLimit = 0.35f;
    public float VerticalOffset = -0.15f;
    #endregion

    #region OLD
    // OLD
    public string ammoType = null;

    // ItemRangedWeapon stats
    public float cooldownTime = 0.75f;
    public float chargeTime = 0.5f;
    public float projectileVelocity = 30f; // Vanilla arrow speed
    public float projectileSpread = 0f; // In degrees
    public float zeroingAngle = 0f;

    public bool allowSprint = true;
    public float moveSpeedPenalty = 0f;

    // Client aiming modsystem stats
    

    

    public string aimTexPartChargePath = null;
    public string aimTexFullChargePath = null;
    public string aimTexBlockedPath = null;

    public float aimFullChargeLeeway = 0.25f;

    // AimAccuracy EntityBehaviour stats
    public float accuracyStartTime = 1f;
    public float accuracyStart = 2.5f;

    public float accuracyOvertimeStart = 6f;
    public float accuracyOvertimeTime = 12f;
    public float accuracyOvertime = 1f;

    public float accuracyMovePenalty = 1f;
    #endregion
}

internal sealed class ClientAiming : IDisposable
{
    public bool Aiming { get; set; } = false;
    public bool ShowReticle { get; private set; } = true;
    public Vec3d TargetVec { get; private set; } = new Vec3d();
    public Vec2f Aim { get; private set; } = new Vec2f();
    public Vec2f AimOffset { get; private set; } = new Vec2f();
    
    public float DriftMultiplier { get; set; } = 1f;
    public float TwitchMultiplier { get; set; } = 1f;
    public BullseyeEnumWeaponReadiness WeaponReadiness { get; set; } = BullseyeEnumWeaponReadiness.Blocked;


    public ClientAiming(ICoreClientAPI api)
    {
        _clientApi = api;
        _configSystem = api.ModLoader.GetModSystem<ConfigSystem>();

        _unprojectionTool = new Unproject();
        ResetAim();

        _reticleRenderer = new ReticleRenderer(api, GetCurrentAim);
        api.Event.RegisterRenderer(_reticleRenderer, EnumRenderStage.Ortho);
    }
    public void StartAiming(WeaponStats stats)
    {
        _weaponStats = stats;

        // If 15 seconds passed since we last made a shot, reset aim to centre of the screen
        if (_clientApi.World.ElapsedMilliseconds - lastAimingEndTime > aimResetTime)
        {
            ResetAim();
        }
        else
        {
            ResetAimOffset();
        }

        if (_configSystem.GetAimingType(stats.WeaponType) == AimingType.Camera)
        {
            SetFixedAimPoint(_clientApi.Render.FrameWidth, _clientApi.Render.FrameHeight);
        }

        Aiming = true;
        aimingDt = 0f;
    }
    public void StopAiming()
    {
        Aiming = false;

        lastAimingEndTime = _clientApi.World.ElapsedMilliseconds;
    }
    public Vec2f GetCurrentAim()
    {
        float offsetMagnitude = _configSystem.ServerSettings.AimDifficulty;

        if (_clientApi.World.Player?.Entity != null)
        {
            offsetMagnitude /= GameMath.Max(_clientApi.World.Player.Entity.Stats.GetBlended("rangedWeaponsAcc"), 0.001f);
        }

        float interpolation = GameMath.Sqrt(GameMath.Min(aimingDt / aimStartInterpolationTime, 1f));

        currentAim.X = (Aim.X + AimOffset.X * offsetMagnitude * _weaponStats.HorizontalAccuracyMultiplier) * interpolation;
        currentAim.Y = (Aim.Y + AimOffset.Y * offsetMagnitude * _weaponStats.VerticalAccuracyMultiplier) * interpolation;

        return currentAim;
    }
    // TODO: For a rewrite, consider switching aimX and aimY from pixels to % of screen width/height. That way it's consistent on all resolutions
    // (still will have to account for FoV though).
    public void UpdateAimPoint(ClientMain __instance,
            ref double ___MouseDeltaX, ref double ___MouseDeltaY,
            ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY,
            float dt)
    {
        if (Aiming)
        {
            // Default FOV is 70, and 1920 is the screen width of my dev machine :) 
            currentFovRatio = (__instance.Width / 1920f) * (GameMath.Tan((70f / 2 * GameMath.DEG2RAD)) / GameMath.Tan((_configSystem.ClientSettings.FieldOfView / 2 * GameMath.DEG2RAD)));
            aimingDt += dt;

            // Update
            switch (_weaponStats.WeaponType)
            {
                case WeaponsType.Sling: UpdateAimOffsetSling(__instance, dt); break;
                default: UpdateAimOffsetSimple(__instance, dt); break;
            }

            if (_configSystem.GetAimingType(_weaponStats.WeaponType) == AimingType.Cursor)
            {
                UpdateMouseDelta(__instance, ref ___MouseDeltaX, ref ___MouseDeltaY, ref ___DelayedMouseDeltaX, ref ___DelayedMouseDeltaY);
            }

            SetAim();
        }
    }
    
    public void SetReticleTextures(LoadedTexture partChargeTex, LoadedTexture fullChargeTex, LoadedTexture blockedTex)
    {
        _reticleRenderer.SetReticleTextures(partChargeTex, fullChargeTex, blockedTex);
    }
    public void Dispose()
    {
        _clientApi.Event.UnregisterRenderer(_reticleRenderer, EnumRenderStage.Ortho);
    }



    private readonly NormalizedSimplexNoise _noiseGenerator = NormalizedSimplexNoise.FromDefaultOctaves(4, 1.0, 0.9, 123L);
    private readonly ConfigSystem _configSystem;
    private readonly Random _random = new();
    private readonly ICoreClientAPI _clientApi;
    private readonly ReticleRenderer _reticleRenderer;
    private readonly Unproject _unprojectionTool;

    private WeaponStats _weaponStats = new();

    private long twitchLastChangeMilliseconds;
    private long twitchLastStepMilliseconds;
    private Vec2f twitch = new();
    
    private double[] viewport = new double[4];
    private double[] rayStart = new double[4];
    private double[] rayEnd = new double[4];
    private float aimingDt;
    private long lastAimingEndTime = 0;
    private const long aimResetTime = 15000;
    private const float aimStartInterpolationTime = 0.3f;
    private Vec2f currentAim = new();
    private float currentFovRatio;
    private float slingHorizRandomOffset;
    const float slingCycleLength = 0.9f;
    const float slingCycleStartDeadzone = 0.1f;
    const float slingCycleEndCoyoteTime = 0.1f; // Human visual reaction time is 250ms on average, a little 'coyote time' makes shooting more satisfying

    private void ResetAimOffset()
    {
        AimOffset.X = 0f;
        AimOffset.Y = 0f;

        twitch.X = 0f;
        twitch.Y = 0f;

        ShowReticle = true;
    }
    private void ResetAim()
    {
        Aim.X = 0f;
        Aim.Y = 0f;

        ResetAimOffset();
    }
    private void UpdateAimOffsetSimple(ClientMain __instance, float dt)
    {
        UpdateAimOffsetSimpleDrift(__instance, dt);
        UpdateAimOffsetSimpleTwitch(__instance, dt);
    }
    private void UpdateAimOffsetSimpleDrift(ClientMain __instance, float dt)
    {
        const float driftMaxRatio = 1.1f;

        float xNoise = ((float)_noiseGenerator.Noise(__instance.ElapsedMilliseconds * _weaponStats.AimDriftFrequency, 1000f) - 0.5f);
        float yNoise = ((float)_noiseGenerator.Noise(-1000f, __instance.ElapsedMilliseconds * _weaponStats.AimDriftFrequency) - 0.5f);

        float maxDrift = GameMath.Max(_weaponStats.AimDrift * driftMaxRatio * DriftMultiplier, 1f) * currentFovRatio;

        AimOffset.X += ((xNoise - AimOffset.X / maxDrift) * _weaponStats.AimDrift * DriftMultiplier * dt * currentFovRatio);
        AimOffset.Y += ((yNoise - AimOffset.Y / maxDrift) * _weaponStats.AimDrift * DriftMultiplier * dt * currentFovRatio);
    }
    private void UpdateAimOffsetSimpleTwitch(ClientMain __instance, float dt)
    {
        // Don't ask me why aimOffset needs to be multiplied by fovRatio here, but not in the Drift function
        // Frankly the whole thing is up for a full rework anyway, but I don't want to get into that until I get started on crossbows and stuff
        float fovModAimOffsetX = AimOffset.X * currentFovRatio;
        float fovModAimOffsetY = AimOffset.Y * currentFovRatio;

        const float twitchMaxRatio = 1 / 7f;

        if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + _weaponStats.AimTwitchDuration)
        {
            twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
            twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

            float twitchMax = GameMath.Max(_weaponStats.AimTwitch * twitchMaxRatio * TwitchMultiplier, 1f) * currentFovRatio;

            twitch.X = (((float)_random.NextDouble() - 0.5f) * 2f) * twitchMax - fovModAimOffsetX / twitchMax;
            twitch.Y = (((float)_random.NextDouble() - 0.5f) * 2f) * twitchMax - fovModAimOffsetY / twitchMax;

            float twitchLength = GameMath.Max(GameMath.Sqrt(twitch.X * twitch.X + twitch.Y * twitch.Y), 1f);

            twitch.X = twitch.X / twitchLength;
            twitch.Y = twitch.Y / twitchLength;
        }

        float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)_weaponStats.AimTwitchDuration;
        float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)_weaponStats.AimTwitchDuration;

        float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

        AimOffset.X += (twitch.X * stepSize * (_weaponStats.AimTwitch * TwitchMultiplier * dt) * (_weaponStats.AimTwitchDuration / 20) * currentFovRatio);
        AimOffset.Y += (twitch.Y * stepSize * (_weaponStats.AimTwitch * TwitchMultiplier * dt) * (_weaponStats.AimTwitchDuration / 20) * currentFovRatio);

        twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;
    }
    private void UpdateAimOffsetSling(ClientMain __instance, float dt)
    {
        float fovRatio = (__instance.Width / 1920f) * (GameMath.Tan((70f / 2 * GameMath.DEG2RAD)) / GameMath.Tan((_configSystem.ClientSettings.FieldOfView / 2 * GameMath.DEG2RAD)));

        float slingRiseArea = 350 * fovRatio;
        float slingHorizArea = 35 * fovRatio;

        float slingHorizTwitch = _weaponStats.AimTwitch * TwitchMultiplier * fovRatio;

        float slingDt = aimingDt % slingCycleLength;

        if (slingDt <= dt)
        {
            slingHorizRandomOffset = slingHorizTwitch * (((float)_random.NextDouble() - 0.5f) * 2f);
        }

        ShowReticle = slingDt > slingCycleStartDeadzone && slingDt < slingCycleLength - slingCycleEndCoyoteTime;

        float slingRatioCurrent = slingDt - slingCycleStartDeadzone;
        float slingRatioMax = slingCycleLength - slingCycleStartDeadzone - slingCycleEndCoyoteTime;

        float slingCurrentPoint = GameMath.Min(slingRatioCurrent / slingRatioMax, 1f);

        AimOffset.X = slingHorizRandomOffset - slingHorizArea * slingCurrentPoint;
        AimOffset.Y = (slingRiseArea / 2f) - (slingRiseArea * slingCurrentPoint);
    }
    private void UpdateMouseDelta(ClientMain __instance,
            ref double ___MouseDeltaX, ref double ___MouseDeltaY,
            ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY)
    {
        float horizontalAimLimit = (__instance.Width / 2f) * _weaponStats.HorizontalLimit;
        float verticalAimLimit = (__instance.Height / 2f) * _weaponStats.VerticalLimit;
        float verticalAimOffset = (__instance.Height / 2f) * _weaponStats.VerticalOffset;

        float yInversionFactor = _configSystem.ClientSettings.InvertMouseYAxis ? -1 : 1;

        float deltaX = (float)(___MouseDeltaX - ___DelayedMouseDeltaX);
        float deltaY = (float)(___MouseDeltaY - ___DelayedMouseDeltaY) * yInversionFactor;

        if (Math.Abs(Aim.X + deltaX) > horizontalAimLimit)
        {
            Aim.X = Aim.X > 0 ? horizontalAimLimit : -horizontalAimLimit;
        }
        else
        {
            Aim.X += deltaX;
            ___DelayedMouseDeltaX = ___MouseDeltaX;
        }

        if (Math.Abs(Aim.Y + deltaY - verticalAimOffset) > verticalAimLimit)
        {
            Aim.Y = (Aim.Y > 0 ? verticalAimLimit : -verticalAimLimit) + verticalAimOffset;
        }
        else
        {
            Aim.Y += deltaY;
            ___DelayedMouseDeltaY = ___MouseDeltaY;
        }
    }
    private void SetFixedAimPoint(int screenWidth, int screenHeight)
    {
        float difficultyModifier = GameMath.Clamp(_configSystem.ServerSettings.AimDifficulty, 0, 1);

        float horizontalLimit = GameMath.Min(_weaponStats.HorizontalLimit, 0.25f);
        float verticalLimit = GameMath.Min(_weaponStats.VerticalLimit, 0.25f);
        float verticalOffset = GameMath.Clamp(_weaponStats.VerticalOffset, -0.05f, 0.1f);

        float horizontalAimLimit = (screenWidth / 2f) * horizontalLimit;
        float verticalAimLimit = (screenHeight / 2f) * verticalLimit;
        float verticalAimOffset = (screenHeight / 2f) * verticalOffset;

        float maxHorizontalShift = horizontalAimLimit / 2.25f;
        float maxVerticalShift = verticalAimLimit / 2.25f;

        maxHorizontalShift = GameMath.Max(maxHorizontalShift, GameMath.Min(maxVerticalShift, horizontalAimLimit));
        maxVerticalShift = GameMath.Max(maxVerticalShift, GameMath.Min(maxHorizontalShift, verticalAimLimit));

        float horizontalCenter = GameMath.Clamp(Aim.X, -maxHorizontalShift, maxHorizontalShift);
        float verticalCenter = GameMath.Clamp(Aim.Y, -maxVerticalShift, maxVerticalShift);

        Aim.X = (horizontalCenter + (-maxHorizontalShift + ((float)_random.NextDouble() * maxHorizontalShift * 2f))) * difficultyModifier;
        Aim.Y = (verticalCenter + (-maxVerticalShift + ((float)_random.NextDouble() * maxVerticalShift * 2f)) + verticalAimOffset) * difficultyModifier;
    }
    private void SetAim()
    {
        Vec2f currentAim = GetCurrentAim();

        int mouseCurrentX = (int)currentAim.X + _clientApi.Render.FrameWidth / 2;
        int mouseCurrentY = (int)currentAim.Y + _clientApi.Render.FrameHeight / 2;
        viewport[0] = 0.0;
        viewport[1] = 0.0;
        viewport[2] = _clientApi.Render.FrameWidth;
        viewport[3] = _clientApi.Render.FrameHeight;

        bool unprojectPassed = true;
        unprojectPassed |= _unprojectionTool.UnProject(mouseCurrentX, _clientApi.Render.FrameHeight - mouseCurrentY, 1, _clientApi.Render.MvMatrix.Top, _clientApi.Render.PMatrix.Top, viewport, rayEnd);
        unprojectPassed |= _unprojectionTool.UnProject(mouseCurrentX, _clientApi.Render.FrameHeight - mouseCurrentY, 0, _clientApi.Render.MvMatrix.Top, _clientApi.Render.PMatrix.Top, viewport, rayStart);

        // If unproject fails, well, not much we can do really. Try not to crash
        if (!unprojectPassed) return;

        double offsetX = rayEnd[0] - rayStart[0];
        double offsetY = rayEnd[1] - rayStart[1];
        double offsetZ = rayEnd[2] - rayStart[2];
        float length = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ);

        // If length is *somehow* zero, just abort not to crash. The start and end of the ray are in the same place, what to even do in that situation?
        if (length == 0) return;

        offsetX /= length;
        offsetY /= length;
        offsetZ /= length;

        TargetVec.X = offsetX;
        TargetVec.Y = offsetY;
        TargetVec.Z = offsetZ;
    }
}