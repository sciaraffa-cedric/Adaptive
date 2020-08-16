using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using Verse;
using System.Reflection.Emit;
using System.Diagnostics;
using System.Collections;
using Verse.Sound;

namespace Adaptive
{
    class Adaptive : Mod
    {
        public static AdaptiveSettings Settings;

        public static bool ForceUpdate = false;
        public static float? CachedPoints = null;
        public static int? LastUpdateTicks = null;

        public Adaptive(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AdaptiveSettings>();

            var harmony = new Harmony("com.RimWorld.Adaptive");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Adaptive Threats";
        }

        [StaticConstructorOnStartup]
        public static class Main
        {
        }

        // Cache
        [HarmonyPatch(typeof(StorytellerUtility))]
        [HarmonyPatch("DefaultParmsNow")]
        static class StorytellerUtility_DefaultParmsNow_Patch
        {
            static bool Prefix()
            {
                ForceUpdate = true;
                return true;
            }
        }

        [HarmonyPatch(typeof(GameComponentUtility))]
        [HarmonyPatch("LoadedGame")]
        static class GameComponentUtility_LoadedGame_Patch
        {
            static void Postfix()
            {
                CachedPoints = null;
                LastUpdateTicks = null;
            }
        }

        [HarmonyPatch(typeof(StorytellerUtility))]
        [HarmonyPatch("DefaultThreatPointsNow")]
        static class StorytellerUtility_DefaultThreatPointsNow_Patch
        {
            static bool Prefix(ref float __result, IIncidentTarget target)
            {
                /*if (CachedPoints.HasValue && LastUpdateTicks.HasValue && !ForceUpdate && (GenTicks.TicksGame < (LastUpdateTicks + GenDate.TicksPerHour)))
                {
                    if (Settings.logMessages)
                    {
                        Log.Message("Using cache");
                    }
                    __result = CachedPoints.Value;
                    return false;
                }
                if (Settings.logMessages)
                {
                    Log.Message("Not using cache, forced = " + ForceUpdate);
                }
                ForceUpdate = false;*/

                float vanillaWealth = target.PlayerWealthForStoryteller;
                float adaptiveWealthPoints = Adaptive.PointsPerWealthCurve.Evaluate(vanillaWealth);

                float num2 = 0f;
                int numcol = 0;

                foreach (Pawn pawn in target.PlayerPawnsForStoryteller)
                {
                    float num3 = 0f;
                    int col = 0;
                    float relevance = 0f;
                    float coeff = 0f;
                    float BodySize = 1f;

                    if (pawn.IsFreeColonist)
                    {
                        relevance = pawn.records.StoryRelevance / 40000; //modification des valeurs <1 par transformation exp
                        BodySize = pawn.BodySize;
                        coeff = (3.6f / (1f + Mathf.Exp((4f - relevance) / 2)) + 0.5f);

                        switch (Settings.systemToUse)
                        {
                            case AdaptiveSettings.SystemToUseEnum.Low: num3 = Adaptive.PointsPerColonistByWealthCurve2.Evaluate(vanillaWealth) * coeff; break;
                            case AdaptiveSettings.SystemToUseEnum.Default: num3 = Adaptive.PointsPerColonistByWealthCurve.Evaluate(vanillaWealth) * coeff; break;
                            case AdaptiveSettings.SystemToUseEnum.High: num3 = Adaptive.PointsPerColonistByWealthCurve3.Evaluate(vanillaWealth) * coeff; break;
                            default: num3 = Adaptive.PointsPerColonistByWealthCurve.Evaluate(vanillaWealth) * coeff; break;
                        }

                        num3 *= BodySize;
                        //    Adaptive.PointsPerColonistByWealthCurve.Evaluate(vanillaWealth) * coeff;
                        col += 1;

                        if (target is Caravan)
                        {
                            num3 *= 1.2f;
                        }
                    }

                    else if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer && !pawn.Downed && pawn.training.CanAssignToTrain(TrainableDefOf.Release).Accepted)
                    {
                        num3 = 0.08f * pawn.kindDef.combatPower;
                        if (target is Caravan)
                        {
                            num3 *= 0.7f;
                            col += 0;
                        }
                    }

                    if (num3 > 0f)
                    {
                        if (pawn.ParentHolder != null && pawn.ParentHolder is Building_CryptosleepCasket)
                        {
                            num3 *= 0.3f;
                        }
                        else if (pawn.Downed)
                        {
                            num3 *= 0f;
                        }

                        num3 = Mathf.Lerp(num3, num3 * pawn.health.summaryHealth.SummaryHealthPercent, 0.15f);
                        num2 += num3;
                    }

                    if (col > 0f)
                    {
                        numcol += 1;
                    }
                }

                Stopwatch watch = new Stopwatch();
                watch.Start();

                float num6 = -.3f * (1 / (1 + Mathf.Exp((20 - numcol) / 8))) + 1f;

