using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace EndTheSuffering
{
    public class JobDriver_EndSuffering : JobDriver
    {
        public const int RaiseTicks = 210;
        public const int HoldTicks = 90;

        public int aimStartTick = -1;

        private ThingWithComps Weapon => job.targetA.Thing as ThingWithComps;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref aimStartTick, "aimStartTick", -1);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition =>
            {
                if (!pawn.Dead)
                {
                    FacialAnimationBridge.Restore(pawn);
                }
                if (condition != JobCondition.Succeeded && !pawn.Dead && pawn.Spawned)
                {
                    Messages.Message("EndSuffering_Cancelled".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            });

            this.FailOn(() => pawn.Downed || pawn.InMentalState || pawn.equipment?.Primary == null || pawn.equipment.Primary != Weapon);

            Toil aim = ToilMaker.MakeToil("EndSuffering_Aim");
            aim.initAction = () =>
            {
                aimStartTick = Find.TickManager.TicksGame;
                pawn.pather.StopDead();
                pawn.stances.CancelBusyStanceSoft();
                // A carried item would stop the weapon from being drawn at all.
                if (pawn.carryTracker?.CarriedThing != null)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _);
                }
                pawn.Rotation = Rot4.South;
            };
            aim.tickAction = () =>
            {
                pawn.Rotation = Rot4.South;
                FacialAnimationBridge.Tick(pawn);
            };
            aim.handlingFacing = true;
            aim.defaultCompleteMode = ToilCompleteMode.Delay;
            aim.defaultDuration = RaiseTicks + HoldTicks;
            yield return aim;

            Toil fire = ToilMaker.MakeToil("EndSuffering_Fire");
            fire.initAction = () =>
            {
                ThingWithComps weapon = Weapon;
                if (!SufferingUtility.WeaponReport(weapon).Accepted)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                SufferingUtility.Fire(pawn, weapon);
            };
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return fire;
        }
    }
}
