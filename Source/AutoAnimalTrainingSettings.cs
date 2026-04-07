using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AutoAnimalTraining
{
    public class AutoAnimalTrainingSettings : ModSettings
    {
        public string trainingZoneName = "@Training";
        public int pollIntervalTicks = 2000;

        /// <summary>
        /// Per-skill step thresholds. Key = TrainableDef.defName, Value = threshold.
        /// Route animal when steps &lt;= threshold for a wanted skill.
        /// Default threshold for any skill not in this dict = -1 (disabled).
        /// </summary>
        public Dictionary<string, int> skillThresholds = new Dictionary<string, int>();

        /// <summary>
        /// Returns the threshold for a specific trainable skill.
        /// -1 means this skill won't trigger routing.
        /// </summary>
        // Actual vanilla max steps: Tameness=5, Obedience=3, Release=2, Rescue=2, Haul=7
        private static readonly Dictionary<string, int> DefaultThresholds = new Dictionary<string, int>
        {
            { "Tameness", 4 },    // Route when steps <= 4 (out of 5)
            { "Obedience", 2 },   // Route when steps <= 2 (out of 3)
            { "Release", 1 },     // Route when steps <= 1 (out of 2)
            { "Rescue", 1 },      // Route when steps <= 1 (out of 2)
            { "Haul", 6 },        // Route when steps <= 6 (out of 7)
        };

        /// <summary>
        /// Returns the default threshold for a skill.
        /// Known skills have preset defaults; unknown skills default to -1 (disabled).
        /// </summary>
        public static int GetDefaultThreshold(string defName)
        {
            return DefaultThresholds.TryGetValue(defName, out int val) ? val : -1;
        }

        public int GetThresholdForSkill(TrainableDef td)
        {
            if (skillThresholds.TryGetValue(td.defName, out int val))
                return val;

            return GetDefaultThreshold(td.defName);
        }

        public void SetThresholdForSkill(TrainableDef td, int value)
        {
            skillThresholds[td.defName] = value;
        }

        /// <summary>
        /// Initialize defaults for any TrainableDefs not yet in the dictionary.
        /// Called when settings UI opens so all current defs are shown.
        /// </summary>
        public void EnsureAllSkillsPresent()
        {
            var allDefs = DefDatabase<TrainableDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                if (!skillThresholds.ContainsKey(allDefs[i].defName))
                {
                    skillThresholds[allDefs[i].defName] = GetDefaultThreshold(allDefs[i].defName);
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref trainingZoneName, "trainingZoneName", "@Training");
            Scribe_Values.Look(ref pollIntervalTicks, "pollIntervalTicks", 2000);
            Scribe_Collections.Look(ref skillThresholds, "skillThresholds", LookMode.Value, LookMode.Value);
            if (skillThresholds == null)
                skillThresholds = new Dictionary<string, int>();
            base.ExposeData();
        }
    }
}
