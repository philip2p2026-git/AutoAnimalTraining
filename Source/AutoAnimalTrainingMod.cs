using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
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
            listing.Gap(16f);

            // Per-skill thresholds header
            listing.Label("--- Skill Step Triggers ---");
            listing.Gap(4f);
            listing.Label("Route animal when a wanted skill's steps <= threshold. Set to -1 to disable that skill.");
            listing.Gap(8f);

            // Ensure all defs are in the dictionary
            Settings.EnsureAllSkillsPresent();

            // Show slider for each TrainableDef
            List<TrainableDef> allDefs = DefDatabase<TrainableDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                TrainableDef td = allDefs[i];
                int current = Settings.GetThresholdForSkill(td);
                string stateLabel = current < 0 ? "OFF" : $"<= {current}";

                listing.Label($"{td.LabelCap} (max {td.steps} steps): trigger {stateLabel}");
                int newVal = (int)listing.Slider(current, -1, td.steps);
                Settings.SetThresholdForSkill(td, newVal);
                listing.Gap(2f);
            }

            listing.End();
        }

        public override string SettingsCategory()
        {
            return "Auto Animal Training";
        }
    }
}
