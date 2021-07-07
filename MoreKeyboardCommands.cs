using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using UnityEngine;
using Game.Services.Input;
using Game.Services;
using Game.Services.Settings;
using Game.Session.Entities;
using Game.UI.Session.HUD;
using Game.UI.Session;
using Assets.Code.Music;
using static UnityEngine.KeyCode;

namespace TDEnhancementPack
{
	[HarmonyPatch(typeof(KeyboardShortcutManager), nameof(KeyboardShortcutManager.Initialize))]
	public static class FunctionKeyActions
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			ConstructorInfo listCtorInfo = AccessTools.Constructor(typeof(List<KeyInput>));

			foreach (var inst in instructions)
			{
				yield return inst;

				if (inst.Is(OpCodes.Newobj, listCtorInfo))
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FunctionKeyActions), nameof(AddInputs)));
			}
		}

		public static List<KeyInput> AddInputs(List<KeyInput> list)
		{
			list.AddRange(
				new KeyCode[] { F1, F2, F3, F4, F5, F6, F7, F8, F9, F10,
				Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9}
				.Select(c => new KeyInput(c, () => SelectMenu(c>= F1, c >= F1 ? c - F1: c-Alpha1)))
				);
			list.Add(new KeyInput(BackQuote, () => ClearIt()));
			return list;
		}

		public static UIBuildGroup currentBuildGroup = null;
		public static void SelectMenu(bool fkey, int x)
		{
			BuildRibbon ribbon = Game.Game.ctx.hud.newbuild;
			BuildSubController controller = ribbon.ctrl;
			object selection = null;

			//Two step process since items come from various places but take the same action:
			{//First, select what we're going to do

				//Use BuildSubController if it's active (subribbons show here)
				if (controller.subshowing)
				{
					if (fkey && controller.category.tabs.Count > 1)
					{
						//Select tab:
						selection = controller.category.tabs.FindAll(t => t.visreqs.AllSatisfied())
							.ElementAtOrDefault(x);
					}
					else
					{
						//Select button:
						selection = controller.tab.elements
							//ignore text and spacers:
							.FindAll(d =>
								(d.type == UIBuildElementType.Button || d.type == UIBuildElementType.ButtonPreview)
								&& d.visreqs.AllSatisfied())
							.ElementAtOrDefault(x);
					}
				}
				else if (currentBuildGroup != null)
				{
					//Select category:

					//button layout and index is not what I'd expect.
					//Input			Index
					//0 1 2			0 2 4
					//3 4		=>	1	3   
					//0 1				0	2
					//2 3		=>	1	3
					int half = (currentBuildGroup.group.Count + 1) / 2;
					int index = x < half ? 2 * x : 2 * (x - half) + 1;


					selection = currentBuildGroup.group
							.FindAll(d =>
								(d.type == UIBuildElementType.Button || d.type == UIBuildElementType.ButtonPreview)
								&& d.visreqs.AllSatisfied())
							.ElementAtOrDefault(index);
				}
				else
				{
					//Select group:
					selection = Game.Game.serv.globals.settings.ui.mainribbon.ElementAtOrDefault(x);
				}
			}


			{//Then activate the selection
				if (selection == null)
				{
					ClearIt();
				}
				else if (selection is UIBuildGroup group)
				{
					currentBuildGroup = group;
					ribbon.RefreshButtons();//To highlight them
				}
				else if (selection is UIBuildElementDef def)
				{
					if (!def.CanDo())
					{
						//Cancel that
						Game.Game.serv.audio.PlayUISFX(UIEffectType.CantBuildHere);
						return;
					}
					//UIBuildElementDef is used whether it's a mainribbon click or a subribbon click and there isn't one place to handle both.
					//Clicked the main ribbon? Subribbon or Callbacks only, 
					//Game.Game.ctx.hud.newbuild.SetActive(elem);

					//Clicked a subribbon?
					//BuildSubController . OnRibbonButtonClick, PlaceTemple or Callback only.

					//I'll just manually call what those methods call.
					switch (def.action)
					{
						case UIBuildElementAction.Callback:
							//Hide it
							ClearIt();

							//Do it
							controller.ProcessCallback(def);
							break;
						case UIBuildElementAction.PlaceTemplate:
							//Hide it
							ClearIt();

							//Do it
							controller.PlaceTemplate(def);
							break;
						case UIBuildElementAction.Subribbon:
							//Show it
							controller.SetActiveSubribbon(def.GetActionArg(0));
							break;
					}
				}
				else if (selection is UIBuildCategoryTab tab)
				{
					controller.SetActiveContentsAndTab(controller.category, tab);
				}
			}
		}

		public static bool CanDo(this UIBuildElementDef def)
		{
			//Similar to public States RefreshState(UIBuildElementDef def, bool isInit)
			return (def.reqs.Count == 0 || def.reqs.AllSatisfied()) &&
				(def.action != UIBuildElementAction.PlaceTemplate || FeedbackUtil.CanTemplateBePlacedSomewhere(def.GetActionArg(0)));
		}

		//SetActiveSubribbon sets input mode, so do this before actions
		public static void ClearIt()
		{
			//Hide it
			Game.Game.ctx.hud.newbuild.ctrl.SetActiveSubribbon(null);

			//currentBuildMenu = null; //Called in prefix for SetActiveSubribbon(null)
		}
	}

	[HarmonyPatch(typeof(BuildSubController), nameof(BuildSubController.SetActiveContentsAndTab))]
	public static class ClearKeyboardBuildMenu
	{
		//private void SetActiveContentsAndTab(UIBuildCategory newcat, UIBuildCategoryTab newtab)

		public static void Prefix(UIBuildCategory newcat, UIBuildCategoryTab newtab)
		{
			FunctionKeyActions.currentBuildGroup = null;
			Game.Game.ctx?.hud?.newbuild?.RefreshButtons();
		}
	}

	[HarmonyPatch(typeof(BuildButtonState), nameof(BuildButtonState.ShouldHighlight))]
	public static class HighlightSelectedGroup
	{
		//public static bool ShouldHighlight(UIBuildElementDef def)
		public static bool Postfix(bool __result, UIBuildElementDef def)
		{
			//TODO: Nice box highlight but this will do.
			return __result || 
				(FunctionKeyActions.currentBuildGroup?.group.Contains(def) ?? false);
		}
	}

	/*
	 * TODO if I want user-changeabl keymappings here they are.
	//Let's edit the default mappings. That's a statically-constructed thing so is unpatchable. So let's edit the first usage of it.
	[HarmonyPatch(typeof(KeyMapper), MethodType.Constructor)]
	public static class MoreKeyboardCommands
	{
		public static bool patched = false;
		public static void Prefix()
		{
			if (patched) return;

			KeyMapper.DEFAULT_MAPPINGS.Add(new KeyMappingTuple((KeyAction)51, KeyCode.F1));
			patched = true;
		}
	}
	*/

	/*
	// Localization isn't loaded until a game is started so changing strings in main menu is bad time.
	[HarmonyPatch(typeof(LocalizationSet),nameof(LocalizationSet.AppendLanguageData))]
	public static class TEST1
	{
		//public void AppendLanguageData(Hashtable data, string lang)
		public static void Prefix(string lang)
		{
			Log.Debug($"Appending {lang}");
		}
	}
	*/
}
