namespace Bullseye.RangedWeapon;

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
    public string ammoType = "";

    // ItemRangedWeapon stats
    public float cooldownTime = 0.75f;
    public float chargeTime = 0.5f;
    public float projectileVelocity = 30f; // Vanilla arrow speed
    public float projectileSpread = 0f; // In degrees
    public float zeroingAngle = 0f;

    public bool allowSprint = true;
    public float moveSpeedPenalty = 0f;

    // Client aiming modsystem stats

    public string aimTexPartChargePath = "";
    public string aimTexFullChargePath = "";
    public string aimTexBlockedPath = "";

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
