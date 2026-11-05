using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace EndTheSuffering
{
    public enum GoreTier
    {
        Small,
        Medium,
        Large
    }

    [DefOf]
    public static class EndSufferingDefOf
    {
        public static JobDef EndSuffering_Job;
        public static ThoughtDef EndSuffering_ColonistEndedSuffering;
        public static ThingDef EndSuffering_Filth_BrainMatter;
        public static ThingDef EndSuffering_Filth_BoneFragments;
        public static FleckDef EndSuffering_BloodMist;

        static EndSufferingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EndSufferingDefOf));
        }
    }

    public static class SufferingUtility
    {
        public const int MinimumAge = 18;

        /// <summary>The mood condition plus who may do this at all. False hides the menu entry entirely.</summary>
        public static bool ShouldOffer(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || !pawn.IsColonistPlayerControlled
                || !pawn.RaceProps.Humanlike || pawn.IsMutant || pawn.IsQuestLodger()
                || pawn.needs?.mood == null || pawn.equipment == null)
            {
                return false;
            }

            // Hard rule, deliberately not a setting: children and teenagers are never eligible.
            if (pawn.ageTracker.AgeBiologicalYears < MinimumAge || pawn.DevelopmentalStage != DevelopmentalStage.Adult)
            {
                return false;
            }

            if (DebugSettings.godMode)
            {
                return true;
            }

            int required = Mathf.RoundToInt(EndTheSufferingMod.Settings.requiredHours * GenDate.TicksPerHour);
            return MoodTracker.Instance != null && MoodTracker.Instance.LowLongEnough(pawn, required);
        }

        /// <summary>Why an eligible pawn cannot do it right now. Accepted means the job may start.</summary>
        public static AcceptanceReport CanDoNow(Pawn pawn)
        {
            if (pawn.InMentalState)
            {
                return "EndSuffering_MentalState".Translate();
            }
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return "EndSuffering_NoHands".Translate();
            }
            if (pawn.health.hediffSet.HasPreventsDeath)
            {
                return "EndSuffering_CannotDie".Translate();
            }
            if (HeadPart(pawn) == null)
            {
                return "EndSuffering_NoHead".Translate();
            }
            return WeaponReport(pawn.equipment.Primary);
        }

        public static AcceptanceReport WeaponReport(ThingWithComps weapon)
        {
            if (weapon == null || !weapon.def.IsRangedWeapon)
            {
                return "EndSuffering_NoFirearm".Translate();
            }
            if (!IsFirearm(weapon))
            {
                return "EndSuffering_NotFirearm".Translate();
            }
            if (!CombatExtendedBridge.HasRoundReady(weapon))
            {
                return "EndSuffering_NoAmmo".Translate();
            }
            return true;
        }

        private static VerbProperties VerbPropsOf(ThingWithComps weapon)
        {
            return weapon.GetComp<CompEquippable>()?.PrimaryVerb?.verbProps;
        }

        private static ThingDef ProjectileOf(ThingWithComps weapon)
        {
            return CombatExtendedBridge.LoadedProjectile(weapon) ?? VerbPropsOf(weapon)?.defaultProjectile;
        }

        public static bool IsFirearm(ThingWithComps weapon)
        {
            VerbProperties verb = VerbPropsOf(weapon);
            ProjectileProperties projectile = ProjectileOf(weapon)?.projectile;
            if (verb == null || projectile?.damageDef == null || verb.ForcedMissRadius > 0f || projectile.explosionRadius > 0f)
            {
                return false;
            }
            return projectile.damageDef == DamageDefOf.Bullet || projectile.damageDef.hediff?.defName == "Gunshot";
        }

        public static GoreTier TierOf(ThingWithComps weapon)
        {
            ThingDef projectileDef = ProjectileOf(weapon);
            ProjectileProperties projectile = projectileDef?.projectile;
            if (projectile == null)
            {
                return GoreTier.Medium;
            }
            if (IsShotgun(weapon, projectileDef))
            {
                return GoreTier.Large;
            }

            int damage = projectile.GetDamageAmount(weapon);
            if (damage >= EndTheSufferingMod.Settings.largeDamage)
            {
                return GoreTier.Large;
            }
            return damage >= EndTheSufferingMod.Settings.mediumDamage ? GoreTier.Medium : GoreTier.Small;
        }

        private static bool IsShotgun(ThingWithComps weapon, ThingDef projectileDef)
        {
            // CE exposes the pellet count on its ProjectilePropertiesCE subclass.
            var pelletField = HarmonyLib.AccessTools.Field(projectileDef.projectile.GetType(), "pelletCount");
            if (pelletField != null && pelletField.GetValue(projectileDef.projectile) is int pellets && pellets > 1)
            {
                return true;
            }
            return Mentions(weapon.def.defName) || Mentions(projectileDef.defName);

            static bool Mentions(string name) => name.IndexOf("shotgun", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static BodyPartRecord HeadPart(Pawn pawn)
        {
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == BodyPartDefOf.Head)
                {
                    return part;
                }
            }
            return null;
        }

        private static BodyPartRecord NamedPart(Pawn pawn, string defName)
        {
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def.defName == defName)
                {
                    return part;
                }
            }
            return null;
        }

        /// <summary>Starts the job after a confirmation, rechecking everything because the game may run meanwhile.</summary>
        public static void RequestConfirmation(Pawn pawn)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "EndSuffering_ConfirmText".Translate(pawn.Named("PAWN")),
                () =>
                {
                    if (ShouldOffer(pawn) && CanDoNow(pawn).Accepted)
                    {
                        Job job = JobMaker.MakeJob(EndSufferingDefOf.EndSuffering_Job, pawn.equipment.Primary);
                        job.playerForced = true;
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                },
                destructive: true));
        }

        public static void Fire(Pawn pawn, ThingWithComps weapon)
        {
            Map map = pawn.Map;
            IntVec3 cell = pawn.Position;
            Vector3 headPos = pawn.DrawPos + pawn.Drawer.renderer.BaseHeadOffsetAt(Rot4.South);
            GoreTier tier = TierOf(weapon);
            VerbProperties verb = VerbPropsOf(weapon);
            ThingDef bloodDef = pawn.RaceProps.BloodDef;

            CombatExtendedBridge.ConsumeRound(weapon);

            verb?.soundCast?.PlayOneShot(new TargetInfo(cell, map));
            verb?.soundCastTail?.PlayOneShot(new TargetInfo(cell, map));
            FleckMaker.Static(headPos + new Vector3(-0.15f, 0f, 0f), map, FleckDefOf.ShotFlash, tier == GoreTier.Large ? 12f : 8f);
            FleckMaker.ThrowSmoke(headPos, map, 0.5f);
            if (bloodDef != null)
            {
                SpawnGore(map, cell, headPos, tier, bloodDef, pawn.LabelIndefinite());
            }

            pawn.forceNoDeathNotification = true;
            Hediff lethal = DestroyPart(pawn, tier);
            if (!pawn.Dead)
            {
                pawn.Kill(null, lethal);
            }
            pawn.forceNoDeathNotification = false;

            if (!pawn.Dead)
            {
                Log.Warning("[End the Suffering] " + pawn.LabelShort + " survived; something prevents death.");
                return;
            }

            if (EndTheSufferingMod.Settings.colonistThought)
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
                {
                    colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(EndSufferingDefOf.EndSuffering_ColonistEndedSuffering);
                }
            }

            Find.LetterStack.ReceiveLetter(
                "EndSuffering_LetterLabel".Translate(pawn.Named("PAWN")),
                "EndSuffering_LetterText".Translate(pawn.Named("PAWN")),
                LetterDefOf.Death,
                new LookTargets(pawn.Corpse != null ? (Thing)pawn.Corpse : null));
        }

        /// <summary>
        /// Adds the missing part directly instead of dealing damage, so shields, armor and CE's damage
        /// rework cannot change the outcome, and no killer gets attributed.
        /// </summary>
        private static Hediff DestroyPart(Pawn pawn, GoreTier tier)
        {
            BodyPartRecord part = null;
            if (tier == GoreTier.Large)
            {
                part = HeadPart(pawn);
            }
            if (part == null && tier != GoreTier.Small)
            {
                part = NamedPart(pawn, "Skull");
            }
            part ??= pawn.health.hediffSet.GetBrain() ?? HeadPart(pawn);
            if (part == null)
            {
                return null;
            }

            var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, part);
            missing.lastInjury = DefDatabase<HediffDef>.GetNamedSilentFail("Gunshot");
            missing.IsFresh = true;
            pawn.health.AddHediff(missing, part);
            return missing;
        }

        private static void SpawnGore(Map map, IntVec3 cell, Vector3 headPos, GoreTier tier, ThingDef bloodDef, string source)
        {
            int blood = tier == GoreTier.Large ? 16 : tier == GoreTier.Medium ? 9 : 3;
            int brain = tier == GoreTier.Large ? 7 : tier == GoreTier.Medium ? 3 : 0;
            int bone = tier == GoreTier.Large ? 4 : 0;
            float radius = tier == GoreTier.Large ? 3.9f : tier == GoreTier.Medium ? 2.9f : 1.9f;
            int mist = tier == GoreTier.Large ? 14 : tier == GoreTier.Medium ? 8 : 3;

            Scatter(map, cell, bloodDef, blood, radius, source);
            Scatter(map, cell, EndSufferingDefOf.EndSuffering_Filth_BrainMatter, brain, radius, source);
            Scatter(map, cell, EndSufferingDefOf.EndSuffering_Filth_BoneFragments, bone, radius, source);

            for (int i = 0; i < mist; i++)
            {
                // The shot enters from the west side of the head, so everything sprays east.
                FleckCreationData data = FleckMaker.GetDataStatic(headPos, map, EndSufferingDefOf.EndSuffering_BloodMist, Rand.Range(0.3f, 0.9f));
                data.velocityAngle = Rand.Range(50f, 130f);
                data.velocitySpeed = Rand.Range(0.6f, 2.6f);
                data.rotationRate = Rand.Range(-60f, 60f);
                map.flecks.CreateFleck(data);
            }
        }

        private static void Scatter(Map map, IntVec3 center, ThingDef filth, int count, float radius, string source)
        {
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    IntVec3 target = center + new IntVec3(
                        Rand.RangeInclusive(0, Mathf.CeilToInt(radius)),
                        0,
                        Rand.RangeInclusive(-Mathf.FloorToInt(radius * 0.6f), Mathf.FloorToInt(radius * 0.6f)));
                    if (target.InBounds(map) && target.Walkable(map) && GenSight.LineOfSight(center, target, map))
                    {
                        FilthMaker.TryMakeFilth(target, map, filth, source);
                        break;
                    }
                }
            }
        }
    }
}
