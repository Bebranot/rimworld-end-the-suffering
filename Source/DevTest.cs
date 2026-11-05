#if DEVTEST
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EndTheSuffering
{
    /// <summary>Automated in-game scenario. Compiled only with -p:DevTest=true, never shipped.</summary>
    public class DevTestComponent : GameComponent
    {
        private const string Tag = "[ETS-TEST] ";
        private const string SaveName = "ets_midanim";

        private int stage;
        private int stageTick;
        private bool savedMidAnim;
        private Pawn current;
        private readonly Queue<string> guns = new Queue<string>(new[] { "Gun_Autopistol", "Gun_AssaultRifle", "Gun_BoltActionRifle", "Gun_SniperRifle", "Gun_PumpShotgun" });
        private string currentGun;
        private Pawn menuPawn;
        private bool menuDeathPaused;
        private ThingWithComps currentWeapon;
        private int roundsBefore;

        private static bool Visual => System.IO.File.Exists(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "ets_visual.flag"));

        public DevTestComponent(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref savedMidAnim, "savedMidAnim");
            Scribe_References.Look(ref current, "current");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && savedMidAnim)
            {
                stage = 50;
            }
        }

        private static void Report(string name, bool pass, string detail = "")
        {
            Log.Message(Tag + (pass ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " :: " + detail : ""));
        }

        private static Map Map => Find.CurrentMap;

        private static Pawn MakeAdult(string gun, float age = 30f)
        {
            var request = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, fixedBiologicalAge: age, fixedChronologicalAge: age,
                allowDead: false, allowDowned: false);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            // Map center can be solid rock under fog, so stand next to a real colonist instead.
            Pawn anchor = Map.mapPawns.FreeColonistsSpawned.First();
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(anchor.Position, Map, 5), Map);
            if (gun != null)
            {
                pawn.equipment.DestroyAllEquipment();
                var weapon = (ThingWithComps)ThingMaker.MakeThing(ThingDef.Named(gun));
                LoadCeMagazine(weapon);
                pawn.equipment.AddEquipment(weapon);
            }
            pawn.needs.mood.CurLevelPercentage = 0f;
            return pawn;
        }

        private static ThingComp CeAmmo(ThingWithComps weapon)
        {
            return weapon.AllComps.FirstOrDefault(c => c.GetType().FullName == "CombatExtended.CompAmmoUser");
        }

        // Under CE a freshly made gun has an empty magazine.
        private static void LoadCeMagazine(ThingWithComps weapon)
        {
            ThingComp ammo = CeAmmo(weapon);
            if (ammo != null)
            {
                HarmonyLib.AccessTools.Method(ammo.GetType(), "ResetAmmoCount").Invoke(ammo, new object[] { null });
            }
        }

        private static int CeRounds(ThingWithComps weapon)
        {
            ThingComp ammo = CeAmmo(weapon);
            return ammo == null ? -1 : (int)HarmonyLib.AccessTools.PropertyGetter(ammo.GetType(), "CurMagCount").Invoke(ammo, null);
        }

        private static void MakeEligible(Pawn pawn)
        {
            MoodTracker.Instance.ForceLong(pawn, Mathf.RoundToInt(EndTheSufferingMod.Settings.requiredHours * GenDate.TicksPerHour));
        }

        private static void LogFaceState(Pawn pawn, int elapsed)
        {
            ThingComp comp = pawn.AllComps.Find(c => c.GetType().Name == "FacialAnimationControllerComp");
            if (comp == null)
            {
                Log.Message(Tag + "face t=" + elapsed + " no FA controller");
                return;
            }
            object param = HarmonyLib.AccessTools.Field(comp.GetType(), "animationParam")?.GetValue(comp);
            object job = param == null ? null : HarmonyLib.AccessTools.Field(param.GetType(), "currentJobName")?.GetValue(param);
            var list = HarmonyLib.AccessTools.Field(comp.GetType(), "currentJobAnimationList")?.GetValue(comp) as System.Collections.IEnumerable;
            var names = new System.Text.StringBuilder();
            if (list != null)
            {
                foreach (object animation in list)
                {
                    object def = HarmonyLib.AccessTools.Field(animation.GetType(), "animationDef")?.GetValue(animation);
                    names.Append((def as Def)?.defName).Append(' ');
                }
            }
            foreach (string part in new[] { "LidControllerComp", "MouthControllerComp", "BrowControllerComp" })
            {
                ThingComp shapeComp = pawn.AllComps.Find(c => c.GetType().Name == part);
                object shape = shapeComp == null ? null : HarmonyLib.Traverse.Create(shapeComp).Property("Shape").GetValue();
                shape ??= shapeComp == null ? null : HarmonyLib.Traverse.Create(shapeComp).Field("Shape").GetValue();
                names.Append(part).Append('=').Append((shape as Def)?.defName ?? "null").Append(' ');
            }
            Find.Selector.ClearSelection();
            Find.CameraDriver.SetRootPosAndSize(pawn.DrawPos, 1.6f);
            Log.Message(Tag + "face t=" + elapsed + " job=" + job + " anims=" + names);
        }

        private static void Start(Pawn pawn)
        {
            Job job = JobMaker.MakeJob(EndSufferingDefOf.EndSuffering_Job, pawn.equipment.Primary);
            job.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override void GameComponentTick()
        {
            if (Map == null || MoodTracker.Instance == null)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            switch (stage)
            {
                case 0:
                    if (stageTick == 0) { stageTick = now; }
                    if (now - stageTick > 120)
                    {
                        if (Visual && menuPawn == null)
                        {
                            // Show Me Your Hands installs its weapon patch from the main menu, which -quicktest skips.
                            HarmonyLib.AccessTools.Method("ShowMeYourHands.RimWorld_MainMenuDrawer_MainMenuOnGUI:MainMenuOnGUI")?.Invoke(null, null);
                            string flagText = System.IO.File.ReadAllText(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "ets_visual.flag")).Trim();
                            menuPawn = MakeAdult(flagText.Length > 0 ? flagText : "Gun_Revolver");
                            MakeEligible(menuPawn);
                            Find.Selector.ClearSelection();
                            Find.Selector.Select(menuPawn);
                            Find.CameraDriver.config.sizeRange = new FloatRange(2f, 60f);
                            Find.CameraDriver.SetRootPosAndSize(menuPawn.DrawPos, 2.5f);
                            Find.TickManager.Pause();
                            Log.Message(Tag + "MENU READY");
                            break;
                        }
                        if (menuPawn != null && !menuPawn.Dead)
                        {
                            if (menuPawn.CurJobDef == EndSufferingDefOf.EndSuffering_Job && menuPawn.jobs.curDriver is JobDriver_EndSuffering d
                                && d.aimStartTick > 0)
                            {
                                int elapsed = now - d.aimStartTick;
                                if (elapsed == 100 || elapsed == 200 || elapsed == 285)
                                {
                                    Find.TickManager.Pause();
                                    LogFaceState(menuPawn, elapsed);
                                }
                            }
                            MakeEligible(menuPawn);
                            break;
                        }
                        if (menuPawn != null && !menuDeathPaused)
                        {
                            menuDeathPaused = true;
                            Find.TickManager.Pause();
                            break;
                        }
                        RunStaticChecks();
                        stage = 10;
                    }
                    break;

                case 10:
                    if (guns.Count == 0) { StartCancelTest(); stage = 30; break; }
                    currentGun = guns.Dequeue();
                    current = MakeAdult(currentGun);
                    if (currentGun == "Gun_AssaultRifle")
                    {
                        // Shield belt and helmet must not change the outcome.
                        current.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_ShieldBelt"), null), false);
                        current.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_SimpleHelmet"), ThingDefOf.Steel), false);
                    }
                    MakeEligible(current);
                    currentWeapon = current.equipment.Primary;
                    roundsBefore = CeRounds(currentWeapon);
                    Log.Message(Tag + "gun " + currentGun + " tier " + SufferingUtility.TierOf(currentWeapon) + " rounds=" + roundsBefore);
                    Start(current);
                    stageTick = now;
                    stage = 20;
                    break;

                case 20:
                    if (now - stageTick == 240)
                    {
                        Find.CameraDriver.JumpToCurrentMapLoc(current.Position);
                        Log.Message(Tag + "POSE " + currentGun + " rot=" + current.Rotation + " job=" + current.CurJobDef?.defName);
                        if (Visual)
                        {
                            Find.CameraDriver.SetRootPosAndSize(current.DrawPos, 8f);
                            Find.TickManager.Pause();
                        }
                    }
                    if (current.Dead)
                    {
                        ReportDeath();
                        stage = 10;
                    }
                    else if (now - stageTick > 500)
                    {
                        Report("death " + currentGun, false, "still alive, job=" + current.CurJobDef?.defName);
                        stage = 10;
                    }
                    break;

                case 30:
                    if (now - stageTick == 60)
                    {
                        current.drafter.Drafted = true;
                    }
                    if (now - stageTick == 120)
                    {
                        Report("cancel by drafting", !current.Dead && current.CurJobDef != EndSufferingDefOf.EndSuffering_Job, "job=" + current.CurJobDef?.defName);
                        current = MakeAdult("Gun_Autopistol");
                        MakeEligible(current);
                        Start(current);
                        stageTick = now;
                        stage = 40;
                    }
                    break;

                case 40:
                    if (now - stageTick == 100)
                    {
                        savedMidAnim = true;
                        GameDataSaveLoader.SaveGame(SaveName);
                        Log.Message(Tag + "saved mid animation, loading");
                        stage = 41;
                        LongEventHandler.ExecuteWhenFinished(() => GameDataSaveLoader.LoadGame(SaveName));
                    }
                    break;

                case 50:
                    if (stageTick == 0)
                    {
                        stageTick = now;
                        var driver = current?.jobs?.curDriver as JobDriver_EndSuffering;
                        Report("save/load keeps animation", driver != null && driver.aimStartTick > 0, "driver=" + (driver != null));
                    }
                    if (current != null && current.Dead)
                    {
                        Report("death after load", true);
                        Finish();
                        stage = 99;
                    }
                    else if (now - stageTick > 500)
                    {
                        Report("death after load", false);
                        Finish();
                        stage = 99;
                    }
                    break;
            }
        }

        private void StartCancelTest()
        {
            current = MakeAdult("Gun_Autopistol");
            MakeEligible(current);
            Start(current);
            stageTick = Find.TickManager.TicksGame;
        }

        private void ReportDeath()
        {
            Corpse corpse = current.Corpse;
            string missing = string.Join(",", current.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Select(h => h.Part.def.defName));
            Report("death " + currentGun, true, "missing=" + missing + " corpse=" + (corpse != null));
            Report("weapon dropped " + currentGun, currentWeapon.Spawned);
            if (roundsBefore >= 0)
            {
                Report("CE round used " + currentGun, CeRounds(currentWeapon) == roundsBefore - 1, roundsBefore + "->" + CeRounds(currentWeapon));
            }
        }

        private void RunStaticChecks()
        {
            Pawn child = MakeAdult("Gun_Autopistol", 10f);
            MakeEligible(child);
            Report("child never offered", !SufferingUtility.ShouldOffer(child), "age=" + child.ageTracker.AgeBiologicalYears);
            child.Destroy();

            Pawn teen = MakeAdult("Gun_Autopistol", 16f);
            MakeEligible(teen);
            Report("teen never offered", !SufferingUtility.ShouldOffer(teen), "age=" + teen.ageTracker.AgeBiologicalYears);
            teen.Destroy();

            Pawn waiting = MakeAdult("Gun_Autopistol");
            Report("not offered before 24h", !SufferingUtility.ShouldOffer(waiting));
            MakeEligible(waiting);
            Report("offered after 24h", SufferingUtility.ShouldOffer(waiting));

            List<FloatMenuOption> options = FloatMenuMakerMap.GetOptions(new List<Pawn> { waiting }, waiting.DrawPos, out FloatMenuContext _);
            FloatMenuOption option = options.FirstOrDefault(o => o.Label == "EndSuffering_MenuLabel".Translate());
            Report("right click shows option", option != null, "options=" + options.Count);
            var iconField = HarmonyLib.AccessTools.Field(typeof(FloatMenuOption), "iconTex");
            Report("option has warning icon", option != null && iconField.GetValue(option) != null && option.iconThing == null);
            Report("option enabled", option != null && !option.Disabled);

            var emptyGun = (ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("Gun_Autopistol"));
            if (CeAmmo(emptyGun) != null)
            {
                waiting.equipment.DestroyAllEquipment();
                waiting.equipment.AddEquipment(emptyGun);
                AcceptanceReport emptyReport = SufferingUtility.CanDoNow(waiting);
                Report("rejected: empty CE magazine", !emptyReport.Accepted, emptyReport.Reason);
            }

            foreach (string name in new[] { "Bow_Short", null })
            {
                waiting.equipment.DestroyAllEquipment();
                if (name != null)
                {
                    waiting.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(ThingDef.Named(name)));
                }
                AcceptanceReport report = SufferingUtility.CanDoNow(waiting);
                Report("rejected: " + (name ?? "unarmed"), !report.Accepted, report.Reason);
            }
            waiting.Destroy();

            Pawn perf = MakeAdult("Gun_Autopistol");
            Need_Mood mood = perf.needs.mood;
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
            {
                MoodTracker.Instance.Observe(perf, mood.CurLevelPercentage < EndTheSufferingMod.Settings.moodThreshold);
            }
            clock.Stop();
            Log.Message(Tag + "tracker cost " + (clock.Elapsed.TotalMilliseconds).ToString("F3") + " ns/call (1M calls, low mood worst case)");
            perf.Destroy();
        }

        private static void Finish()
        {
            Log.Message(Tag + "DONE");
        }
    }
}
#endif
