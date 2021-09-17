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
using Verse.Sound;
using static Verse.Widgets;

namespace RGExpandedWorldGeneration
{
	public enum AxialTilt
	{
		VeryLow,
		Low,
		Normal,
		High,
		VeryHigh
	}
	public class WorldComponent_WorldGenerator : WorldComponent
	{
		public AxialTilt axialTilt = AxialTilt.Normal;

		public static Dictionary<AxialTilt, SimpleCurve> mappedValues = new Dictionary<AxialTilt, SimpleCurve>
		{
			{ 
				AxialTilt.VeryLow, new SimpleCurve()
				{
					{ new CurvePoint(0f, 0.75f), true },
					{ new CurvePoint(0.1f, 1f), true },
					{ new CurvePoint(1f, 7f), true }
				}
			},
			{
				AxialTilt.Low, new SimpleCurve()
				{
					{ new CurvePoint(0f, 1.5f), true },
					{ new CurvePoint(0.1f, 2f), true },
					{ new CurvePoint(1f, 14f), true }
				}
			},
			{
				AxialTilt.Normal, new SimpleCurve()
				{
					{ new CurvePoint(0f, 3f), true },
					{ new CurvePoint(0.1f, 4f), true },
					{ new CurvePoint(1f, 28f), true }
				}
			},
			{
				AxialTilt.High, new SimpleCurve()
				{
					{ new CurvePoint(0f, 4.5f), true },
					{ new CurvePoint(0.1f, 6f), true },
					{ new CurvePoint(1f, 42f), true }
				}
			},
			{
				AxialTilt.VeryHigh, new SimpleCurve()
				{
					{ new CurvePoint(0f, 6f), true },
					{ new CurvePoint(0.1f, 8f), true },
					{ new CurvePoint(1f, 56f), true }
				}
			}
		};

		public static WorldComponent_WorldGenerator Instance;
		public WorldComponent_WorldGenerator(World world) : base(world)
		{
			Instance = this;
		}

		public bool worldGenerated;
        public override void FinalizeInit()
        {
            base.FinalizeInit();
			if (!worldGenerated && RGExpandedWorldGenerationSettings.curWorldGenerationPreset != null)
            {
				axialTilt = RGExpandedWorldGenerationSettings.curWorldGenerationPreset.axialTilt;
				worldGenerated = true;
			}
		}

        public override void ExposeData()
		{
			Scribe_Values.Look(ref worldGenerated, "worldGenerated");
			Scribe_Values.Look(ref axialTilt, "axialTilt", AxialTilt.Normal, true);
			Instance = this;
		}
	}
}
