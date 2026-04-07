using Verse;

namespace AutoAnimalTraining
{
    public class AutoAnimalTrainingSettings : ModSettings
    {
        public string trainingZoneName = "@Training";
        public int pollIntervalTicks = 2000;
        public int stepsThreshold = 1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref trainingZoneName, "trainingZoneName", "@Training");
            Scribe_Values.Look(ref pollIntervalTicks, "pollIntervalTicks", 2000);
            Scribe_Values.Look(ref stepsThreshold, "stepsThreshold", 1);
            base.ExposeData();
        }
    }
}
