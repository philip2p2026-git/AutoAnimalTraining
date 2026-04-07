using HarmonyLib;
using RimWorld;
using Verse;

namespace AutoAnimalTraining.Patches
{
    /// <summary>
    /// Postfix on AreaManager.TryMakeNewAllowed — fires when a new area is created.
    /// </summary>
    [HarmonyPatch(typeof(AreaManager), nameof(AreaManager.TryMakeNewAllowed))]
    public static class Patch_AreaManager_TryMakeNewAllowed
    {
        public static void Postfix(AreaManager __instance, bool __result, Area_Allowed area)
        {
            if (!__result || area == null)
                return;

            MapComponent_AutoTraining comp = __instance.map.GetComponent<MapComponent_AutoTraining>();
            comp?.Notify_AreaChanged();
        }
    }

    /// <summary>
    /// Postfix on AreaManager.Remove — fires when an area is deleted.
    /// </summary>
    [HarmonyPatch(typeof(AreaManager), "Remove")]
    public static class Patch_AreaManager_Remove
    {
        public static void Postfix(AreaManager __instance, Area area)
        {
            MapComponent_AutoTraining comp = __instance.map.GetComponent<MapComponent_AutoTraining>();
            comp?.Notify_AreaChanged();
        }
    }

    /// <summary>
    /// Postfix on Area_Allowed.SetLabel — fires when an area is renamed via code.
    /// </summary>
    [HarmonyPatch(typeof(Area_Allowed), nameof(Area_Allowed.SetLabel))]
    public static class Patch_AreaAllowed_SetLabel
    {
        public static void Postfix(Area_Allowed __instance)
        {
            MapComponent_AutoTraining comp = __instance.Map.GetComponent<MapComponent_AutoTraining>();
            comp?.Notify_AreaChanged();
        }
    }

    /// <summary>
    /// Postfix on Area.set_RenamableLabel — fires when an area is renamed via the UI dialog.
    /// </summary>
    [HarmonyPatch(typeof(Area), nameof(Area.RenamableLabel), MethodType.Setter)]
    public static class Patch_Area_RenamableLabel_Set
    {
        public static void Postfix(Area __instance)
        {
            if (__instance is Area_Allowed)
            {
                MapComponent_AutoTraining comp = __instance.Map.GetComponent<MapComponent_AutoTraining>();
                comp?.Notify_AreaChanged();
            }
        }
    }
}
