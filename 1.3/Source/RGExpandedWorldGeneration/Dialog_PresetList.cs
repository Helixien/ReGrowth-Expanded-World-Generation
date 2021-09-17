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
using static RimWorld.Planet.WorldGenStep_Roads;

namespace RGExpandedWorldGeneration
{
	public class Dialog_PresetList_Load : Dialog_PresetList
	{
		public Dialog_PresetList_Load(Page_CreateWorldParams parent) : base(parent)
		{
			interactButLabel = "LoadGameButton".Translate();
		}

		protected override void DoPresetInteraction(string name)
		{
			Page_CreateWorldParams_Patch.tmpWorldGenerationPreset = RGExpandedWorldGenerationSettingsMod.settings.presets[name];
			Log.Message("RGExpandedWorldGenerationSettingsMod.settings.presets[name]: " + RGExpandedWorldGenerationSettingsMod.settings.presets[name] + " - " + name + " - " + RGExpandedWorldGenerationSettingsMod.settings.presets[name].seaLevel);
			Page_CreateWorldParams_Patch.ApplyChanges(parent);
			Close();
		}
	}

	public class Dialog_PresetList_Save : Dialog_PresetList
	{
		protected override bool ShouldDoTypeInField => true;
		public Dialog_PresetList_Save(Page_CreateWorldParams parent) : base(parent)
		{
			interactButLabel = "OverwriteButton".Translate();
		}

		protected override void DoPresetInteraction(string name)
		{
			RGExpandedWorldGenerationSettingsMod.settings.presets[name] = RGExpandedWorldGenerationSettings.curWorldGenerationPreset.MakeCopy();
			Messages.Message("SavedAs".Translate(name), MessageTypeDefOf.SilentInput, historical: false);
			RGExpandedWorldGenerationSettingsMod.settings.Write();
			Close();
		}
	}

	public abstract class Dialog_PresetList : Window
	{
		protected string interactButLabel = "Error";

		protected float bottomAreaHeight;

		protected Vector2 scrollPosition = Vector2.zero;

		protected string typingName = "";

		private bool focusedNameArea;

		protected const float EntryHeight = 40f;

		protected const float FileNameLeftMargin = 8f;

		protected const float FileNameRightMargin = 4f;

		protected const float FileInfoWidth = 94f;

		protected const float InteractButWidth = 100f;

		protected const float InteractButHeight = 36f;

		protected const float DeleteButSize = 36f;

		private static readonly Color DefaultFileTextColor = new Color(1f, 1f, 0.6f);

		protected const float NameTextFieldWidth = 400f;

		protected const float NameTextFieldHeight = 35f;

		protected const float NameTextFieldButtonSpace = 20f;
		public override Vector2 InitialSize => new Vector2(620f, 700f);
		protected virtual bool ShouldDoTypeInField => false;

		protected Page_CreateWorldParams parent;
		public Dialog_PresetList(Page_CreateWorldParams parent)
		{
			doCloseButton = true;
			doCloseX = true;
			forcePause = true;
			absorbInputAroundWindow = true;
			closeOnAccept = false;
			this.parent = parent;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Vector2 vector = new Vector2(inRect.width - 16f, 40f);
			float y = vector.y;
			var presets = RGExpandedWorldGenerationSettingsMod.settings.presets;
			float height = (float)presets.Count * y;
			Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, height);
			float num = inRect.height - Window.CloseButSize.y - bottomAreaHeight - 18f;
			if (ShouldDoTypeInField)
			{
				num -= 53f;
			}
			Rect outRect = inRect.TopPartPixels(num);
			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
			float num2 = 0f;
			int num3 = 0;
			foreach (var preset in presets.Keys.ToList())
			{
				if (num2 + vector.y >= scrollPosition.y && num2 <= scrollPosition.y + outRect.height)
				{
					Rect rect = new Rect(0f, num2, vector.x, vector.y);
					if (num3 % 2 == 0)
					{
						Widgets.DrawAltRect(rect);
					}
					GUI.BeginGroup(rect);
					Rect rect2 = new Rect(rect.width - 36f, (rect.height - 36f) / 2f, 36f, 36f);
					if (Widgets.ButtonImage(rect2, TexButton.DeleteX, Color.white, GenUI.SubtleMouseoverColor))
					{
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDelete".Translate(preset), delegate
						{
							RGExpandedWorldGenerationSettingsMod.settings.presets.Remove(preset);
						}, destructive: true));
					}
					Text.Font = GameFont.Small;
					Rect rect3 = new Rect(rect2.x - 100f, (rect.height - 36f) / 2f, 100f, 36f);
					if (Widgets.ButtonText(rect3, interactButLabel))
					{
						DoPresetInteraction(preset);
					}
					Rect rect4 = new Rect(rect3.x - 94f, 0f, 94f, rect.height);
					GUI.color = Color.white;
					Text.Anchor = TextAnchor.UpperLeft;
					Rect rect5 = new Rect(8f, 0f, rect4.x - 8f - 4f, rect.height);
					Text.Anchor = TextAnchor.MiddleLeft;
					Text.Font = GameFont.Small;
					string fileNameWithoutExtension = preset;
					Widgets.Label(rect5, fileNameWithoutExtension.Truncate(rect5.width * 1.8f));
					GUI.color = Color.white;
					Text.Anchor = TextAnchor.UpperLeft;
					GUI.EndGroup();
				}
				num2 += vector.y;
				num3++;
			}
			Widgets.EndScrollView();
			if (ShouldDoTypeInField)
			{
				DoTypeInField(inRect.TopPartPixels(inRect.height - Window.CloseButSize.y - 18f));
			}
		}

		protected virtual void DoTypeInField(Rect rect)
		{
			GUI.BeginGroup(rect);
			bool flag = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
			float y = rect.height - 35f;
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleLeft;
			GUI.SetNextControlName("MapNameField");
			string str = Widgets.TextField(new Rect(5f, y, 400f, 35f), typingName);
			if (GenText.IsValidFilename(str))
			{
				typingName = str;
			}
			if (!focusedNameArea)
			{
				UI.FocusControl("MapNameField", this);
				focusedNameArea = true;
			}
			if (Widgets.ButtonText(new Rect(420f, y, rect.width - 400f - 20f, 35f), "SaveGameButton".Translate()) || flag)
			{
				if (typingName.NullOrEmpty())
				{
					Messages.Message("NeedAName".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				}
				else
				{
					DoPresetInteraction(typingName);
				}
			}
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.EndGroup();
		}
		protected abstract void DoPresetInteraction(string name);

		protected virtual Color FileNameColor(SaveFileInfo sfi)
		{
			return DefaultFileTextColor;
		}
	}
}
