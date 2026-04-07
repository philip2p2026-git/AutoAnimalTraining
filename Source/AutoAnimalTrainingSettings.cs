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
        public int GetThresholdForSkill(TrainableDef td)
        {
            if (skillThresholds.TryGetValue(td.defName, out int val))
                return val;

            // Default: enable Tameness at threshold 1, all others disabled (-1)
            return td.defName == "Tameness" ? 1 : -1;
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
                    skillThresholds[allDefs[i].defName] = allDefs[i].defName == "Tameness" ? 1 : -1;
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
