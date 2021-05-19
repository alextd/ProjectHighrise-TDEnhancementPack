using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Sim;
using Game.UI.Session.HUD;
using Game.Util;
using Game.Services;
using UnityEngine;
using UnityEngine.UI;

namespace BetterPlacement
{

	public class TickerEntryBookable : TickerEntry
	{
		public TickerEntryBookable()
			: base(float.PositiveInfinity)
		{
		}

		public override GameObject CreateGameObject(GameObject container)
		{
			return UIUtil.MakeUIPrefab(UIPrefab.TickerEntryText, container);
		}

		public override void InitializeGameObject()
		{
			UIUtil.GetTextMesh(go, "Icon").text = Loc.Get("ui.build.performers-ui");
			UIUtil.GetTextMesh(go, "Icon").color = Color.green;
			UIUtil.GetTextMesh(go, "Text").text = "There are bookable performers";

			go.GetComponent<Button>().onClick.AddListener(() => Game.Game.serv.dialogs.AddPopup(new Game.UI.Session.Hotels.PerformerListPopup()));

			UIUtil.GetChild(go, "Close").SetActive(false);
			UIUtil.GetChild(go, "Ok Button").SetActive(false);
		}

		public override void ReleaseGameObject()
		{
			go.GetComponent<Button>().onClick.RemoveAllListeners();
		}

		private void OnClose()
		{
			PerformerBookableTicker.bookableTicker = null;
			Game.Game.ctx.hud.tickers.Remove(this);
		}
	}
	[HarmonyPatch(typeof(EventSpaceManager), nameof(EventSpaceManager.ProcessAttendees))]
	public static class PerformerBookableTicker
	{
		public static TickerEntryBookable bookableTicker = null;

		//private void ProcessAttendees()
		public static void Prefix(EventSpaceManager.EventSpaceManagerData ___data)
		{
			Update(___data);
		}

		public static void Update(EventSpaceManager.EventSpaceManagerData data)
		{ 
			bool bookable = data.performers.Where(r => r.CanBeBooked()).Count() > 0;

			if (bookable)
			{
				if (bookableTicker == null)
				{
					bookableTicker = new TickerEntryBookable();

					Game.Game.ctx.hud.tickers.Add(bookableTicker);
				}
			}
			else
			{ 
				if(bookableTicker != null)
				{
					Game.Game.ctx.hud.tickers.Remove(bookableTicker);
					bookableTicker = null;
				}
			}
		}
	}
}
