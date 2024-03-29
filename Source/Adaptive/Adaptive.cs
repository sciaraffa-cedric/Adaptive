﻿using RimWorld;
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

        public static bool ForceUpdate = false;
        public static float? CachedPoints = null;
        public static int? LastUpdateTicks = null;

        public Adaptive(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("com.RimWorld.Adaptive");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        /*public override string SettingsCategory()
        {
            return "Adaptive Threats";
        }*/

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
                    int col = 0;
                    float coeff = 0f;
                    float AgePts = 1f;
                    //float BodySize = 1f;
                    float relevance = 0f;

                    if (pawn.IsQuestLodger())
                    {
                        continue;
                    }
                    float num3 = 0f;

                    if (pawn.IsFreeColonist)
                    {
                        // relevance = pawn.records.storyRelevance / 40000 ; 
                        // modification des valeurs < 1 par transformation exp storyRelevance vs LastBattleTick == battleExitTick
                        relevance = pawn.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) / (144f * 40000f);
                        // 150 000 storyrelevance at year 6 (ticks = 6 * 3 600 000) - 24h 60 000 ticks - 1y 60 days

                        coeff = (3.6f / (1f + Mathf.Exp((4f - relevance) / 2)) + 0.5f);

                        /*switch (Settings.systemToUse)
                        {
                            case AdaptiveSettings.SystemToUseEnum.Low: num3 = Adaptive.PointsPerColonistByWealthCurve2.Evaluate(vanillaWealth) * coeff; break;
                            case AdaptiveSettings.SystemToUseEnum.Default: num3 = Adaptive.PointsPerColonistByWealthCurve.Evaluate(vanillaWealth) * coeff; break;
                            case AdaptiveSettings.SystemToUseEnum.High: num3 = Adaptive.PointsPerColonistByWealthCurve3.Evaluate(vanillaWealth) * coeff; break;
                            default: num3 = Adaptive.PointsPerColonistByWealthCurve.Evaluate(vanillaWealth) * coeff; break;
                        }*/

                        num3 = Adaptive.PointsPerColonistByWealthCurve.Evaluate(vanillaWealth) * coeff;

                        // BodySize = pawn.BodySize;
                        // num3 *= BodySize;

                        // num3 = 20f; //TEST --- sortie ASSEMBLIES

                        col += 1;

                        if (target is Caravan)
                        {
                            num3 *= 1f; // instead of 1.2 v1.4
                        }

                        // num2 += num3; erreur ajout double num3 à num2
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
                        if (pawn.IsSlaveOfColony)
                        {
                            num3 *= 0.5f; // .75f vanilla
                        }
                        if (pawn.Downed)
                        {
                            num3 *= 0f;
                        }
                        if (ModsConfig.BiotechActive && pawn.RaceProps.Humanlike)
                        {
                            AgePts = Adaptive.PointsFactorForPawnAgeYearsCurve.Evaluate(pawn.ageTracker.AgeBiologicalYearsFloat);
                            num3 *= AgePts;
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

                /*switch (Settings.systemToUse)
                {
                    case AdaptiveSettings.SystemToUseEnum.Low: finalPoints = num2 * 0.5f; break;
                    case AdaptiveSettings.SystemToUseEnum.Default: finalPoints = num2; break;
                    case AdaptiveSettings.SystemToUseEnum.High: finalPoints = num2 * 1.5f; break;
                    default: finalPoints = num2; break;
                }*/

                finalPoints = num2;

                // float num4 = adaptiveWealthPoints + finalPoints * UnityEngine.Random.Range(0.9f, 1.1f);
                float num4 = adaptiveWealthPoints + finalPoints; // TEST

                float totalThreatPointsFactor = Find.StoryWatcher.watcherAdaptation.TotalThreatPointsFactor;
                float num5 = Mathf.Lerp(1f, totalThreatPointsFactor, Find.Storyteller.difficulty.adaptationEffectFactor);
                //num4 *= Mathf.Min(num5, 1f);
                num4 *= num5;

                //remove difficulty scale -> fixed 100% threat scale for quests in custom storyteller 
                num4 *= Find.Storyteller.difficulty.threatScale;
                //num4 *= 2.2f; // losing is fun

                num4 *= target.IncidentPointsRandomFactorRange.RandomInRange;
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

        private static readonly SimpleCurve PointsFactorForPawnAgeYearsCurve = new SimpleCurve
    {
        new CurvePoint(3f, 0f),
        new CurvePoint(13f, 0.5f),
        new CurvePoint(18f, 1f)
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

   }
}

