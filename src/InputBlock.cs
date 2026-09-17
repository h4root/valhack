using HarmonyLib;

namespace ValheimAdminOverlay
{
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class TakeInputPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Overlay.IsOpen) return true;
            __result = false;
            return false;
        }
    }
}
