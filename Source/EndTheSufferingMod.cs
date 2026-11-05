using HarmonyLib;
using UnityEngine;
using Verse;

namespace EndTheSuffering
{
    public class EndTheSufferingSettings : ModSettings
    {
        public float moodThreshold = 0.05f;
        public float requiredHours = 24f;
        public int mediumDamage = 14;
        public int largeDamage = 24;
        public bool colonistThought = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref moodThreshold, "moodThreshold", 0.05f);
            Scribe_Values.Look(ref requiredHours, "requiredHours", 24f);
            Scribe_Values.Look(ref mediumDamage, "mediumDamage", 14);
            Scribe_Values.Look(ref largeDamage, "largeDamage", 24);
            Scribe_Values.Look(ref colonistThought, "colonistThought", true);
        }
    }

    public class EndTheSufferingMod : Mod
    {
        public static EndTheSufferingSettings Settings;

        public EndTheSufferingMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<EndTheSufferingSettings>();
            new Harmony("bebranot.endthesuffering").PatchAll(typeof(EndTheSufferingMod).Assembly);
        }

        public override string SettingsCategory() => "End the Suffering";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("EndSuffering_SettingMood".Translate(Mathf.RoundToInt(Settings.moodThreshold * 100f)));
            Settings.moodThreshold = list.Slider(Settings.moodThreshold, 0.01f, 0.30f);

            list.Label("EndSuffering_SettingHours".Translate(Mathf.RoundToInt(Settings.requiredHours)));
            Settings.requiredHours = Mathf.Round(list.Slider(Settings.requiredHours, 1f, 240f));

            list.Label("EndSuffering_SettingMedium".Translate(Settings.mediumDamage));
            Settings.mediumDamage = Mathf.RoundToInt(list.Slider(Settings.mediumDamage, 5f, 60f));

            list.Label("EndSuffering_SettingLarge".Translate(Settings.largeDamage));
            Settings.largeDamage = Mathf.RoundToInt(list.Slider(Settings.largeDamage, Settings.mediumDamage + 1f, 80f));
            if (Settings.largeDamage <= Settings.mediumDamage)
            {
                Settings.largeDamage = Settings.mediumDamage + 1;
            }

            list.CheckboxLabeled("EndSuffering_SettingThought".Translate(), ref Settings.colonistThought);
            list.End();
        }
    }
}
