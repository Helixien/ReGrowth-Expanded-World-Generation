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
		public Dictionary<string, int> biomeCommonalities;
		public Dictionary<string, int> biomeScoreOffsets;
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
		public AxialTilt axialTilt;
		public void Init()
        {
			seedString = GenText.RandomSeedString();
			planetCoverage = 0.3f;
			rainfall = OverallRainfall.Normal;
			temperature = OverallTemperature.Normal;
			population = OverallPopulation.Normal;
			axialTilt = AxialTilt.Normal;
			ResetFactionCounts();
			Reset();
		}

		public void Reset()
        {
			riverDensity = 1f;
			ancientRoadDensity = 1f;
			factionRoadDensity = 1f;
			mountainDensity = 1f;
			seaLevel = 1f;
			axialTilt = AxialTilt.Normal;
			ResetBiomeCommonalities();
			ResetBiomeScoreOffsets();
		}

		public bool IsDifferentFrom(WorldGenerationPreset other)
		{
			if (seedString != other.seedString || planetCoverage != other.planetCoverage || rainfall != other.rainfall || temperature != other.temperature
				|| population != other.population || riverDensity != other.riverDensity || ancientRoadDensity != other.ancientRoadDensity
				|| factionRoadDensity != other.factionRoadDensity || mountainDensity != other.mountainDensity || seaLevel != other.seaLevel || axialTilt != other.axialTilt)
            {
				return true;
            }

			if (factionCounts.Count != other.factionCounts.Count || !factionCounts.ContentEquals(other.factionCounts))
			{
				return true;
			}
			if (biomeCommonalities.Count != other.biomeCommonalities.Count || !biomeCommonalities.ContentEquals(other.biomeCommonalities))
			{
				return true;
            }
			if (biomeScoreOffsets.Count != other.biomeScoreOffsets.Count || !biomeScoreOffsets.ContentEquals(other.biomeScoreOffsets))
			{
				return true;
			}
			return false;
		}

		public WorldGenerationPreset MakeCopy()
        {
			var copy = new WorldGenerationPreset();
			copy.factionCounts = this.factionCounts.ToDictionary(x => x.Key, y => y.Value);
			copy.biomeCommonalities = this.biomeCommonalities.ToDictionary(x => x.Key, y => y.Value);
			copy.biomeScoreOffsets = this.biomeScoreOffsets.ToDictionary(x => x.Key, y => y.Value);
			copy.seedString = this.seedString;
			copy.planetCoverage = this.planetCoverage;
			copy.rainfall = this.rainfall;
			copy.temperature = this.temperature;
			copy.population = this.population;
			copy.riverDensity = this.riverDensity;
			copy.ancientRoadDensity = this.ancientRoadDensity;
			copy.factionRoadDensity = this.factionRoadDensity;
			copy.mountainDensity = this.mountainDensity;
			copy.seaLevel = this.seaLevel;
			copy.axialTilt = this.axialTilt;
			return copy;
		}
		private void ResetFactionCounts()
		{
			factionCounts = new Dictionary<string, int>();
			foreach (FactionDef configurableFaction in FactionGenerator.ConfigurableFactions)
			{
				factionCounts.Add(configurableFaction.defName, configurableFaction.startingCountAtWorldCreation);
			}
		}

		public void ResetBiomeCommonalities()
		{
			biomeCommonalities = new Dictionary<string, int>();
			foreach (BiomeDef biomeDef in DefDatabase<BiomeDef>.AllDefs)
			{
				biomeCommonalities.Add(biomeDef.defName, 10);
			}
		}

		public void ResetBiomeScoreOffsets()
		{
			biomeScoreOffsets = new Dictionary<string, int>();
			foreach (BiomeDef biomeDef in DefDatabase<BiomeDef>.AllDefs)
			{
				biomeScoreOffsets.Add(biomeDef.defName, 0);
			}
		}
		public void ExposeData()
        {
			Scribe_Collections.Look(ref factionCounts, "factionCounts", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref biomeCommonalities, "biomeCommonalities", LookMode.Value, LookMode.Value);
			Scribe_Collections.Look(ref biomeScoreOffsets, "biomeScoreOffsets", LookMode.Value, LookMode.Value);
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
			Scribe_Values.Look(ref axialTilt, "axialTilt");
		}
	}
}
