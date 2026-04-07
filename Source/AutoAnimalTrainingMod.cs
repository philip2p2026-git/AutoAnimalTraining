using HarmonyLib;
using UnityEngine;
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

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // Training zone name
            listing.Label("Training zone name (area must match exactly):");
            Settings.trainingZoneName = listing.TextEntry(Settings.trainingZoneName);
            listing.Gap(12f);

            // Poll interval
            listing.Label($"Poll interval for training checks (ticks): {Settings.pollIntervalTicks}");
            Settings.pollIntervalTicks = (int)listing.Slider(Settings.pollIntervalTicks, 250, 10000);
            listing.Gap(6f);
            listing.Label("250 ticks = ~4 sec, 2000 ticks = ~33 sec, 10000 ticks = ~2.7 min");

            listing.End();
        }

        public override string SettingsCategory()
        {
            return "Auto Animal Training";
        }
    }
}
