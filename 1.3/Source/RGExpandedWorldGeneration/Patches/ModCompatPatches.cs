using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;

namespace RGExpandedWorldGeneration
{
    [StaticConstructorOnStartup]
    public static class ModCompat
    {
        public static bool MyLittlePlanetActive;
        static ModCompat()
        {
            MyLittlePlanetActive = ModLister.mods.Any(x => x.Active && x.PackageIdPlayerFacing == "Oblitus.MyLittlePlanet");
        }
    }

    [HarmonyPatch]
    public static class RealisticPlanets_Patch
    {
        private static MethodBase pageUtility_StitchedPages;
        private static MethodBase genTemperature_SeasonalShiftAmplitudeAt;
        public static bool Prepare()
        {
            pageUtility_StitchedPages = AccessTools.Method("Planets_Code.PageUtility_StitchedPages:Postfix");
            genTemperature_SeasonalShiftAmplitudeAt = AccessTools.Method("Planets_Code.GenTemperature_SeasonalShiftAmplitudeAt:Postfix");
            return pageUtility_StitchedPages != null && genTemperature_SeasonalShiftAmplitudeAt != null;
        }
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return pageUtility_StitchedPages;
            yield return genTemperature_SeasonalShiftAmplitudeAt;
        }

        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class MyLittlePlanet_Patch
    {
        private static MethodBase planetShapeGenerator_DoGenerate_Patch;
        public static bool Prepare()
        {
            planetShapeGenerator_DoGenerate_Patch = AccessTools.Method("WorldGenRules.RulesOverrider+PlanetShapeGenerator_DoGenerate_Patch:Prefix");
            return planetShapeGenerator_DoGenerate_Patch != null;
        }
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method("RimWorld.Planet.PlanetShapeGenerator:DoGenerate");
        }
    
        [HarmonyPriority(int.MinValue)]
        public static void Prefix(ref int ___subdivisionsCount)
        {
            var type = AccessTools.TypeByName("WorldGenRules.RulesOverrider");
            var gameComp = Current.Game.components.First(x => x.GetType().Name.Contains("RulesOverrider"));
            ___subdivisionsCount = (int)AccessTools.Field(type, "subcount").GetValue(gameComp);
        }
    }

    [HarmonyPatch]
    public static class RimWar_Patch
    {
        private static MethodBase patch_Page_CreateWorldParams_DoWindowContents;
        public static bool Prepare()
        {
            patch_Page_CreateWorldParams_DoWindowContents = AccessTools.Method("RimWar.Harmony.RimWarMod+Patch_Page_CreateWorldParams_DoWindowContents:Postfix");
            return patch_Page_CreateWorldParams_DoWindowContents != null;
        }
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return patch_Page_CreateWorldParams_DoWindowContents;
        }

        public static float yOffset = 295;
        public static float xOffset = 495;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (codes[i].opcode == OpCodes.Ldc_R4)
                {
                    if (codes[i].OperandIs(118))
                    {
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(RimWar_Patch), "yOffset"));
                    }
                    else if (codes[i].OperandIs(0))
                    {
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(RimWar_Patch), "xOffset"));
                    }
                    else
                    {
                        yield return code;
                    }
                }
                else
                {
                    yield return code;
                }
            }
        }
    }
}
