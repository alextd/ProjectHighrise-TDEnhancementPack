using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;

using UnityEngine;
using UnityEngine.UI;
using Game.UI.Session.HUD;
using Game.Util;
using Game.Session.Entities;
using Game.Session.Entities.Components;
using Game.Session.Board;
using Game.Session.Sim;
using Game.Services;

namespace BetterPlacement
{
	public class TickerEntryUtility : TickerEntry
	{
		public HashSet<NotifType> warnings = new HashSet<NotifType>();
		public int id;

		public TickerEntryUtility(int i)
			: base(float.PositiveInfinity)
		{
			id = i;
		}

		public override GameObject CreateGameObject(GameObject container)
		{
			//TickerEntryText close enough prefab.
			return UIUtil.MakeUIPrefab(UIPrefab.TickerEntryText, container);
		}

		public override void InitializeGameObject()
		{
			UIUtil.GetButton(go, "Close").onClick.SetListener(OnClose);
			UIUtil.GetTextMesh(go, "Text").text = "Utilities Needed";
			//UIUtil.GetTextMesh(go, "Icon").text = Loc.Get(iconkey);

			UIUtil.GetChild(go, "Text")
				.AddComponent<UnityEngine.UI.Button>()
				.onClick.AddListener(delegate () {
					GoToEntity();
				});

			UIUtil.GetChild(go, "Ok Button").SetActive(false);
		}

		public override void ReleaseGameObject()
		{
			UIUtil.GetButton(go, "Close").onClick.RemoveAllListeners();
			UIUtil.GetChild(go, "Text").GetComponent<UnityEngine.UI.Button>().onClick.RemoveAllListeners();
		}

		private void OnClose()
		{
			UtilityTickerTracker.Remove(id);
		}

		public void Add(NotifType type)
		{
			warnings.Add(type);
			Refresh();
		}
		public void Remove(NotifType type)
		{
			warnings.Remove(type);
			Refresh();
		}

		/*
		//It looks like the warning icons are prefab so nevermind.
		public static string IconFor(NotifType type)
		{
			return Loc.Get("ui.icon.utilhvac");
		}
		*/

		public void GoToEntity()
		{
			Game.Game.serv.camera.SetPosition((Vector2)Game.Game.ctx.entityman.Find(id).data.placement.worldpos, false);
		}

		public bool IsEmpty => warnings.Count == 0;

		public void Refresh()
		{
			//Set Text
			Entity e = Game.Game.ctx.entityman.Find(id);
			string name = e.data.unit?.instance?.name ?? Loc.Get(e.config.unit.locname);
			UIUtil.GetTextMesh(go, "Text").text = $"{name} has complaints:\n{string.Join("\n", warnings.Select(t => MakeComplaint(t)).ToArray())}";

			//MakeIcon(warnings.First());
		}


		public static bool HasComplaint(NotifType type)
		{
			switch (type)
			{
				case NotifType.UnitUpset: 
				case NotifType.TrashProblem: 
				case NotifType.ServiceOverloaded: 
				case NotifType.NeedConnPower: 
				case NotifType.NeedConnPhone: 
				case NotifType.NeedConnCable: 
				case NotifType.NeedConnWater: 
				case NotifType.NeedConnGas: 
				case NotifType.NeedConnHVAC: 
					return true;
			}
			return false;
		}
		public static string MakeComplaint(NotifType type)
		{
			switch(type)
			{
				case NotifType.None: return "None! I don't know what this is doing here.";
				case NotifType.MoveInOffice: return "Office needs move-in";
				case NotifType.MoveInRetail: return "Retail needs move-in";
				case NotifType.MoveInRestaurant: return "Restaurent needs move-in";
				case NotifType.MoveInApartment: return "Apartment needs move-in";
				case NotifType.MoveInPending: return "Pending? needs move-in";
				case NotifType.UnitUpset: return "Upset - service?";
				case NotifType.TrashProblem: return "Trash problem";
				case NotifType.ServiceOverloaded: return "Service Overloaded";
				case NotifType.NotEnoughPower: return "Not enough power";
				case NotifType.NotEnoughPhone: return "Not enough phone lines";
				case NotifType.NotEnoughCable: return "Not enough cable lines";
				case NotifType.NotEnoughWater: return "Not enough water";
				case NotifType.NotEnoughGas: return "Not enough gas";
				case NotifType.NotEnoughHVAC: return "Not enough HVAC";
				case NotifType.NeedConnPower: return "Needs power connection";
				case NotifType.NeedConnPhone: return "Needs phone connection";
				case NotifType.NeedConnCable: return "Needs cable connection";
				case NotifType.NeedConnWater: return "Needs water connection";
				case NotifType.NeedConnGas: return "Needs gas connection";
				case NotifType.NeedConnHVAC: return "Needs HVAC connection";
				case NotifType.MoveInHotelRoom: return "Hotel needs move-in";
			}
			return "I dunno, sorry";
		}
	}

	public static class UtilityTickerTracker
	{
		//should be game component or whatever but there's no modding framework for that sort of thing, static here we go.
		public static Dictionary<int, TickerEntryUtility> tickers = new Dictionary<int, TickerEntryUtility>();

		public static void Add(int id, NotifType type)
		{
			TickerEntryUtility ticker;
			if (!tickers.TryGetValue(id, out ticker))
			{
				ticker = new TickerEntryUtility(id);
				Game.Game.ctx.hud.tickers.Add(ticker);
				tickers[id] = ticker;
			}
			ticker.Add(type);
		}

		public static void Remove(int id, NotifType type)
		{
			if (tickers.TryGetValue(id, out TickerEntryUtility ticker))
			{
				ticker.Remove(type);

				if (ticker.IsEmpty)
				{
					tickers.Remove(id);
					Game.Game.ctx.hud.tickers.Remove(ticker);
				}
			}
		}

		public static void Remove(int id)
		{
			if (tickers.TryGetValue(id, out TickerEntryUtility ticker))
			{
				tickers.Remove(id);
				Game.Game.ctx.hud.tickers.Remove(ticker);
			}
		}
	}


	[HarmonyPatch(typeof(NotifComponent), nameof(NotifComponent.Add))]
	public static class ToggleTickerAdd
	{
		//public void Add(NotifType type)
		public static void Prefix(NotifComponent __instance, NotifType type)
		{
			if (__instance.IsAdded(type) || __instance.config.IsInhibited(type)) return;

			if (!TickerEntryUtility.HasComplaint(type)) return;//Movein/not enough should be ignored as a popup.

			UtilityTickerTracker.Add(__instance.entity.id, type);
		}
	}

	[HarmonyPatch(typeof(NotifComponent), nameof(NotifComponent.Remove))]
	public static class ToggleTickerRemove
	{
		//public void Remove(NotifType type)
		public static void Prefix(NotifComponent __instance, NotifType type)
		{
			if (!__instance.IsAdded(type)) return;

			UtilityTickerTracker.Remove(__instance.entity.id, type);
		}
	}


	[HarmonyPatch(typeof(NotifComponent), nameof(NotifComponent.OnBeforeEntityRemoved))]
	public static class ToggleTickerDestroy
	{
		//public override void OnBeforeEntityRemoved(bool shutdown)
		public static void Prefix(NotifComponent __instance)
		{
			UtilityTickerTracker.Remove(__instance.entity.id);
		}
	}
}
