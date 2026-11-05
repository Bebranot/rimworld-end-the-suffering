using HarmonyLib;
using UnityEngine;
using Verse;

namespace EndTheSuffering
{
    /// <summary>
    /// Changes where the weapon is drawn by editing the arguments of the vanilla draw method. Show Me Your Hands and
    /// Combat Extended both read those arguments, so hands and CE's own weapon drawing follow without any direct link.
    /// Priority.First is what makes that work: they must see the edited values.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    internal static class Patch_DrawEquipmentAiming
    {
        private const float EndAimAngle = 72f;
        private const float StartAimAngle = 143f;

        // Show Me Your Hands ignores a weapon further than 0.9 cells from the pawn.
        private const float MaxWeaponOffset = 0.82f;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Thing eq, ref Vector3 drawLoc, ref float aimAngle)
        {
            if (!(eq.ParentHolder is Pawn_EquipmentTracker tracker)
                || !(tracker.pawn?.jobs?.curDriver is JobDriver_EndSuffering driver)
                || driver.aimStartTick < 0)
            {
                return;
            }

            Pawn pawn = tracker.pawn;
            int elapsed = Find.TickManager.TicksGame - driver.aimStartTick;
            float progress = Mathf.Clamp01(elapsed / (float)JobDriver_EndSuffering.RaiseTicks);
            float eased = progress * progress * (3f - 2f * progress);

            // Shaking grows as the gun comes up. Driven by game ticks: it pauses with the game and costs nothing.
            float phase = elapsed + (pawn.thingIDNumber % 97);
            float shakeNoise = Mathf.Sin(phase * 0.93f) + 0.6f * Mathf.Sin(phase * 2.17f) + 0.35f * Mathf.Sin(phase * 3.71f);
            float shakeAngle = Mathf.Lerp(0.8f, 3.2f, eased) * shakeNoise;
            float shakeOffset = Mathf.Lerp(0.004f, 0.014f, eased);

            float targetAim = EndAimAngle;
            Vector3 direction = Vector3.forward.RotatedBy(targetAim);
            Vector3 head = pawn.DrawPos + pawn.Drawer.renderer.BaseHeadOffsetAt(Rot4.South);
            Vector3 muzzle = head + new Vector3(-0.16f, 0f, -0.02f);
            Vector3 target = muzzle - direction * (0.42f * eq.Graphic.drawSize.x);

            Vector3 offset = target - pawn.DrawPos;
            offset.y = 0f;
            if (offset.sqrMagnitude > MaxWeaponOffset * MaxWeaponOffset)
            {
                offset = offset.normalized * MaxWeaponOffset;
            }
            target = pawn.DrawPos + offset;
            target.y = drawLoc.y;

            drawLoc = Vector3.Lerp(drawLoc, target, eased);
            drawLoc.x += shakeOffset * Mathf.Sin(phase * 1.31f);
            drawLoc.z += shakeOffset * Mathf.Sin(phase * 1.73f);
            aimAngle = Mathf.Lerp(StartAimAngle, targetAim, eased) + shakeAngle;
        }
    }
}
