using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace EndTheSuffering
{
    public static class DevTools
    {
        [DebugAction("End the Suffering", "Set mood to 0 and skip wait", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeEligible(Pawn pawn)
        {
            if (pawn.needs?.mood == null || MoodTracker.Instance == null)
            {
                return;
            }
            pawn.needs.mood.CurLevelPercentage = 0f;
            MoodTracker.Instance.ForceLong(pawn, Mathf.RoundToInt(EndTheSufferingMod.Settings.requiredHours * GenDate.TicksPerHour));
        }
    }
}
