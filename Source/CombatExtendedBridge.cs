using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace EndTheSuffering
{
    /// <summary>
    /// Optional Combat Extended access. Everything goes through reflection so the mod has no CE dependency,
    /// and any failure falls back to vanilla behaviour. Only runs on a right click or on the shot, never per tick.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class CombatExtendedBridge
    {
        private static readonly Type AmmoUserType;
        private static readonly MethodInfo UseAmmoGetter;
        private static readonly MethodInfo CanBeFiredNowGetter;
        private static readonly MethodInfo LoadedMagazineGetter;
        private static readonly MethodInfo CurAmmoProjectileGetter;
        private static readonly MethodInfo NotifyShotFired;

        static CombatExtendedBridge()
        {
            AmmoUserType = AccessTools.TypeByName("CombatExtended.CompAmmoUser");
            if (AmmoUserType == null)
            {
                return;
            }
            UseAmmoGetter = AccessTools.PropertyGetter(AmmoUserType, "UseAmmo");
            CanBeFiredNowGetter = AccessTools.PropertyGetter(AmmoUserType, "CanBeFiredNow");
            LoadedMagazineGetter = AccessTools.PropertyGetter(AmmoUserType, "LoadedMagazine");
            CurAmmoProjectileGetter = AccessTools.PropertyGetter(AmmoUserType, "CurAmmoProjectile");
            NotifyShotFired = AccessTools.Method(AmmoUserType, "Notify_ShotFired");
        }

        private static ThingComp AmmoComp(ThingWithComps weapon)
        {
            if (AmmoUserType == null)
            {
                return null;
            }
            var comps = weapon.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (AmmoUserType.IsInstanceOfType(comps[i]))
                {
                    return comps[i];
                }
            }
            return null;
        }

        public static bool HasRoundReady(ThingWithComps weapon)
        {
            ThingComp comp = AmmoComp(weapon);
            if (comp == null)
            {
                return true;
            }
            try
            {
                return !(bool)UseAmmoGetter.Invoke(comp, null) || (bool)CanBeFiredNowGetter.Invoke(comp, null);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[End the Suffering] CE ammo check failed, ignoring ammo: " + e.Message, 88120431);
                return true;
            }
        }

        public static ThingDef LoadedProjectile(ThingWithComps weapon)
        {
            ThingComp comp = AmmoComp(weapon);
            if (comp == null)
            {
                return null;
            }
            try
            {
                return CurAmmoProjectileGetter.Invoke(comp, null) as ThingDef;
            }
            catch (Exception e)
            {
                Log.WarningOnce("[End the Suffering] CE projectile lookup failed: " + e.Message, 88120432);
                return null;
            }
        }

        public static void ConsumeRound(ThingWithComps weapon)
        {
            ThingComp comp = AmmoComp(weapon);
            if (comp == null)
            {
                return;
            }
            try
            {
                if ((bool)UseAmmoGetter.Invoke(comp, null) && (bool)LoadedMagazineGetter.Invoke(comp, null))
                {
                    NotifyShotFired.Invoke(comp, new object[] { 1 });
                }
            }
            catch (Exception e)
            {
                Log.WarningOnce("[End the Suffering] CE ammo use failed: " + e.Message, 88120433);
            }
        }
    }
}
