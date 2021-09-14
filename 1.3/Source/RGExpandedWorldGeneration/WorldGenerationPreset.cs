using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RGExpandedWorldGeneration
{
    public class WorldGenerationPreset : IExposable
    {
		public Dictionary<string, int> factionCounts;
		public Dictionary<string, float> biomeCommonalities;
		public string seedString;
		public float planetCoverage;
		public OverallRainfall rainfall;
		public OverallTemperature temperature;
		public OverallPopulation population;
		public float riverDensity;
		public float ancientRoadDensity;
		public float factionRoadDensity;
		public float mountainDensity;
		public float seaLevel;

		public void Init()
        {
			seedString = GenText.RandomSeedString();
			planetCoverage = ((!Prefs.DevMode || !UnityData.isEditor) ? 0.3f : 0.05f);
			rainfall = OverallRainfall.Normal;
			temperature = OverallTemperature.Normal;
			population = OverallPopulation.Normal;
			riverDensity = 1f;
			ancientRoadDensity = 1f;
			factionRoadDensity = 1f;
			mountainDensity = 1f;
			seaLevel = 1f;
			ResetFactionCounts();
			ResetBiomeCommonalities();
		}
		private void ResetFactionCounts()
		{
			factionCounts = new Dictionary<string, int>();
			foreach (FactionDef configurableFaction in FactionGenerator.ConfigurableFactions)
			{
				factionCounts.Add(configurableFaction.defName, configurableFaction.startingCountAtWorldCreation);
			}
		}

		private void ResetBiomeCommonalities()
		{
			biomeCommonalities = new Dictionary<string, float>();
			foreach (BiomeDef biomeDef in DefDatabase<BiomeDef>.AllDefs)
			{
				biomeCommonalities.Add(biomeDef.defName, 1f);
			}
		}
		public void ExposeData()
        {
			Scribe_Collections.Look(ref factionCounts, "factionCounts", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref biomeCommonalities, "biomeCommonalities", LookMode.Value, LookMode.Value);
			Scribe_Values.Look(ref seedString, "seedString");
			Scribe_Values.Look(ref planetCoverage, "planetCoverage");
			Scribe_Values.Look(ref rainfall, "rainfall");
			Scribe_Values.Look(ref temperature, "temperature");
			Scribe_Values.Look(ref population, "population");
			Scribe_Values.Look(ref riverDensity, "riverDensity");
			Scribe_Values.Look(ref ancientRoadDensity, "ancientRoadDensity");
			Scribe_Values.Look(ref factionRoadDensity, "settlementRoadDensity");
			Scribe_Values.Look(ref mountainDensity, "mountainDensity");
			Scribe_Values.Look(ref seaLevel, "seaLevel");
		}
	}
}
