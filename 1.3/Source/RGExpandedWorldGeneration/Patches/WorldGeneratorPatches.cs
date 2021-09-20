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

    [HarmonyPatch(typeof(WorldGenStep_Terrain), "SetupElevationNoise")]
    public static class SetupElevationNoise_Patch
    {
        public static void Prefix(ref FloatRange ___ElevationRange)
        {
            ___ElevationRange = new FloatRange(-500f * Page_CreateWorldParams_Patch.tmpWorldGenerationPreset.seaLevel, 5000f);
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Terrain), "GenerateTileFor")]
    public static class GenerateTileFor_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var methodToHook = AccessTools.Method(typeof(ModuleBase), "GetValue", new Type[] { typeof(Vector3) });
            var noiseMountainLinesField = AccessTools.Field(typeof(WorldGenStep_Terrain), "noiseMountainLines");
            var codes = instructions.ToList();
            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;
                if (i > 2 && code.Calls(methodToHook) && codes[i - 2].LoadsField(noiseMountainLinesField))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Page_CreateWorldParams_Patch), nameof(Page_CreateWorldParams_Patch.tmpWorldGenerationPreset)));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(WorldGenerationPreset), nameof(WorldGenerationPreset.mountainDensity)));
                    yield return new CodeInstruction(OpCodes.Div);
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Terrain), "BiomeFrom")]
    public static class BiomeFrom_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var methodToHook = AccessTools.Method(typeof(BiomeWorker), "GetScore");
            var getScoreAdjustedMethod = AccessTools.Method(typeof(BiomeFrom_Patch), "GetScoreAdjusted");
            var codes = instructions.ToList();
            bool found = false;
            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;
                if (!found && codes[i].opcode == OpCodes.Stloc_S && codes[i].operand is LocalBuilder lb && lb.LocalIndex == 5 && codes[i - 1].Calls(methodToHook))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 5);
                    yield return new CodeInstruction(OpCodes.Call, getScoreAdjustedMethod);
                    yield return new CodeInstruction(OpCodes.Stloc_S, 5);
                    found = true;
                }
            }
        }

        private static float GetScoreAdjusted(BiomeDef biomeDef, float score)
        {
            var scoreOffset = Page_CreateWorldParams_Patch.tmpWorldGenerationPreset.biomeScoreOffsets[biomeDef.defName];
            score += scoreOffset;
            var biomeCommonalityOverride = Page_CreateWorldParams_Patch.tmpWorldGenerationPreset.biomeCommonalities[biomeDef.defName] / 10f;
            if (biomeCommonalityOverride == 0)
            {
                if (scoreOffset != 0)
                {
                    return scoreOffset;
                }
                return -999;
            }
            var adjustedScore = score < 0 ? score / biomeCommonalityOverride : score * biomeCommonalityOverride;
            return adjustedScore;
        }
    }

    [HarmonyPatch]
    static class WorldGenStep_Roads_GenerateRoadEndpoints_Patch
    {
        static MethodBase TargetMethod()
        {
            foreach (var nestType in typeof(WorldGenStep_Roads).GetNestedTypes(AccessTools.all))
            {
                foreach (var meth in AccessTools.GetDeclaredMethods(nestType))
                {
                    if (meth.Name.Contains("GenerateRoadEndpoints") && meth.ReturnType == typeof(bool))
                    {
                        return meth;
                    }
                }
            }
            return null;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            bool found = false;
            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;
                if (!found && code.OperandIs(0.05f))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Page_CreateWorldParams_Patch), nameof(Page_CreateWorldParams_Patch.tmpWorldGenerationPreset)));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(WorldGenerationPreset), nameof(WorldGenerationPreset.factionRoadDensity)));
                    yield return new CodeInstruction(OpCodes.Div);
                    found = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Roads), "GenerateRoadEndpoints")]
    public static class GenerateRoadEndpoints_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var methodToHook = AccessTools.Method(typeof(FloatRange), "get_RandomInRange");
            var codes = instructions.ToList();
            bool found = false;
            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;
                if (!found && code.Calls(methodToHook))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Page_CreateWorldParams_Patch), nameof(Page_CreateWorldParams_Patch.tmpWorldGenerationPreset)));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(WorldGenerationPreset), nameof(WorldGenerationPreset.factionRoadDensity)));
                    yield return new CodeInstruction(OpCodes.Mul);
                    found = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_AncientSites), "GenerateAncientSites")]
    public static class GenerateAncientSites_Patch
    {
        private static void Prefix(WorldGenStep_AncientSites __instance, out FloatRange __state)
        {
            __state = __instance.ancientSitesPer100kTiles;
            __instance.ancientSitesPer100kTiles *= Page_CreateWorldParams_Patch.tmpWorldGenerationPreset.ancientRoadDensity;
        }

        private static void Postfix(WorldGenStep_AncientSites __instance, FloatRange __state)
        {
            __instance.ancientSitesPer100kTiles = __state;
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Rivers), "GenerateRivers")]
    public static class GenerateRivers_Patch
    {
        public struct RiverData
        {
            public float spawnChance;
            public float[] branchChance;
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(out Dictionary<RiverDef, RiverData> __state)
        {
            __state = new Dictionary<RiverDef, RiverData>();
            foreach (var def in DefDatabase<RiverDef>.AllDefs)
            {
                var riverData = new RiverData();
                __state[def] = riverData;
                riverData.spawnChance = def.spawnChance;
                def.spawnChance *= Page_CreateWorldParams_Patch.tmpWorldGenerationPreset.riverDensity;
                if (def.branches != null)
                {
                    riverData.branchChance = new float[def.branches.Count];
                    for (var i = 0; i < def.branches.Count; i++)
                    {
                        riverData.branchChance[i] = def.branches[i].chance;
                        def.branches[i].chance *= Page_CreateWorldParams_Patch.tmpWorldGenerationPreset.riverDensity;
                    }
                }
            }
        }

        private static void Postfix(Dictionary<RiverDef, RiverData> __state)
        {
            __state = new Dictionary<RiverDef, RiverData>();
            foreach (var data in __state)
            {
                data.Key.spawnChance = data.Value.spawnChance;
                if (data.Key.branches != null)
                {
                    for (var i = 0; i < data.Key.branches.Count; i++)
                    {
                        data.Key.branches[i].chance = data.Value.branchChance[i];
                    }
                }
            }
        }
    }
}
