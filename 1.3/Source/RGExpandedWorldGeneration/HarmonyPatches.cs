using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;
using static RimWorld.Planet.WorldGenStep_Roads;

namespace RGExpandedWorldGeneration
{
    [StaticConstructorOnStartup]
    internal static class HarmonyInit
    {
        static HarmonyInit()
        {
            new Harmony("RGExpandedWorldGeneration.Mod").PatchAll();
        }
    }
    [HarmonyPatch(typeof(WorldGenStep_Terrain), "SetupElevationNoise")]
    public static class SetupElevationNoise_Patch
    {
        public static void Prefix(ref FloatRange ___ElevationRange)
        {
            ___ElevationRange = new FloatRange(-500f * Page_CreateWorldParams_Patch.curWorldGenerationPreset.seaLevel, 5000f);
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
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Page_CreateWorldParams_Patch), nameof(Page_CreateWorldParams_Patch.curWorldGenerationPreset)));
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
            return score * Page_CreateWorldParams_Patch.curWorldGenerationPreset.biomeCommonalities[biomeDef.defName];
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
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Page_CreateWorldParams_Patch), nameof(Page_CreateWorldParams_Patch.curWorldGenerationPreset)));
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
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Page_CreateWorldParams_Patch), nameof(Page_CreateWorldParams_Patch.curWorldGenerationPreset)));
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
            __instance.ancientSitesPer100kTiles *= Page_CreateWorldParams_Patch.curWorldGenerationPreset.ancientRoadDensity;
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
                def.spawnChance *= Page_CreateWorldParams_Patch.curWorldGenerationPreset.riverDensity;
                if (def.branches != null)
                {
                    riverData.branchChance = new float[def.branches.Count];
                    for (var i = 0; i < def.branches.Count; i++)
                    {
                        riverData.branchChance[i] = def.branches[i].chance;
                        def.branches[i].chance *= Page_CreateWorldParams_Patch.curWorldGenerationPreset.riverDensity;
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
    [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
    public static class Page_CreateWorldParams_Patch
    {
        public static WorldGenerationPreset curWorldGenerationPreset;

        public static Vector2 scrollPositon;

        [TweakValue("0P")] public static float scrollWidthOffset = 21;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var doGuiMethod = AccessTools.Method(typeof(Page_CreateWorldParams_Patch), "DoGui");
            var beginScrollViewMethod = AccessTools.Method(typeof(Page_CreateWorldParams_Patch), "BeginScrollView");
            var beginGroupMethod = AccessTools.Method(typeof(GUI), "BeginGroup", new Type[] { typeof(Rect) });
            var endGroupMethod = AccessTools.Method(typeof(GUI), "EndGroup");
            var scrollWidthOffsetField = AccessTools.Field(typeof(Page_CreateWorldParams_Patch), "scrollWidthOffset");
            var getY = AccessTools.Method(typeof(Rect), "get_y");

            var codes = instructions.ToList();

            bool found1 = false;
            bool found2 = false;
            bool found3 = false;
            bool found4 = false;

            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;
                if (!found1 && code.opcode == OpCodes.Ldloc_1 && codes[i - 1].Calls(getY))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, scrollWidthOffsetField);
                    yield return new CodeInstruction(OpCodes.Add);
                    found1 = true;
                }

                if (!found2 && codes[i].Calls(beginGroupMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Call, beginScrollViewMethod);
                    found2 = true;
                }

                if (!found3 && codes[i].opcode == OpCodes.Ldc_R4 && codes[i].OperandIs(200f) && codes[i - 1].Calls(AccessTools.Method(typeof(Rect), "get_width")))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, scrollWidthOffsetField);
                    yield return new CodeInstruction(OpCodes.Add);
                    found3 = true;
                }

                if (!found4 && codes[i + 1].Calls(endGroupMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 6);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                    yield return new CodeInstruction(OpCodes.Call, doGuiMethod);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Widgets), nameof(Widgets.EndScrollView)));
                    found4 = true;
                }
            }
        }
        private static void BeginScrollView(Rect rect, float width)
        {
            var outRect = new Rect(0, 0, width, rect.height - 100);
            Rect viewRect = outRect;
            outRect.width += scrollWidthOffset;
            viewRect.height = GetScrollHeight();
            Widgets.BeginScrollView(outRect, ref scrollPositon, viewRect);
        }
        private static float GetScrollHeight()
        {
            return 240 + ((6 + DefDatabase<BiomeDef>.DefCount) * 40);
        }
        private static void DoGui(Page_CreateWorldParams window, ref float num, float width2)
        {
            UpdateCurPreset(window);
            window.absorbInputAroundWindow = false;
            DoSlider(ref num, width2, "RG.RiverDensity".Translate(), ref curWorldGenerationPreset.riverDensity);
            DoSlider(ref num, width2, "RG.AncientRoadDensity".Translate(), ref curWorldGenerationPreset.ancientRoadDensity);
            DoSlider(ref num, width2, "RG.FactionRoadDensity".Translate(), ref curWorldGenerationPreset.factionRoadDensity);
            DoSlider(ref num, width2, "RG.MountainDensity".Translate(), ref curWorldGenerationPreset.mountainDensity);
            DoSlider(ref num, width2, "RG.SeaLevel".Translate(), ref curWorldGenerationPreset.seaLevel);
            num += 40f;
            var prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, num, 400, 30), "RG.BiomeCommonalities".Translate());
            Text.Anchor = prevAnchor;
            foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs.OrderBy(x => x.label ?? x.defName))
            {
                var value = curWorldGenerationPreset.biomeCommonalities[biomeDef.defName];
                DoSlider(ref num, width2, biomeDef.label?.CapitalizeFirst() ?? biomeDef.defName, ref value);
                curWorldGenerationPreset.biomeCommonalities[biomeDef.defName] = value;
            }
        }
        private static void UpdateCurPreset(Page_CreateWorldParams window)
        {
            if (curWorldGenerationPreset is null)
            {
                curWorldGenerationPreset = new WorldGenerationPreset();
                curWorldGenerationPreset.Init();
            };
            curWorldGenerationPreset.factionCounts = window.factionCounts.ToDictionary(x => x.Key.defName, y => y.Value);
            curWorldGenerationPreset.temperature = window.temperature;
            curWorldGenerationPreset.seedString = window.seedString;
            curWorldGenerationPreset.planetCoverage = window.planetCoverage;
            curWorldGenerationPreset.rainfall = window.rainfall;
            curWorldGenerationPreset.population = window.population;
        }
        private static void DoSlider(ref float num, float width2, string label, ref float field)
        {
            num += 40f;
            Widgets.Label(new Rect(0f, num, 200f, 30f), label);
            Rect slider = new Rect(200f, num, width2, 30f);
            field = Widgets.HorizontalSlider(slider, field, 0f, 10f, false, (field * 100).ToStringDecimalIfSmall() + "%");
        }
    }
}
