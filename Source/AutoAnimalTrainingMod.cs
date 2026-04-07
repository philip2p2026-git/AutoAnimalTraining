using HarmonyLib;
using Verse;

namespace AutoAnimalTraining
{
    public class AutoAnimalTrainingMod : Mod
    {
        public static AutoAnimalTrainingSettings Settings { get; private set; }

        public AutoAnimalTrainingMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AutoAnimalTrainingSettings>();

            var harmony = new Harmony("philip.autoanimaltraining");
            harmony.PatchAll();

            Log.Message("[AutoAnimalTraining] Loaded successfully — Harmony patches applied");
        }
    }
}
