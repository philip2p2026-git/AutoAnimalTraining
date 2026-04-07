using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutoAnimalTraining
{
    public class MapComponent_AutoTraining : MapComponent
    {
        private Area trainingZone;
        private bool zoneWarningLogged;
        private int tickCounter;
        private bool diagnosticLogged;

        /// <summary>
        /// Stores each animal's original area restriction before we override it.
        /// Key = animal pawn, Value = original Area (null means unrestricted).
        /// </summary>
        private Dictionary<Pawn, Area> originalAreas = new Dictionary<Pawn, Area>();

        /// <summary>
        /// Tracks which animals have already been logged as routed.
        /// Prevents log spam from the route/release cooldown ping-pong cycle.
        /// </summary>
        private HashSet<Pawn> loggedAsRouted = new HashSet<Pawn>();

        // Save/load scratch fields — must persist across ExposeData phases (LoadingVars → ResolvingCrossRefs → PostLoadInit)
        private List<Pawn> saveLoad_trackedPawns;
        private List<string> saveLoad_originalAreaLabels;

        private static AutoAnimalTrainingSettings Settings => AutoAnimalTrainingMod.Settings;

        // Cached reflection field for Pawn_TrainingTracker.steps (internal)
        private static readonly FieldInfo StepsField =
            typeof(Pawn_TrainingTracker).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance);

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

            tickCounter++;
            if (tickCounter < Settings.pollIntervalTicks)
                return;
            tickCounter = 0;

            if (trainingZone == null)
                return;

            CheckAnimalsForRouting();
        }

        /// <summary>
        /// Called by Harmony patches when an area is created, deleted, or renamed.
        /// Immediately re-evaluates whether the training zone exists.
        /// </summary>
        public void Notify_AreaChanged()
        {
            Area previousZone = trainingZone;
            ResolveTrainingZone();

            // If zone was deleted, revert all tracked animals
            if (previousZone != null && trainingZone == null)
            {
                RevertAllAnimals();
            }
        }

        /// <summary>
        /// Save/load support. Persists tracked animals and their original areas across saves.
        /// Uses class fields (saveLoad_trackedPawns, saveLoad_originalAreaLabels) because
        /// ExposeData is called multiple times during loading (LoadingVars, ResolvingCrossRefs,
        /// PostLoadInit) and LookMode.Reference needs the list to persist between phases.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Prepare parallel lists for saving
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saveLoad_trackedPawns = new List<Pawn>(originalAreas.Keys);
                saveLoad_originalAreaLabels = new List<string>();
                foreach (var kvp in originalAreas)
                {
                    saveLoad_originalAreaLabels.Add(kvp.Value?.Label ?? "<<null>>");
                }
            }

            // Scribe calls — these persist across LoadingVars → ResolvingCrossRefs because the fields survive between ExposeData invocations
            Scribe_Collections.Look(ref saveLoad_trackedPawns, "trackedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref saveLoad_originalAreaLabels, "originalAreaLabels", LookMode.Value);

            // Reconstruct runtime state after all cross-refs are resolved
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                originalAreas = new Dictionary<Pawn, Area>();
                loggedAsRouted = new HashSet<Pawn>();

                if (saveLoad_trackedPawns != null && saveLoad_originalAreaLabels != null)
                {
                    for (int i = 0; i < saveLoad_trackedPawns.Count && i < saveLoad_originalAreaLabels.Count; i++)
                    {
                        Pawn pawn = saveLoad_trackedPawns[i];
                        if (pawn == null) continue; // Reference didn't resolve (sold/dead)

                        string areaLabel = saveLoad_originalAreaLabels[i];
                        Area restoredArea = null;
                        if (areaLabel != "<<null>>")
                        {
                            restoredArea = map.areaManager.GetLabeled(areaLabel);
                            if (restoredArea == null)
                            {
                                Log.Warning($"[AutoAnimalTraining] Could not find original area '{areaLabel}' for {pawn.LabelShort} — will release to unrestricted");
                            }
                        }

                        originalAreas[pawn] = restoredArea;
                        loggedAsRouted.Add(pawn); // They were already routed before save
                    }

                    int count = originalAreas.Count;
                    if (count > 0)
                    {
                        Log.Message($"[AutoAnimalTraining] Load complete — restored {count} tracked animals");
                    }
                }

                // Clear scratch fields
                saveLoad_trackedPawns = null;
                saveLoad_originalAreaLabels = null;
            }
        }

        private void CheckAnimalsForRouting()
        {
            var pawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            if (pawns == null)
                return;

            // One-time diagnostic: log each eligible animal's training state
            bool runDiagnostic = !diagnosticLogged;
            if (runDiagnostic)
            {
                diagnosticLogged = true;
                if (Settings.verboseLogging)
                {
                    Log.Message($"[AutoAnimalTraining] === Diagnostic: per-skill thresholds, StepsField={StepsField != null} ===");
                }
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!IsEligibleForAutoTraining(pawn))
                    continue;

                if (runDiagnostic && Settings.verboseLogging)
                {
                    LogAnimalDiagnostic(pawn);
                }

                bool alreadyRouted = originalAreas.ContainsKey(pawn);
                bool needsTraining = NeedsTraining(pawn);
                bool onCooldown = TrainableUtility.TrainedTooRecently(pawn);

                if (needsTraining && !onCooldown)
                {
                    // Needs training and can be trained now — route to zone
                    if (!alreadyRouted)
                    {
                        AssignToTrainingZone(pawn);
                    }
                }
                else if (alreadyRouted)
                {
                    // Either training complete OR on cooldown — release back
                    if (!needsTraining)
                    {
                        ReleaseFromTrainingZone(pawn, cooldown: false);
                    }
                    else if (onCooldown)
                    {
                        ReleaseFromTrainingZone(pawn, cooldown: true);
                    }
                }
            }

            // Clean up dead/despawned/sold animals from tracking
            CleanupStaleEntries();
        }

        private void LogAnimalDiagnostic(Pawn pawn)
        {
            var tracker = pawn.training;
            if (tracker == null)
            {
                Log.Message($"[AutoAnimalTraining]   {pawn.LabelShort}: training=null");
                return;
            }

            var stepsMap = StepsField?.GetValue(tracker) as DefMap<TrainableDef, int>;
            if (stepsMap == null)
            {
                Log.Message($"[AutoAnimalTraining]   {pawn.LabelShort}: stepsMap=null (reflection failed)");
                return;
            }

            var allTrainables = DefDatabase<TrainableDef>.AllDefsListForReading;
            string info = "";
            for (int i = 0; i < allTrainables.Count; i++)
            {
                TrainableDef td = allTrainables[i];
                bool wanted = tracker.GetWanted(td);
                int current = stepsMap[td];
                bool learned = tracker.HasLearned(td);
                int skillThreshold = Settings.GetThresholdForSkill(td);
                string thresholdStr = skillThreshold < 0 ? "off" : $"<={skillThreshold}";
                info += $"\n    {td.defName}: wanted={wanted}, steps={current}/{td.steps}, learned={learned}, trigger={thresholdStr}";
            }

            Log.Message($"[AutoAnimalTraining]   {pawn.LabelShort} ({pawn.kindDef?.label}):{info}");
        }

        private bool IsEligibleForAutoTraining(Pawn pawn)
        {
            return pawn.RaceProps.Animal
                && pawn.Spawned
                && pawn.playerSettings != null
                && pawn.playerSettings.SupportsAllowedAreas
                && pawn.training != null;
        }

        /// <summary>
        /// Returns true if any wanted trainable has steps &lt;= its per-skill threshold.
        /// A threshold of -1 means that skill is disabled and won't trigger routing.
        /// </summary>
        private bool NeedsTraining(Pawn pawn)
        {
            var tracker = pawn.training;
            if (tracker == null)
                return false;

            var stepsMap = StepsField?.GetValue(tracker) as DefMap<TrainableDef, int>;
            if (stepsMap == null)
                return false;

            var allTrainables = DefDatabase<TrainableDef>.AllDefsListForReading;

            for (int i = 0; i < allTrainables.Count; i++)
            {
                TrainableDef td = allTrainables[i];
                if (!tracker.GetWanted(td))
                    continue;

                int threshold = Settings.GetThresholdForSkill(td);
                if (threshold < 0)
                    continue; // This skill is disabled

                int currentSteps = stepsMap[td];
                if (currentSteps <= threshold && currentSteps < td.steps)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the first degraded trainable info for logging.
        /// </summary>
        private (TrainableDef trainable, int steps)? GetFirstDegradedTrainable(Pawn pawn)
        {
            var tracker = pawn.training;
            if (tracker == null)
                return null;

            var stepsMap = StepsField?.GetValue(tracker) as DefMap<TrainableDef, int>;
            if (stepsMap == null)
                return null;

            var allTrainables = DefDatabase<TrainableDef>.AllDefsListForReading;

            for (int i = 0; i < allTrainables.Count; i++)
            {
                TrainableDef td = allTrainables[i];
                if (!tracker.GetWanted(td))
                    continue;

                int threshold = Settings.GetThresholdForSkill(td);
                if (threshold < 0)
                    continue;

                int currentSteps = stepsMap[td];
                if (currentSteps <= threshold && currentSteps < td.steps)
                {
                    return (td, currentSteps);
                }
            }

            return null;
        }

        private void AssignToTrainingZone(Pawn pawn)
        {
            Area currentArea = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;

            // Don't re-assign if already in the training zone
            if (currentArea == trainingZone)
            {
                // Still track it so we can release later
                if (!originalAreas.ContainsKey(pawn))
                    originalAreas[pawn] = null;
                return;
            }

            // Save original area
            originalAreas[pawn] = currentArea;

            // Assign to training zone
            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = trainingZone;

            // Interrupt current job so pathfinding re-evaluates with new area
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);

            // Log only on first route (suppress repeated route/release ping-pong)
            bool alreadyLoggedRoute = loggedAsRouted.Contains(pawn);
            if (!alreadyLoggedRoute || Settings.verboseLogging)
            {
                var degraded = GetFirstDegradedTrainable(pawn);
                string trainableInfo = degraded.HasValue
                    ? $"{degraded.Value.trainable.LabelCap} at {degraded.Value.steps} step(s)"
                    : "training needed";
                string kindLabel = pawn.kindDef?.label ?? "Animal";

                Log.Message($"[AutoAnimalTraining] {pawn.LabelShort} ({kindLabel}) routed to Training Zone — {trainableInfo}");
                loggedAsRouted.Add(pawn);
            }
        }

        private void ReleaseFromTrainingZone(Pawn pawn, bool cooldown = false)
        {
            if (!originalAreas.TryGetValue(pawn, out Area originalArea))
                return;

            // Only restore if the animal is still in our training zone
            Area currentArea = pawn.playerSettings?.AreaRestrictionInPawnCurrentMap;
            if (currentArea == trainingZone)
            {
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = originalArea;
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }

            originalAreas.Remove(pawn);

            string kindLabel = pawn.kindDef?.label ?? "Animal";
            if (cooldown)
            {
                // Cooldown release: only log if verbose (this is the spammy one)
                if (Settings.verboseLogging)
                {
                    Log.Message($"[AutoAnimalTraining] {pawn.LabelShort} ({kindLabel}) released from Training Zone — on cooldown, will re-route when ready");
                }
            }
            else
            {
                // Training complete: always log and clear the logged-route tracker
                Log.Message($"[AutoAnimalTraining] {pawn.LabelShort} ({kindLabel}) released from Training Zone — training restored");
                loggedAsRouted.Remove(pawn);
            }
        }

        private void RevertAllAnimals()
        {
            if (originalAreas.Count == 0)
                return;

            foreach (var kvp in originalAreas.ToList())
            {
                Pawn pawn = kvp.Key;
                Area originalArea = kvp.Value;

                if (pawn?.playerSettings != null && pawn.Spawned)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = originalArea;
                }
            }

            int count = originalAreas.Count;
            originalAreas.Clear();
            loggedAsRouted.Clear();
            Log.Message($"[AutoAnimalTraining] Zone deleted — reverted {count} animals to original areas");
        }

        private void CleanupStaleEntries()
        {
            if (originalAreas.Count == 0)
                return;

            var staleKeys = originalAreas.Keys
                .Where(p => p == null || p.Dead || !p.Spawned || p.Faction != Faction.OfPlayer)
                .ToList();

            for (int i = 0; i < staleKeys.Count; i++)
            {
                originalAreas.Remove(staleKeys[i]);
                loggedAsRouted.Remove(staleKeys[i]);
            }
        }

        private void ResolveTrainingZone()
        {
            string zoneName = Settings.trainingZoneName;
            Area found = map.areaManager.GetLabeled(zoneName);

            if (found != null && trainingZone != found)
            {
                trainingZone = found;
                zoneWarningLogged = false;
                diagnosticLogged = false;

                int eligible = CountEligibleAnimals();
                Log.Message($"[AutoAnimalTraining] Training Zone '{zoneName}' found — monitoring {eligible} eligible animals");
                Messages.Message(
                    $"[AutoAnimalTraining] Training Zone '{zoneName}' found — monitoring {eligible} eligible animals",
                    MessageTypeDefOf.PositiveEvent, historical: false);
            }
            else if (found == null && trainingZone != null)
            {
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
                .Count(p => IsEligibleForAutoTraining(p));
        }
    }
}
