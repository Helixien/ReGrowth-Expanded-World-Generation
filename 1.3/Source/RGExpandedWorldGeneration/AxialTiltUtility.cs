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
	public static class AxialTiltUtility
	{
		private static int cachedEnumValuesCount = -1;

		public static int EnumValuesCount
		{
			get
			{
				if (AxialTiltUtility.cachedEnumValuesCount < 0)
				{
					AxialTiltUtility.cachedEnumValuesCount = Enum.GetNames(typeof(AxialTilt)).Length;
				}
				return AxialTiltUtility.cachedEnumValuesCount;
			}
		}
	}
}
