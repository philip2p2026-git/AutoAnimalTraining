using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutoAnimalTraining.Patches
{
    /// <summary>
    /// Prefix on WorkGiver_Train.JobOnThing — when enabled, prevents colonists
    /// from training non-roaming tamed animals that are outside the Training Zone.
    /// Roamer animals are unaffected (they can't be area-restricted anyway).
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_Train), nameof(WorkGiver_Train.JobOnThing))]
    public static class Patch_WorkGiver_Train_JobOnThing
    {
        public static bool Prefix(Pawn pawn, Thing t, ref Job __result)
        {
            var settings = AutoAnimalTrainingMod.Settings;
            if (settings == null || !settings.restrictTrainingToZone)
                return true; // Setting disabled — run vanilla logic

            if (!(t is Pawn animal))
                return true; // Not a pawn — let vanilla handle

            if (!animal.RaceProps.Animal)
                return true; // Not an animal

            // Skip restriction for roamer animals — they can't be area-controlled
            if (animal.playerSettings == null || !animal.playerSettings.SupportsAllowedAreas)
                return true; // Roamer or no player settings — let vanilla handle

            // Find the training zone on this map
            Area trainingZone = animal.Map?.areaManager.GetLabeled(settings.trainingZoneName);
            if (trainingZone == null)
                return true; // No training zone exists — let vanilla handle

            // Check if the animal is physically inside the training zone
            if (!trainingZone[animal.Position])
            {
                // Animal is outside the zone — block training
                __result = null;
                return false;
            }

            return true; // Animal is inside the zone — allow training
        }
    }
}
