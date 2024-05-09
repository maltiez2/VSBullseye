using HarmonyLib;
using System.Reflection;
using Vintagestory.Client.NoObf;

namespace Bullseye;

internal static class HarmonyPatches
{
    public static void Patch(string harmonyId, Aiming.ClientAiming aiming)
    {
        _aiming = aiming;

        new Harmony(harmonyId).Patch(typeof(ClientMain).GetMethod("UpdateCameraYawPitch", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyMethod(HarmonyPatches.UpdateCameraYawPitch)
            );

        new Harmony(harmonyId).Patch(typeof(SystemRenderAim).GetMethod("DrawAim", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyMethod(HarmonyPatches.DrawAim)
            );
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(ClientMain).GetMethod("UpdateCameraYawPitch", BindingFlags.Instance | BindingFlags.NonPublic), HarmonyPatchType.Prefix);
        new Harmony(harmonyId).Unpatch(typeof(SystemRenderAim).GetMethod("DrawAim", BindingFlags.Instance | BindingFlags.NonPublic), HarmonyPatchType.Prefix);
    }

    private static Aiming.ClientAiming? _aiming;

    private static bool DrawAim()
    {
        return !(_aiming?.Aiming ?? false);
    }

    private static bool UpdateCameraYawPitch(ClientMain __instance,
            ref double ___MouseDeltaX, ref double ___MouseDeltaY,
            ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY,
            float dt)
    {
        _aiming?.UpdateAimPoint(__instance, ref ___MouseDeltaX, ref ___MouseDeltaY, ref ___DelayedMouseDeltaX, ref ___DelayedMouseDeltaY, dt);

        return true;
    }
}
