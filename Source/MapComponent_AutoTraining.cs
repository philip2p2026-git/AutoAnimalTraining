using System.Linq;
using RimWorld;
using Verse;

namespace AutoAnimalTraining
{
    public class MapComponent_AutoTraining : MapComponent
    {
        private Area trainingZone;
        private bool zoneWarningLogged;
        private int tickCounter;

        private static AutoAnimalTrainingSettings Settings => AutoAnimalTrainingMod.Settings;

        public MapComponent_AutoTraining(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ResolveTrainingZone();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Configurable poll interval for future animal training checks
            tickCounter++;
            if (tickCounter < Settings.pollIntervalTicks)
                return;
            tickCounter = 0;

            // TODO Milestone 2: animal training degradation check will go here
        }

        /// <summary>
        /// Called by Harmony patches when an area is created, deleted, or renamed.
        /// Immediately re-evaluates whether the training zone exists.
        /// </summary>
        public void Notify_AreaChanged()
        {
            ResolveTrainingZone();
        }

        private void ResolveTrainingZone()
        {
            string zoneName = Settings.trainingZoneName;
            Area found = map.areaManager.GetLabeled(zoneName);

            if (found != null && trainingZone != found)
            {
                // Zone found (newly created or renamed to match)
                trainingZone = found;
                zoneWarningLogged = false;

                int eligible = CountEligibleAnimals();
                Log.Message($"[AutoAnimalTraining] Training Zone '{zoneName}' found — monitoring {eligible} eligible animals");
                Messages.Message(
                    $"[AutoAnimalTraining] Training Zone '{zoneName}' found — monitoring {eligible} eligible animals",
                    MessageTypeDefOf.PositiveEvent, historical: false);
            }
            else if (found == null && trainingZone != null)
            {
                // Zone was deleted or renamed away
                trainingZone = null;
                zoneWarningLogged = false;

                Log.Message($"[AutoAnimalTraining] Training Zone '{zoneName}' removed");
                Messages.Message(
                    $"[AutoAnimalTraining] Training Zone '{zoneName}' removed",
                    MessageTypeDefOf.NeutralEvent, historical: false);
            }
            else if (found == null && !zoneWarningLogged)
            {
                Log.Message($"[AutoAnimalTraining] Training Zone '{zoneName}' not found — create an area named '{zoneName}' to enable auto-routing");
                zoneWarningLogged = true;
            }
        }

        private int CountEligibleAnimals()
        {
            if (map.mapPawns == null)
                return 0;

            return map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Count(p => p.RaceProps.Animal
                    && p.playerSettings?.SupportsAllowedAreas == true);
        }
    }
}
