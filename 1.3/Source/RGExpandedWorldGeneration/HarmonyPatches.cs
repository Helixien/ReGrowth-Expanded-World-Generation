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
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Profile;
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

    [HarmonyPatch(typeof(WorldFactionsUIUtility), "DoWindowContents")]
    public static class DoWindowContents_Patch
    {
        public const float LowerWidgetHeight = 210;
        public static void Prefix(ref Rect rect)
        {
            rect.y += 425;
            rect.height = LowerWidgetHeight;
        }
    }

    [HarmonyPatch(typeof(WorldLayer), "RegenerateNow")]
    public static class RegenerateNow_Patch
    {
        public static bool Prefix(WorldLayer __instance)
        {
            if (Page_CreateWorldParams_Patch.dirty && __instance is WorldLayer_Glow && Find.WindowStack.WindowOfType<Page_CreateWorldParams>() != null)
            {
                Log.Message("Removing it");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
    public static class Page_CreateWorldParams_Patch
    {
        public const int WorldCameraHeight = 315;
        public const int WorldCameraWidth = 315;

        private static Color BackgroundColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 15);
        private static Texture2D GeneratePreview = ContentFinder<Texture2D>.Get("UI/GeneratePreview");

        public static WorldGenerationPreset curWorldGenerationPreset;

        public static Vector2 scrollPosition;

        public static bool dirty;

        public static Texture2D worldPreview;

        private static int dirtyUpdate = 0;

        private static bool biomeCoverageInit;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var doGuiMethod = AccessTools.Method(typeof(Page_CreateWorldParams_Patch), "DoGui");
            var beginScrollViewMethod = AccessTools.Method(typeof(Page_CreateWorldParams_Patch), "BeginScrollView");
            var beginGroupMethod = AccessTools.Method(typeof(GUI), "BeginGroup", new Type[] { typeof(Rect) });
            var endGroupMethod = AccessTools.Method(typeof(GUI), "EndGroup");
            var scrollWidthOffsetField = AccessTools.Field(typeof(Page_CreateWorldParams_Patch), "scrollWidthOffset");
            var getY = AccessTools.Method(typeof(Rect), "get_y");

            var codes = instructions.ToList();
            bool found = false;

            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;
                if (!found && codes[i + 1].Calls(endGroupMethod))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 6);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                    yield return new CodeInstruction(OpCodes.Call, doGuiMethod);
                    found = true;
                }
            }
        }

        private static void Postfix(Page_CreateWorldParams __instance)
        {
            DoWorldPreviewArea(__instance);
        }

        private static Stopwatch total = new Stopwatch();
        private static void DoGui(Page_CreateWorldParams window, ref float num, float width2)
        {
            window.absorbInputAroundWindow = false;
            UpdateCurPreset(window);
            DoSlider(0, ref num, width2, "RG.RiverDensity".Translate(), ref curWorldGenerationPreset.riverDensity);
            DoSlider(0, ref num, width2, "RG.MountainDensity".Translate(), ref curWorldGenerationPreset.mountainDensity);
            DoSlider(0, ref num, width2, "RG.SeaLevel".Translate(), ref curWorldGenerationPreset.seaLevel);

            if (!biomeCoverageInit)
            {
                biomeCoverageInit = true;
                total.Restart();

                Rand.PushState();
                int seed = (Rand.Seed = WorldGenerator.GetSeedFromSeedString(window.seedString));
                var worldGenStep_Terrain = new WorldGenStep_Terrain();
                Current.CreatingWorld = new World();
                Current.CreatingWorld.info.seedString = window.seedString;
                Current.CreatingWorld.info.planetCoverage = window.planetCoverage;
                Current.CreatingWorld.info.overallRainfall = window.rainfall;
                Current.CreatingWorld.info.overallTemperature = window.temperature;
                Current.CreatingWorld.info.overallPopulation = window.population;
                Current.CreatingWorld.info.name = NameGenerator.GenerateName(RulePackDefOf.NamerWorld);
                WorldGenerator.tmpGenSteps.Clear();
                WorldGenerator.tmpGenSteps.AddRange(WorldGenerator.GenStepsInOrder);
                for (int i = 0; i < WorldGenerator.tmpGenSteps.Count; i++)
                {
                    try
                    {
                        Rand.Seed = Gen.HashCombineInt(seed, WorldGenerator.GetSeedPart(WorldGenerator.tmpGenSteps, i));
                        if (worldGenStepDefs.Contains(WorldGenerator.tmpGenSteps[i]))
                        {
                            if (WorldGenerator.tmpGenSteps[i] == DefDatabase<WorldGenStepDef>.GetNamed("Terrain"))
                            {
                                WorldGenerator.tmpGenSteps[i].worldGenStep.GenerateFresh(window.seedString);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error in WorldGenStep: " + ex);
                    }
                }
                Rand.PopState();
                DeepProfiler.End();
                Current.CreatingWorld = null;
                Log.Message("Biome score: " + (float)total.ElapsedTicks / Stopwatch.Frequency);

            }
            var labelRect = new Rect(0f, num + 104, 80, 30);
            Widgets.Label(labelRect, "RG.Biomes".Translate());
            var outRect = new Rect(labelRect.x, labelRect.yMax, width2 + 195, DoWindowContents_Patch.LowerWidgetHeight - 50);
            Rect viewRect = new Rect(outRect.x, outRect.y, outRect.width - 16f, (DefDatabase<BiomeDef>.DefCount * 40) + 10);
            Widgets.DrawBoxSolid(new Rect(outRect.x, outRect.y, outRect.width - 16f, outRect.height), BackgroundColor);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            num = outRect.y + 15;
            foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs.OrderBy(x => x.label ?? x.defName))
            {
                var value = curWorldGenerationPreset.biomeCommonalities[biomeDef.defName];
                DoSliderBiomeCommonality(10, ref num, width2, biomeDef.label?.CapitalizeFirst() ?? biomeDef.defName, ref value);
                curWorldGenerationPreset.biomeCommonalities[biomeDef.defName] = value;
            }
            Widgets.EndScrollView();
        }
        private static void DoWorldPreviewArea(Page_CreateWorldParams window)
        {
            var previewAreaRect = new Rect(545, 10, WorldCameraHeight, WorldCameraWidth);
            Rect generateButtonRect = Rect.zero;
            if (worldPreview is null)
            {
                generateButtonRect = new Rect(previewAreaRect.center.x - 12, previewAreaRect.center.y - 12, 35, 35);
                Text.Font = GameFont.Medium;
                var textSize = Text.CalcSize("RG.GeneratePreview".Translate());
                Widgets.Label(new Rect(generateButtonRect.center.x - (textSize.x / 2), generateButtonRect.yMax, textSize.x, textSize.y), "RG.GeneratePreview".Translate());
                Text.Font = GameFont.Small;
            }
            else
            {
                generateButtonRect = new Rect(previewAreaRect.xMax - 35, previewAreaRect.y, 35, 35);
            }
            DrawGeneratePreviewButton(window, generateButtonRect);
            if (dirty)
            {
                Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                Find.WorldCamera.gameObject.SetActive(true);
                Find.WorldCameraDriver.desiredAltitude = 700;
                Find.WorldCameraDriver.Update();
            }
            if (dirtyUpdate == 1)
            {
                worldPreview = GetWorldCameraPreview(Find.WorldCamera, WorldCameraHeight, WorldCameraWidth);
                Find.WorldCamera.gameObject.SetActive(false);
                Find.World.renderer.wantedMode = WorldRenderMode.None;
                dirty = false;
                dirtyUpdate = 0;
            }
            if (dirty)
            {
                dirtyUpdate++;
            }
            
            if (worldPreview != null)
            {
                GUI.DrawTexture(previewAreaRect, worldPreview);
            }

            float numY = previewAreaRect.yMax - 40;
            DoSlider(previewAreaRect.x - 55, ref numY, 256, "RG.AncientRoadDensity".Translate(), ref curWorldGenerationPreset.ancientRoadDensity);
            DoSlider(previewAreaRect.x - 55, ref numY, 256, "RG.FactionRoadDensity".Translate(), ref curWorldGenerationPreset.factionRoadDensity);
        }
        private static void DrawGeneratePreviewButton(Page_CreateWorldParams window, Rect generateButtonRect)
        {
            GUI.DrawTexture(generateButtonRect, GeneratePreview);
            if (Mouse.IsOver(generateButtonRect))
            {
                Widgets.DrawHighlightIfMouseover(generateButtonRect);
                if (Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        dirty = false;
                        dirtyUpdate = 0;
                        worldPreview = null;
                        Find.GameInitData.ResetWorldRelatedMapInitData();
                        Current.Game.World = GenerateWorld(window.planetCoverage, window.seedString, window.rainfall, window.temperature, window.population, window.factionCounts);
                        Find.World.features = new WorldFeatures();
                        MemoryUtility.UnloadUnusedUnityAssets();
                        for (int i = 0; i < Find.World.renderer.layers.Count; i++)
                        {
                            var layer = Find.World.renderer.layers[i];
                            if (layer is WorldLayer_Hills || layer is WorldLayer_Rivers || layer is WorldLayer_Roads || layer is WorldLayer_Terrain)
                            {
                                layer.RegenerateNow();
                            }
                        }
                        dirty = true;
                        Event.current.Use();
                    }
                }
            }
        }

        private static HashSet<WorldGenStepDef> worldGenStepDefs = new HashSet<WorldGenStepDef>
        {
            DefDatabase<WorldGenStepDef>.GetNamed("Components"),
            DefDatabase<WorldGenStepDef>.GetNamed("Terrain"),
            DefDatabase<WorldGenStepDef>.GetNamed("Lakes"),
            DefDatabase<WorldGenStepDef>.GetNamed("Rivers"),
            DefDatabase<WorldGenStepDef>.GetNamed("AncientSites"),
            DefDatabase<WorldGenStepDef>.GetNamed("AncientRoads"),
            DefDatabase<WorldGenStepDef>.GetNamed("Roads")
        };
        public static World GenerateWorld(float planetCoverage, string seedString, OverallRainfall overallRainfall, OverallTemperature overallTemperature, OverallPopulation population, Dictionary<FactionDef, int> factionCounts = null)
        {
            Rand.PushState();
            int seed = (Rand.Seed = WorldGenerator.GetSeedFromSeedString(seedString));
            try
            {
                Current.CreatingWorld = new World();
                Current.CreatingWorld.info.seedString = seedString;
                Current.CreatingWorld.info.planetCoverage = planetCoverage;
                Current.CreatingWorld.info.overallRainfall = overallRainfall;
                Current.CreatingWorld.info.overallTemperature = overallTemperature;
                Current.CreatingWorld.info.overallPopulation = population;
                Current.CreatingWorld.info.name = NameGenerator.GenerateName(RulePackDefOf.NamerWorld);
                WorldGenerator.tmpGenSteps.Clear();
                WorldGenerator.tmpGenSteps.AddRange(WorldGenerator.GenStepsInOrder);
                for (int i = 0; i < WorldGenerator.tmpGenSteps.Count; i++)
                {
                    try
                    {
                        Rand.Seed = Gen.HashCombineInt(seed, WorldGenerator.GetSeedPart(WorldGenerator.tmpGenSteps, i));
                        if (worldGenStepDefs.Contains(WorldGenerator.tmpGenSteps[i]))
                        {
                            WorldGenerator.tmpGenSteps[i].worldGenStep.GenerateFresh(seedString);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error in WorldGenStep: " + ex);
                    }
                }
                return Current.CreatingWorld;
            }
            finally
            {
                Rand.PopState();
                Current.CreatingWorld = null;
            }
        }
        private static Texture2D GetWorldCameraPreview(Camera worldCamera, int width, int height)
        {
            Rect rect = new Rect(0, 0, width, height);
            RenderTexture renderTexture = new RenderTexture(width, height, 24);
            Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGBA32, false);

            worldCamera.targetTexture = renderTexture;
            worldCamera.Render();

            RenderTexture.active = renderTexture;
            screenShot.ReadPixels(rect, 0, 0);
            screenShot.Apply();
            worldCamera.targetTexture = null;
            RenderTexture.active = null;
            return screenShot;
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
        private static void DoSlider(float x, ref float num, float width2, string label, ref float field)
        {
            num += 40f;
            var labelRect = new Rect(x, num, 200f, 30f);
            Widgets.Label(labelRect, label);
            Rect slider = new Rect(labelRect.xMax, num, width2, 30f);
            field = Widgets.HorizontalSlider(slider, field, 0f, 10f, false, (field * 100).ToStringDecimalIfSmall() + "%");
        }
        private static void DoSliderBiomeCommonality(float x, ref float num, float width2, string label, ref float field)
        {
            var labelRect = new Rect(x, num - 10, 200f, 30f);
            Widgets.Label(labelRect, label);
            Rect slider = new Rect(labelRect.x, num, width2 + 160f, 30f);
            field = Widgets.HorizontalSlider(slider, field, 0f, 10f, false, (field * 100).ToStringDecimalIfSmall() + "%");
            num += 40f;
        }
    }
}
