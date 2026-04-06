using System.Linq;
using RimWorld;
using Verse;

namespace AutoAnimalTraining
{
    public class MapComponent_AutoTraining : MapComponent
    {
        private const string TrainingZoneName = "Training";

        private Area trainingZone;
        private bool zoneWarningLogged;

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

            // Run once every 2000 ticks (~33 seconds) on a TickLong-like cadence
            if (Find.TickManager.TicksGame % 2000 != 0)
                return;

            ResolveTrainingZone();
        }

        private void ResolveTrainingZone()
        {
            Area found = map.areaManager.GetLabeled(TrainingZoneName);

            if (found != null && trainingZone != found)
            {
                trainingZone = found;
                zoneWarningLogged = false;

                int eligible = CountEligibleAnimals();
                Log.Message($"[AutoAnimalTraining] Training Zone '{TrainingZoneName}' found — monitoring {eligible} eligible animals");
            }
            else if (found == null && trainingZone != null)
            {
                // Zone was deleted
                trainingZone = null;
                zoneWarningLogged = false;
                Log.Message($"[AutoAnimalTraining] Training Zone '{TrainingZoneName}' removed");
            }
            else if (found == null && !zoneWarningLogged)
            {
                Log.Message($"[AutoAnimalTraining] Training Zone '{TrainingZoneName}' not found — create an area named '{TrainingZoneName}' to enable auto-routing");
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
