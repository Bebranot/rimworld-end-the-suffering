using RimWorld;
using UnityEngine;
using Verse;

namespace EndTheSuffering
{
    [StaticConstructorOnStartup]
    public class FloatMenuOptionProvider_EndSuffering : FloatMenuOptionProvider
    {
        private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/EndSuffering_Warning");

        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        // Uses GetSingleOption rather than GetSingleOptionFor(Pawn): for pawn targets the menu replaces the icon with the pawn.
        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            if (pawn == null || !context.ClickedPawns.Contains(pawn) || !SufferingUtility.ShouldOffer(pawn))
            {
                return null;
            }

            AcceptanceReport report = SufferingUtility.CanDoNow(pawn);
            if (!report.Accepted)
            {
                return new FloatMenuOption("EndSuffering_Disabled".Translate(report.Reason), null, Icon, Color.white);
            }

            return new FloatMenuOption(
                "EndSuffering_MenuLabel".Translate(),
                () => SufferingUtility.RequestConfirmation(pawn),
                Icon,
                Color.white);
        }
    }
}
