using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Bullseye.Old;

namespace Bullseye.New.Aiming;

internal sealed class ClientAiming : IDisposable
{
    public bool Aiming { get; set; } = false;
    public bool ShowReticle { get; private set; } = true;
    public Vec3d TargetVec { get; private set; } = new Vec3d();
    public Vec2f aim { get; private set; } = new Vec2f();
    public Vec2f aimOffset { get; private set; } = new Vec2f();
    public BullseyeRangedWeaponStats WeaponStats { get; set; } = new BullseyeRangedWeaponStats();
    public float DriftMultiplier { get; set; } = 1f;
    public float TwitchMultiplier { get; set; } = 1f;
    public BullseyeEnumWeaponReadiness WeaponReadiness { get; set; } = BullseyeEnumWeaponReadiness.Blocked;


    public ClientAiming(ICoreClientAPI api)
    {
        _clientApi = api;

        _configSystem = api.ModLoader.GetModSystem<ConfigSystem>();

        unproject = new Unproject();
        ResetAim();

        reticleRenderer = new BullseyeReticleRenderer(api, GetCurrentAim);
        api.Event.RegisterRenderer(reticleRenderer, EnumRenderStage.Ortho);

        
    }
    public void StartAiming(WeaponsType weaponType)
    {
        _currentWeaponType = weaponType;
        
        // If 15 seconds passed since we last made a shot, reset aim to centre of the screen
        if (_clientApi.World.ElapsedMilliseconds - lastAimingEndTime > aimResetTime)
        {
            ResetAim();
        }
        else
        {
            ResetAimOffset();
        }

        if (_configSystem.ClientSettings.BowAimingType == AimingType.Camera)
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

        currentAim.X = (aim.X + aimOffset.X * offsetMagnitude * WeaponStats.horizontalAccuracyMult) * interpolation;
        currentAim.Y = (aim.Y + aimOffset.Y * offsetMagnitude * WeaponStats.verticalAccuracyMult) * interpolation;

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
            switch (WeaponStats.weaponType)
            {
                case BullseyeRangedWeaponType.Sling: UpdateAimOffsetSling(__instance, dt); break;
                default: UpdateAimOffsetSimple(__instance, dt); break;
            }

            if (_configSystem.ClientSettings.BowAimingType == AimingType.Cursor)
            {
                UpdateMouseDelta(__instance, ref ___MouseDeltaX, ref ___MouseDeltaY, ref ___DelayedMouseDeltaX, ref ___DelayedMouseDeltaY);
            }

            SetAim();
        }
    }
    public void UpdateAimOffsetSimple(ClientMain __instance, float dt)
    {
        UpdateAimOffsetSimpleDrift(__instance, dt);
        UpdateAimOffsetSimpleTwitch(__instance, dt);
    }
    public void UpdateAimOffsetSimpleDrift(ClientMain __instance, float dt)
    {
        const float driftMaxRatio = 1.1f;

        float xNoise = ((float)noisegen.Noise(__instance.ElapsedMilliseconds * WeaponStats.aimDriftFrequency, 1000f) - 0.5f);
        float yNoise = ((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * WeaponStats.aimDriftFrequency) - 0.5f);

        float maxDrift = GameMath.Max(WeaponStats.aimDrift * driftMaxRatio * DriftMultiplier, 1f) * currentFovRatio;

        aimOffset.X += ((xNoise - aimOffset.X / maxDrift) * WeaponStats.aimDrift * DriftMultiplier * dt * currentFovRatio);
        aimOffset.Y += ((yNoise - aimOffset.Y / maxDrift) * WeaponStats.aimDrift * DriftMultiplier * dt * currentFovRatio);
    }
    public void UpdateAimOffsetSimpleTwitch(ClientMain __instance, float dt)
    {
        // Don't ask me why aimOffset needs to be multiplied by fovRatio here, but not in the Drift function
        // Frankly the whole thing is up for a full rework anyway, but I don't want to get into that until I get started on crossbows and stuff
        float fovModAimOffsetX = aimOffset.X * currentFovRatio;
        float fovModAimOffsetY = aimOffset.Y * currentFovRatio;

        const float twitchMaxRatio = 1 / 7f;

        if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + WeaponStats.aimTwitchDuration)
        {
            twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
            twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

            float twitchMax = GameMath.Max(WeaponStats.aimTwitch * twitchMaxRatio * TwitchMultiplier, 1f) * currentFovRatio;

            twitch.X = (((float)_random.NextDouble() - 0.5f) * 2f) * twitchMax - fovModAimOffsetX / twitchMax;
            twitch.Y = (((float)_random.NextDouble() - 0.5f) * 2f) * twitchMax - fovModAimOffsetY / twitchMax;

            float twitchLength = GameMath.Max(GameMath.Sqrt(twitch.X * twitch.X + twitch.Y * twitch.Y), 1f);

            twitch.X = twitch.X / twitchLength;
            twitch.Y = twitch.Y / twitchLength;
        }

        float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)WeaponStats.aimTwitchDuration;
        float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)WeaponStats.aimTwitchDuration;

        float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

        aimOffset.X += (twitch.X * stepSize * (WeaponStats.aimTwitch * TwitchMultiplier * dt) * (WeaponStats.aimTwitchDuration / 20) * currentFovRatio);
        aimOffset.Y += (twitch.Y * stepSize * (WeaponStats.aimTwitch * TwitchMultiplier * dt) * (WeaponStats.aimTwitchDuration / 20) * currentFovRatio);

        twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;
    }
    public void UpdateAimOffsetSling(ClientMain __instance, float dt)
    {
        float fovRatio = (__instance.Width / 1920f) * (GameMath.Tan((70f / 2 * GameMath.DEG2RAD)) / GameMath.Tan((_configSystem.ClientSettings.FieldOfView / 2 * GameMath.DEG2RAD)));

        float slingRiseArea = 350 * fovRatio;
        float slingHorizArea = 35 * fovRatio;

        float slingHorizTwitch = WeaponStats.aimTwitch * TwitchMultiplier * fovRatio;

        float slingDt = aimingDt % slingCycleLength;

        if (slingDt <= dt)
        {
            slingHorizRandomOffset = slingHorizTwitch * (((float)_random.NextDouble() - 0.5f) * 2f);
        }

        ShowReticle = slingDt > slingCycleStartDeadzone && slingDt < slingCycleLength - slingCycleEndCoyoteTime;

        float slingRatioCurrent = slingDt - slingCycleStartDeadzone;
        float slingRatioMax = slingCycleLength - slingCycleStartDeadzone - slingCycleEndCoyoteTime;

        float slingCurrentPoint = GameMath.Min(slingRatioCurrent / slingRatioMax, 1f);

        aimOffset.X = slingHorizRandomOffset - slingHorizArea * slingCurrentPoint;
        aimOffset.Y = (slingRiseArea / 2f) - (slingRiseArea * slingCurrentPoint);
    }
    public void UpdateMouseDelta(ClientMain __instance,
            ref double ___MouseDeltaX, ref double ___MouseDeltaY,
            ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY)
    {
        float horizontalAimLimit = (__instance.Width / 2f) * WeaponStats.horizontalLimit;
        float verticalAimLimit = (__instance.Height / 2f) * WeaponStats.verticalLimit;
        float verticalAimOffset = (__instance.Height / 2f) * WeaponStats.verticalOffset;

        float yInversionFactor = _configSystem.ClientSettings.InvertMouseYAxis ? -1 : 1;

        float deltaX = (float)(___MouseDeltaX - ___DelayedMouseDeltaX);
        float deltaY = (float)(___MouseDeltaY - ___DelayedMouseDeltaY) * yInversionFactor;

        if (Math.Abs(aim.X + deltaX) > horizontalAimLimit)
        {
            aim.X = aim.X > 0 ? horizontalAimLimit : -horizontalAimLimit;
        }
        else
        {
            aim.X += deltaX;
            ___DelayedMouseDeltaX = ___MouseDeltaX;
        }

        if (Math.Abs(aim.Y + deltaY - verticalAimOffset) > verticalAimLimit)
        {
            aim.Y = (aim.Y > 0 ? verticalAimLimit : -verticalAimLimit) + verticalAimOffset;
        }
        else
        {
            aim.Y += deltaY;
            ___DelayedMouseDeltaY = ___MouseDeltaY;
        }
    }
    public void SetFixedAimPoint(int screenWidth, int screenHeight)
    {
        float difficultyModifier = GameMath.Clamp(_configSystem.ServerSettings.AimDifficulty, 0, 1);

        float horizontalLimit = GameMath.Min(WeaponStats.horizontalLimit, 0.25f);
        float verticalLimit = GameMath.Min(WeaponStats.verticalLimit, 0.25f);
        float verticalOffset = GameMath.Clamp(WeaponStats.verticalOffset, -0.05f, 0.1f);

        float horizontalAimLimit = (screenWidth / 2f) * horizontalLimit;
        float verticalAimLimit = (screenHeight / 2f) * verticalLimit;
        float verticalAimOffset = (screenHeight / 2f) * verticalOffset;

        float maxHorizontalShift = horizontalAimLimit / 2.25f;
        float maxVerticalShift = verticalAimLimit / 2.25f;

        maxHorizontalShift = GameMath.Max(maxHorizontalShift, GameMath.Min(maxVerticalShift, horizontalAimLimit));
        maxVerticalShift = GameMath.Max(maxVerticalShift, GameMath.Min(maxHorizontalShift, verticalAimLimit));

        float horizontalCenter = GameMath.Clamp(aim.X, -maxHorizontalShift, maxHorizontalShift);
        float verticalCenter = GameMath.Clamp(aim.Y, -maxVerticalShift, maxVerticalShift);

        aim.X = (horizontalCenter + (-maxHorizontalShift + ((float)_random.NextDouble() * maxHorizontalShift * 2f))) * difficultyModifier;
        aim.Y = (verticalCenter + (-maxVerticalShift + ((float)_random.NextDouble() * maxVerticalShift * 2f)) + verticalAimOffset) * difficultyModifier;
    }
    public void SetAim()
    {
        Vec2f currentAim = GetCurrentAim();

        int mouseCurrentX = (int)currentAim.X + _clientApi.Render.FrameWidth / 2;
        int mouseCurrentY = (int)currentAim.Y + _clientApi.Render.FrameHeight / 2;
        viewport[0] = 0.0;
        viewport[1] = 0.0;
        viewport[2] = _clientApi.Render.FrameWidth;
        viewport[3] = _clientApi.Render.FrameHeight;

        bool unprojectPassed = true;
        unprojectPassed |= unproject.UnProject(mouseCurrentX, _clientApi.Render.FrameHeight - mouseCurrentY, 1, _clientApi.Render.MvMatrix.Top, _clientApi.Render.PMatrix.Top, viewport, rayEnd);
        unprojectPassed |= unproject.UnProject(mouseCurrentX, _clientApi.Render.FrameHeight - mouseCurrentY, 0, _clientApi.Render.MvMatrix.Top, _clientApi.Render.PMatrix.Top, viewport, rayStart);

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
    public void SetWeaponReadinessState(BullseyeEnumWeaponReadiness state)
    {
        WeaponReadiness = state;
    }

    public void SetReticleTextures(LoadedTexture partChargeTex, LoadedTexture fullChargeTex, LoadedTexture blockedTex)
    {
        reticleRenderer.SetReticleTextures(partChargeTex, fullChargeTex, blockedTex);
    }
    public void Dispose()
    {
        // Can be null when loading a world aborts partway through
        if (reticleRenderer != null)
        {
            _clientApi.Event.UnregisterRenderer(reticleRenderer, EnumRenderStage.Ortho);
            reticleRenderer = null;
        }

        _clientApi = null;

        _configSystem = null;

        _random = null;

        WeaponStats = null;
        noisegen = null;
        unproject = null;
    }



    private NormalizedSimplexNoise noisegen = NormalizedSimplexNoise.FromDefaultOctaves(4, 1.0, 0.9, 123L);
    private ConfigSystem _configSystem;
    private Random _random = new();
    private ICoreClientAPI? _clientApi;
    private BullseyeReticleRenderer reticleRenderer;
    private long twitchLastChangeMilliseconds;
    private long twitchLastStepMilliseconds;
    private Vec2f twitch = new();
    private Unproject unproject;
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

    private WeaponsType _currentWeaponType = WeaponsType.Bow;

    private void ResetAimOffset()
    {
        aimOffset.X = 0f;
        aimOffset.Y = 0f;

        twitch.X = 0f;
        twitch.Y = 0f;

        ShowReticle = true;
    }
    private void ResetAim()
    {
        aim.X = 0f;
        aim.Y = 0f;

        ResetAimOffset();
    }
}