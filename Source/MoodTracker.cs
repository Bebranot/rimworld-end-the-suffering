using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace EndTheSuffering
{
    /// <summary>
    /// Remembers since when each player pawn has been continuously below the mood threshold.
    /// Holds entries only for pawns in that state, keyed by thingIDNumber so the save has no references to resolve.
    /// </summary>
    public class MoodTracker : GameComponent
    {
        public const int CheckGapTicks = 2500;
        private const int PurgeAfterTicks = GenDate.TicksPerDay * 3;

        public static MoodTracker Instance;

        private Dictionary<int, LowMoodEntry> entries = new Dictionary<int, LowMoodEntry>();
        private List<int> purgeBuffer;

        public MoodTracker(Game game)
        {
            Instance = this;
        }

        public void Observe(Pawn pawn, bool low)
        {
            int id = pawn.thingIDNumber;
            if (!low)
            {
                if (entries.Count > 0)
                {
                    entries.Remove(id);
                }
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (!entries.TryGetValue(id, out LowMoodEntry entry))
            {
                entries[id] = new LowMoodEntry { sinceTick = now, lastSeenTick = now };
                return;
            }

            // A long gap means the mood was not being updated (cryptosleep etc.), so the streak is unknown.
            if (now - entry.lastSeenTick > CheckGapTicks)
            {
                entry.sinceTick = now;
            }
            entry.lastSeenTick = now;
        }

        public bool LowLongEnough(Pawn pawn, int requiredTicks)
        {
            return entries.TryGetValue(pawn.thingIDNumber, out LowMoodEntry entry)
                && Find.TickManager.TicksGame - entry.sinceTick >= requiredTicks;
        }

        /// <summary>Dev tool: pretends the pawn has already waited out the required time.</summary>
        public void ForceLong(Pawn pawn, int ticks)
        {
            int now = Find.TickManager.TicksGame;
            entries[pawn.thingIDNumber] = new LowMoodEntry { sinceTick = now - ticks, lastSeenTick = now };
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % GenDate.TicksPerDay != 0 || entries.Count == 0)
            {
                return;
            }

            purgeBuffer ??= new List<int>();
            purgeBuffer.Clear();
            foreach (KeyValuePair<int, LowMoodEntry> pair in entries)
            {
                if (now - pair.Value.lastSeenTick > PurgeAfterTicks)
                {
                    purgeBuffer.Add(pair.Key);
                }
            }
            for (int i = 0; i < purgeBuffer.Count; i++)
            {
                entries.Remove(purgeBuffer[i]);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "entries", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && entries == null)
            {
                entries = new Dictionary<int, LowMoodEntry>();
            }
        }
    }

    public class LowMoodEntry : IExposable
    {
        public int sinceTick;
        public int lastSeenTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sinceTick, "since");
            Scribe_Values.Look(ref lastSeenTick, "seen");
        }
    }

    [HarmonyPatch(typeof(Need_Mood), nameof(Need_Mood.NeedInterval))]
    internal static class Patch_NeedMood_NeedInterval
    {
        private static void Postfix(Need_Mood __instance, Pawn ___pawn)
        {
            MoodTracker tracker = MoodTracker.Instance;
            if (tracker == null || ___pawn.Faction == null || !___pawn.Faction.IsPlayer)
            {
                return;
            }
            tracker.Observe(___pawn, __instance.CurLevelPercentage < EndTheSufferingMod.Settings.moodThreshold);
        }
    }
}
