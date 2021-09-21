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
    internal static class HarmonyInit
    {
        static HarmonyInit()
        {
            new Harmony("RGExpandedWorldGeneration.Mod").PatchAll();
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
        public static bool Prefix()
        {
            if (Page_CreateWorldParams_Patch.dirty && Find.WindowStack.WindowOfType<Page_CreateWorldParams>() != null)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Rand), "EnsureStateStackEmpty")]
    public static class EnsureStateStackEmpty_Patch
    {
        public static bool Prefix()
        {
            if (Page_CreateWorldParams_Patch.generatingWorld)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GenTemperature), "SeasonalShiftAmplitudeAt", null)]
    public static class GenTemperature_SeasonalShiftAmplitudeAt
    {
        public static bool Prefix()
        {
            return false;
        }

        [HarmonyPriority(int.MaxValue)]
        public static void Postfix(int tile, ref float __result)
        {
            if (Find.WorldGrid.LongLatOf(tile).y >= 0f)
            {
                __result = WorldComponent_WorldGenerator.mappedValues[WorldComponent_WorldGenerator.Instance.axialTilt].Evaluate(Find.WorldGrid.DistanceFromEquatorNormalized(tile));
                return;
            }
            __result = -WorldComponent_WorldGenerator.mappedValues[WorldComponent_WorldGenerator.Instance.axialTilt].Evaluate(Find.WorldGrid.DistanceFromEquatorNormalized(tile));
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), "Reset")]
    public static class Reset_Patch
    {
        public static void Postfix()
        {
            if (Page_CreateWorldParams_Patch.tmpWorldGenerationPreset != null)
            {
                Page_CreateWorldParams_Patch.tmpWorldGenerationPreset.Reset();
            }
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), "CanDoNext")]
    public static class CanDoNext_Patch
    {
        public static void Prefix()
        {
            if (Page_CreateWorldParams_Patch.thread != null)
            {
                Page_CreateWorldParams_Patch.thread.Abort();
                Page_CreateWorldParams_Patch.thread = null;
            }
            Page_CreateWorldParams_Patch.generatingWorld = false;
        }
    }

    [HarmonyPatch(typeof(Page), "DoBottomButtons")]
    public static class DoBottomButtons_Patch
    {
        public static bool Prefix(Page __instance, Rect rect, string nextLabel = null, string midLabel = null, Action midAct = null, bool showNext = true, bool doNextOnKeypress = true)
        {
            if (__instance is Page_CreateWorldParams createWorldParams)
            {
                Page_CreateWorldParams_Patch.DoBottomButtons(createWorldParams, rect, nextLabel, midLabel, midAct, showNext, doNextOnKeypress);
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

        public static WorldGenerationPreset tmpWorldGenerationPreset;

        public static Vector2 scrollPosition;

        public static bool dirty;

        public static Texture2D worldPreview;

        public static bool isActive;

        private static World threadedWorld;

        public static Thread thread;

        public static int updatePreviewCounter;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var planetCoverage = AccessTools.Field(typeof(Page_CreateWorldParams), "planetCoverage");
            var doGlobeCoverageSliderMethod = AccessTools.Method(typeof(Page_CreateWorldParams_Patch), "DoGlobeCoverageSlider");
            var doGuiMethod = AccessTools.Method(typeof(Page_CreateWorldParams_Patch), "DoGui");
            var endGroupMethod = AccessTools.Method(typeof(GUI), "EndGroup");
            var codes = instructions.ToList();
            bool found = false;

            for (var i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (codes[i].opcode == OpCodes.Ldloc_S && codes[i].operand is LocalBuilder lb && lb.LocalIndex == 9 
                    && codes[i + 2].LoadsField(planetCoverage))
                {
                    i += codes.FirstIndexOf(x => x.Calls(AccessTools.Method(typeof(WindowStack), "Add")) && codes.IndexOf(x) > i) - i;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 9);
                    yield return new CodeInstruction(OpCodes.Call, doGlobeCoverageSliderMethod);
                }
                else
                {
                    yield return code;
                }
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

        public static void DoBottomButtons(Page_CreateWorldParams window, Rect rect, string nextLabel = null, string midLabel = null, Action midAct = null, bool showNext = true, bool doNextOnKeypress = true)
        {
            float y = rect.y + rect.height - 38f;
            Text.Font = GameFont.Small;
            string label = "Back".Translate();
            var backRect = new Rect(rect.x, y, Page_CreateWorldParams.BottomButSize.x, Page_CreateWorldParams.BottomButSize.y);
            if ((Widgets.ButtonText(backRect, label) 
                || KeyBindingDefOf.Cancel.KeyDownEvent) && window.CanDoBack())
            {
                window.DoBack();
            }
            if (showNext)
            {
                if (nextLabel.NullOrEmpty())
                {
                    nextLabel = "Next".Translate();
                }
                Rect rect2 = new Rect(rect.x + rect.width - Page_CreateWorldParams.BottomButSize.x, y, Page_CreateWorldParams.BottomButSize.x, Page_CreateWorldParams.BottomButSize.y);
                if ((Widgets.ButtonText(rect2, nextLabel) || (doNextOnKeypress && KeyBindingDefOf.Accept.KeyDownEvent)) && window.CanDoNext())
                {
                    window.DoNext();
                }
                UIHighlighter.HighlightOpportunity(rect2, "NextPage");
            }

            var savePresetRect = new Rect(backRect.xMax + 100, y, Page_CreateWorldParams.BottomButSize.x, Page_CreateWorldParams.BottomButSize.y);
            string labelSavePreset = "RG.SavePreset".Translate();
            if (Widgets.ButtonText(savePresetRect, labelSavePreset))
            {
                var saveWindow = new Dialog_PresetList_Save(window);
                Find.WindowStack.Add(saveWindow);
            }

            var loadPresetRect = new Rect(savePresetRect.xMax + 15, y, Page_CreateWorldParams.BottomButSize.x, Page_CreateWorldParams.BottomButSize.y);
            string labelLoadPreset = "RG.LoadPreset".Translate();
            if (Widgets.ButtonText(loadPresetRect, labelLoadPreset))
            {
                var loadWindow = new Dialog_PresetList_Load(window);
                Find.WindowStack.Add(loadWindow);
            }

            var midActRect = new Rect(loadPresetRect.xMax + 15, y, Page_CreateWorldParams.BottomButSize.x, Page_CreateWorldParams.BottomButSize.y);
            if (midAct != null && Widgets.ButtonText(midActRect, midLabel))
            {
                midAct();
            }
        }

        private static void Postfix(Page_CreateWorldParams __instance)
        {
            DoWorldPreviewArea(__instance);
        }

        private static void DoGlobeCoverageSlider(Page_CreateWorldParams window, Rect rect)
        {
            var value = (double)Widgets.HorizontalSlider(rect, window.planetCoverage, 0.05f, 1, false, (window.planetCoverage * 100).ToString() + "%", "RG.Small".Translate(), "RG.Large".Translate()) * 100;
            window.planetCoverage = ((float)Math.Round(value / 5) * 5) / 100;
        }
        private static void DoGui(Page_CreateWorldParams window, ref float num, float width2)
        {
            isActive = true;
            window.absorbInputAroundWindow = false;
            UpdateCurPreset(window);
            DoSlider(0, ref num, width2, "RG.RiverDensity".Translate(), ref tmpWorldGenerationPreset.riverDensity, "None".Translate());
            DoSlider(0, ref num, width2, "RG.MountainDensity".Translate(), ref tmpWorldGenerationPreset.mountainDensity, "None".Translate());
            DoSlider(0, ref num, width2, "RG.SeaLevel".Translate(), ref tmpWorldGenerationPreset.seaLevel, "None".Translate());

            Rect labelRect;
            Rect slider;
            if (!ModCompat.MyLittlePlanetActive)
            {
                num += 40f;
                labelRect = new Rect(0, num, 200f, 30f);
                slider = new Rect(labelRect.xMax, num, width2, 30f);
                Widgets.Label(labelRect, "RG.AxialTilt".Translate());
                tmpWorldGenerationPreset.axialTilt = (AxialTilt)Mathf.RoundToInt(Widgets.HorizontalSlider(slider,
                    (float)tmpWorldGenerationPreset.axialTilt, 0f, AxialTiltUtility.EnumValuesCount - 1, middleAlignment: true,
                    "PlanetRainfall_Normal".Translate(), "PlanetRainfall_Low".Translate(), "PlanetRainfall_High".Translate(), 1f));
            }

            labelRect = new Rect(0f, num + 64, 80, 30);
            Widgets.Label(labelRect, "RG.Biomes".Translate());
            var outRect = new Rect(labelRect.x, labelRect.yMax - 3, width2 + 195, DoWindowContents_Patch.LowerWidgetHeight - 50);
            Rect viewRect = new Rect(outRect.x, outRect.y, outRect.width - 16f, (DefDatabase<BiomeDef>.DefCount * 90) + 10);
            Rect rect3 = new Rect(outRect.xMax - 200f - 16f, labelRect.y, 200f, Text.LineHeight);



            Widgets.DrawBoxSolid(new Rect(outRect.x, outRect.y, outRect.width - 16f, outRect.height), BackgroundColor);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            num = outRect.y + 15;
            foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefs.OrderBy(x => x.label ?? x.defName))
            {
                DoBiomeSliders(biomeDef, 10, ref num, biomeDef.label?.CapitalizeFirst() ?? biomeDef.defName);
            }
            Widgets.EndScrollView();

            if (!tmpWorldGenerationPreset.biomeCommonalities.All(x => x.Value == 10) || !tmpWorldGenerationPreset.biomeScoreOffsets.All(y => y.Value == 0))
            {
                if (Widgets.ButtonText(rect3, "ResetFactionsToDefault".Translate()))
                {
                    tmpWorldGenerationPreset.ResetBiomeCommonalities();
                    tmpWorldGenerationPreset.ResetBiomeScoreOffsets();
                }
            }

            if (RGExpandedWorldGenerationSettings.curWorldGenerationPreset is null)
            {
                RGExpandedWorldGenerationSettings.curWorldGenerationPreset = tmpWorldGenerationPreset.MakeCopy();
            }
            else if (RGExpandedWorldGenerationSettings.curWorldGenerationPreset.IsDifferentFrom(tmpWorldGenerationPreset))
            {
                RGExpandedWorldGenerationSettings.curWorldGenerationPreset = tmpWorldGenerationPreset.MakeCopy();
                updatePreviewCounter = 60;
                if (thread != null)
                {
                    thread.Abort();
                    thread = null;
                }
            }
            if (thread is null)
            {
                if (updatePreviewCounter == 0)
                {
                    StartRefreshWorldPreview(window);
                }
            }
            if (updatePreviewCounter > -2)
            {
                updatePreviewCounter--;
            }
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
            int numAttempt = 0;
            if (thread is null && Find.World != null && Find.World.info.name != "DefaultWorldName" || worldPreview != null)
            {
                if (dirty)
                {
                    while (numAttempt < 5)
                    {
                        worldPreview = GetWorldCameraPreview(Find.WorldCamera, WorldCameraHeight, WorldCameraWidth);
                        if (IsBlack(worldPreview))
                        {
                            numAttempt++;
                        }
                        else
                        {
                            dirty = false;
                            break;
                        }
                    }

                }
                if (worldPreview != null)
                {
                    GUI.DrawTexture(previewAreaRect, worldPreview);
                }
            }

            float numY = previewAreaRect.yMax - 40;
            DoSlider(previewAreaRect.x - 55, ref numY, 256, "RG.AncientRoadDensity".Translate(), ref tmpWorldGenerationPreset.ancientRoadDensity, "None".Translate());
            DoSlider(previewAreaRect.x - 55, ref numY, 256, "RG.FactionRoadDensity".Translate(), ref tmpWorldGenerationPreset.factionRoadDensity, "None".Translate());

            Rect labelRect;
            Rect slider;
            if (ModCompat.MyLittlePlanetActive)
            {
                numY += 40;
                labelRect = new Rect(previewAreaRect.x - 55, numY, 200f, 30f);
                slider = new Rect(labelRect.xMax, numY, 256, 30f);
                Widgets.Label(labelRect, "RG.AxialTilt".Translate());
                tmpWorldGenerationPreset.axialTilt = (AxialTilt)Mathf.RoundToInt(Widgets.HorizontalSlider(slider,
                    (float)tmpWorldGenerationPreset.axialTilt, 0f, AxialTiltUtility.EnumValuesCount - 1, middleAlignment: true,
                    "PlanetRainfall_Normal".Translate(), "PlanetRainfall_Low".Translate(), "PlanetRainfall_High".Translate(), 1f));
            }
        }

        public static void ApplyChanges(Page_CreateWorldParams window)
        {
            window.rainfall = tmpWorldGenerationPreset.rainfall;
            window.population = tmpWorldGenerationPreset.population;
            window.planetCoverage = tmpWorldGenerationPreset.planetCoverage;
            window.seedString = tmpWorldGenerationPreset.seedString;
            window.temperature = tmpWorldGenerationPreset.temperature;
            foreach (var data in tmpWorldGenerationPreset.factionCounts)
            {
                var factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(data.Key);
                if (factionDef != null)
                {
                    window.factionCounts[factionDef] = data.Value;
                }
            }
        }
        private static bool IsBlack(Texture2D texture)
        {
            var pixel = texture.GetPixel(texture.width / 2, texture.height / 2);
            return pixel.r <= 0 && pixel.g <= 0 && pixel.b <= 0;
        }
        private static void StartRefreshWorldPreview(Page_CreateWorldParams window)
        {
            dirty = false;
            updatePreviewCounter = -1;
            if (thread != null && thread.IsAlive)
            {
                thread.Abort();
                generatingWorld = false;
            }
            thread = new Thread(delegate ()
            {
                GenerateWorld(window.planetCoverage, window.seedString, window.rainfall, window.temperature, window.population, window.factionCounts);
            });
            thread.Start();
        }

        private static float texSpinAngle;
        private static void DrawGeneratePreviewButton(Page_CreateWorldParams window, Rect generateButtonRect)
        {
            if (thread != null)
            {
                if (texSpinAngle > 360f)
                {
                    texSpinAngle -= 360f;
                }
                texSpinAngle += 3;
            }
            if (Prefs.UIScale != 1f)
            {
                GUI.DrawTexture(generateButtonRect, GeneratePreview);
            }
            else
            {
                Widgets.DrawTextureRotated(generateButtonRect, GeneratePreview, texSpinAngle);
            }
            if (Mouse.IsOver(generateButtonRect))
            {
                Widgets.DrawHighlightIfMouseover(generateButtonRect);
                if (Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        StartRefreshWorldPreview(window);
                        Event.current.Use();
                    }
                }
            }
            if (thread != null && !thread.IsAlive && threadedWorld != null)
            {
                for (int i = 0; i < Find.World.renderer.layers.Count; i++)
                {
                    var layer = Find.World.renderer.layers[i];
                    if (layer is WorldLayer_Hills || layer is WorldLayer_Rivers || layer is WorldLayer_Roads || layer is WorldLayer_Terrain)
                    {
                        layer.RegenerateNow();
                    }
                }
                threadedWorld = null;
                thread = null;
                dirty = true;
                generatingWorld = false;
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

        public static bool generatingWorld;
        public static void GenerateWorld(float planetCoverage, string seedString, OverallRainfall overallRainfall, OverallTemperature overallTemperature, OverallPopulation population, Dictionary<FactionDef, int> factionCounts = null)
        {
            generatingWorld = true;
            Rand.PushState();
            int seed = (Rand.Seed = WorldGenerator.GetSeedFromSeedString(seedString));
            Find.GameInitData.ResetWorldRelatedMapInitData();
            try
            {
                Current.CreatingWorld = new World
                {
                    renderer = new WorldRenderer(),
                    UI = new WorldInterface(),
                };
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
                        if (!(ex is ThreadAbortException))
                        {
                            Log.Error("Error in WorldGenStep: " + ex);
                        }
                        else
                        {
                            Rand.PopState();
                            Current.CreatingWorld = null;
                            generatingWorld = false;
                            return;
                        }
                    }
                }
                threadedWorld = Current.CreatingWorld;
                Current.Game.World = threadedWorld;
                Find.World.features = new WorldFeatures();
                MemoryUtility.UnloadUnusedUnityAssets();
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadAbortException))
                {
                    Log.Error("Error: " + ex);
                }
                else
                {
                    if (Rand.stateStack.Any())
                    {
                        Rand.PopState();
                    }
                    generatingWorld = false;
                    Current.CreatingWorld = null;
                    return;
                }
            }
            finally
            {
                if (Rand.stateStack.Any())
                {
                    Rand.PopState();
                }
                generatingWorld = false;
                Current.CreatingWorld = null;
            }
        }
        private static Texture2D GetWorldCameraPreview(Camera worldCamera, int width, int height)
        {
            Find.World.renderer.wantedMode = WorldRenderMode.Planet;
            Find.WorldCamera.gameObject.SetActive(true);
            Find.World.UI.Reset();
            Find.WorldCameraDriver.desiredAltitude = 800;
            Find.WorldCameraDriver.altitude = 800;
            Find.WorldCameraDriver.ApplyPositionToGameObject();

            Rect rect = new Rect(0, 0, width, height);
            RenderTexture renderTexture = new RenderTexture(width, height, 24);
            Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGBA32, false);

            worldCamera.targetTexture = renderTexture;
            worldCamera.Render();

            ExpandableWorldObjectsUtility.ExpandableWorldObjectsUpdate();
            Find.World.renderer.DrawWorldLayers();
            Find.World.dynamicDrawManager.DrawDynamicWorldObjects();
            Find.World.features.UpdateFeatures();
            NoiseDebugUI.RenderPlanetNoise();

            RenderTexture.active = renderTexture;
            screenShot.ReadPixels(rect, 0, 0);
            screenShot.Apply();
            worldCamera.targetTexture = null;
            RenderTexture.active = null;

            Find.WorldCamera.gameObject.SetActive(false);
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            return screenShot;
        }
        private static void UpdateCurPreset(Page_CreateWorldParams window)
        {
            if (tmpWorldGenerationPreset is null)
            {
                tmpWorldGenerationPreset = new WorldGenerationPreset();
                tmpWorldGenerationPreset.Init();
            };
            tmpWorldGenerationPreset.factionCounts = window.factionCounts.ToDictionary(x => x.Key.defName, y => y.Value);
            tmpWorldGenerationPreset.temperature = window.temperature;
            tmpWorldGenerationPreset.seedString = window.seedString;
            tmpWorldGenerationPreset.planetCoverage = window.planetCoverage;
            tmpWorldGenerationPreset.rainfall = window.rainfall;
            tmpWorldGenerationPreset.population = window.population;
        }
        private static void DoSlider(float x, ref float num, float width2, string label, ref float field, string leftLabel)
        {
            num += 40f;
            var labelRect = new Rect(x, num, 200f, 30f);
            Widgets.Label(labelRect, label);
            Rect slider = new Rect(labelRect.xMax, num, width2, 30f);
            field = Widgets.HorizontalSlider(slider, (int)(field * 3f), 0, 6, middleAlignment: true, 
                "PlanetRainfall_Normal".Translate(), leftLabel, "PlanetRainfall_High".Translate(), 1f) / 3f;

        }
        private static void DoBiomeSliders(BiomeDef biomeDef, float x, ref float num, string label)
        {
            var labelRect = new Rect(x, num - 10, 200f, 30f);
            Widgets.Label(labelRect, label);
            num += 10;
            Rect biomeCommonalityLabel = new Rect(labelRect.x, num + 5, 70, 30);
            var value = tmpWorldGenerationPreset.biomeCommonalities[biomeDef.defName];
            if (value < 10f)
            {
                GUI.color = Color.red;
            }
            else if (value > 10f)
            {
                GUI.color = Color.green;
            }
            Widgets.Label(biomeCommonalityLabel, "RG.Commonality".Translate());
            Rect biomeCommonalitySlider = new Rect(biomeCommonalityLabel.xMax + 5, num, 340, 30f);
            tmpWorldGenerationPreset.biomeCommonalities[biomeDef.defName] = (int)Widgets.HorizontalSlider(biomeCommonalitySlider, value, 0, 20, false, (value * 10).ToString() + "%");
            GUI.color = Color.white;
            num += 30f;

            Rect biomeOffsetLabel = new Rect(labelRect.x, num + 5, 70, 30);
            var value2 = tmpWorldGenerationPreset.biomeScoreOffsets[biomeDef.defName];
            if (value2 < 0f)
            {
                GUI.color = Color.red;
            }
            else if (value2 > 0f)
            {
                GUI.color = Color.green;
            }
            Widgets.Label(biomeOffsetLabel, "RG.ScoreOffset".Translate());
            Rect scoreOffsetSlider = new Rect(biomeOffsetLabel.xMax + 5, biomeCommonalitySlider.yMax, 340, 30f);
            tmpWorldGenerationPreset.biomeScoreOffsets[biomeDef.defName] = (int)Widgets.HorizontalSlider(scoreOffsetSlider, value2, -99, 99, false, value2.ToString());
            GUI.color = Color.white;
            num += 50f;
        }
    }
}
