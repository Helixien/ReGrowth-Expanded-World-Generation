using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RGExpandedWorldGeneration
{
    class RGExpandedWorldGenerationSettings : ModSettings
    {
        public static WorldGenerationPreset curWorldGenerationPreset;

        public Dictionary<string, WorldGenerationPreset> presets;
        public RGExpandedWorldGenerationSettings()
        {
            presets = new Dictionary<string, WorldGenerationPreset>();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref presets, "presets", LookMode.Value, LookMode.Deep);
        }
    }
}