                //modification du point / colon
                float finalPoints;

                switch (Settings.systemToUse)
                {
                    case AdaptiveSettings.SystemToUseEnum.Low: finalPoints = num2 * 0.7f; break;
                    case AdaptiveSettings.SystemToUseEnum.Default: finalPoints = num2; break;
                    case AdaptiveSettings.SystemToUseEnum.High: finalPoints = num2 * 1.3f; break;
                    default: finalPoints = num2; break;
                }

                float num4 = adaptiveWealthPoints + finalPoints * UnityEngine.Random.Range(0.9f, 1.1f);
                float totalThreatPointsFactor = Find.StoryWatcher.watcherAdaptation.TotalThreatPointsFactor;
                float num5 = Mathf.Lerp(1f, totalThreatPointsFactor, Find.Storyteller.difficulty.adaptationEffectFactor);
                num4 *= num5;
                num4 *= Find.Storyteller.difficulty.threatScale;
                num4 *= Find.Storyteller.def.pointsFactorFromDaysPassed.Evaluate((float)GenDate.DaysPassed);
                num4 *= num6;

                float final;
                final = Mathf.Max(num4, 35f);

                __result = final;

                CachedPoints = __result;
                LastUpdateTicks = GenTicks.TicksGame;

                return false;
            }
        }

        private static readonly SimpleCurve PointsPerWealthCurve = new SimpleCurve
        {
            {
                new CurvePoint(0f, 0f),
                true
            },
            {
                new CurvePoint(10000f, 0f),
                true
            },
            {
                new CurvePoint(400000f, 800f),
                true
            },
            {
                new CurvePoint(700000f, 900f),
                true
            },
            {
                new CurvePoint(1000000f, 1000f),
                true
            }
        };

        private static readonly SimpleCurve PointsPerColonistByWealthCurve = new SimpleCurve
        {
            {
                new CurvePoint(0f, 20f),
                true
            },
            {
                new CurvePoint(10000f, 20f),
                true
            },
            {
                new CurvePoint(400000f, 100f),
                true
            },
            {
                new CurvePoint(1000000f, 120f),
                true
            }
        };

        private static readonly SimpleCurve PointsPerColonistByWealthCurve2 = new SimpleCurve
        {
            {
                new CurvePoint(0f, 15f),
                true
            },
            {
                new CurvePoint(10000f, 15f),
                true
            },
            {
                new CurvePoint(400000f, 60f),
                true
            },
            {
                new CurvePoint(1000000f, 70f),
                true
            }
        };

        private static readonly SimpleCurve PointsPerColonistByWealthCurve3 = new SimpleCurve
        {
            {
                new CurvePoint(0f, 30f),
                true
            },
            {
                new CurvePoint(10000f, 30f),
                true
            },
            {
                new CurvePoint(400000f, 140f),
                true
            },
            {
                new CurvePoint(1000000f, 200f),
                true
            }
        };

        public class AdaptiveSettings : ModSettings
        {
            public enum SystemToUseEnum
            {
                Low = 0,
                Default = 1,
                High = 2,
            }

            public SystemToUseEnum systemToUse = SystemToUseEnum.Default;

            public override void ExposeData()
            {
                base.ExposeData();
                Scribe_Values.Look(ref systemToUse, "systemToUse", SystemToUseEnum.Default);

            }

            public void DoWindowContents(Rect inRect)
            {
                var list = new Listing_Standard { ColumnWidth = (inRect.width - 34f) / 1.5f };
                list.Begin(inRect);
                list.Gap(12f);

                list.ColumnWidth = inRect.width - 24f;

                //var list2 = new Listing_Standard { ColumnWidth = (inRect.width - 24f) };
                //list.Begin(inRect);
                //list.Gap(12f);

                string medium = "Default";
                string low = "Chill";
                string high = "Hardcore";
                string current;
                switch (systemToUse)
                {
                    case SystemToUseEnum.Default: current = medium; break;
                    case SystemToUseEnum.Low: current = low; break;
                    case SystemToUseEnum.High: current = high; break;
                    default: current = "???"; break;
                }

                if (list.ButtonTextLabeled("Difficulty", current))
                {
                    List<FloatMenuOption> list5 = new List<FloatMenuOption>();

                    list5.Add(new FloatMenuOption(medium, delegate () { systemToUse = SystemToUseEnum.Default; }, MenuOptionPriority.Default, null, null, 0f, null, null));
                    list5.Add(new FloatMenuOption(low, delegate () { systemToUse = SystemToUseEnum.Low; }, MenuOptionPriority.Default, null, null, 0f, null, null));
                    list5.Add(new FloatMenuOption(high, delegate () { systemToUse = SystemToUseEnum.High; }, MenuOptionPriority.Default, null, null, 0f, null, null));

                    Find.WindowStack.Add(new FloatMenu(list5));
                }

                list.End();
            }
        }
    }
}

