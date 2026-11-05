using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace EndTheSuffering
{
    /// <summary>
    /// Optional [NL] Facial Animation access through reflection. On RimWorld 1.7 that mod's own per-draw update never
    /// runs, so nothing would play the face animation. This drives it for the one pawn in the job, and only then.
    /// FA ignores a second update in the same tick, so this is harmless where its own update does run.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class FacialAnimationBridge
    {
        private static readonly Type ControllerType;
        private static readonly MethodInfo UpdateStatus;
        private static readonly MethodInfo UpdateAnimation;
        private static readonly MethodInfo AnimationParamGetter;
        private static readonly FieldInfo CurrentJobName;
        private static readonly FieldInfo NeedReset;
        private static bool failed;

        static FacialAnimationBridge()
        {
            ControllerType = AccessTools.TypeByName("FacialAnimation.FacialAnimationControllerComp");
            Type paramType = AccessTools.TypeByName("FacialAnimation.FacialAnimationParam");
            if (ControllerType == null || paramType == null)
            {
                return;
            }
            UpdateStatus = AccessTools.Method(ControllerType, "UpdateStatus");
            UpdateAnimation = AccessTools.Method(ControllerType, "UpdateAnimation");
            AnimationParamGetter = AccessTools.PropertyGetter(ControllerType, "AnimationParam");
            CurrentJobName = AccessTools.Field(paramType, "currentJobName");
            NeedReset = AccessTools.Field(paramType, "needUpdateJobWithReset");
        }

        private static ThingComp Controller(Pawn pawn)
        {
            if (failed || ControllerType == null || UpdateStatus == null || UpdateAnimation == null)
            {
                return null;
            }
            foreach (ThingComp comp in pawn.AllComps)
            {
                if (comp.GetType() == ControllerType)
                {
                    return comp;
                }
            }
            return null;
        }

        /// <summary>Called every tick of the aim toil.</summary>
        public static void Tick(Pawn pawn)
        {
            ThingComp controller = Controller(pawn);
            if (controller == null)
            {
                return;
            }
            try
            {
                UpdateStatus.Invoke(controller, new object[] { Find.TickManager.TicksGame });
                UpdateAnimation.Invoke(controller, null);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Puts the idle face back after a cancelled job; otherwise the last frame would stay on 1.7.</summary>
        public static void Restore(Pawn pawn)
        {
            ThingComp controller = Controller(pawn);
            if (controller == null || AnimationParamGetter == null || CurrentJobName == null || NeedReset == null)
            {
                return;
            }
            try
            {
                object param = AnimationParamGetter.Invoke(controller, null);
                CurrentJobName.SetValue(param, "");
                NeedReset.SetValue(param, true);
                UpdateAnimation.Invoke(controller, null);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static void Fail(Exception e)
        {
            failed = true;
            Log.Warning("[End the Suffering] Facial Animation link disabled: " + e.GetBaseException().Message);
        }
    }
}
