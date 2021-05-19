using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//LET IT BE KNOWN, 4 years ago: https://steamcommunity.com/app/423580/discussions/0/133262487499689624/#c133262487502148872
//Devs: "Since each event space can only be used once every few days (because of the cleanup time etc),
//I think it won't be a chore to book them and pay attention to them
//- once we add the summary ticker, that is, which isn't therr right now! :)
// 1. Yes, it is a chore, easily fixed too.
// 2. They never did add that ticker.
using Game.Session.Sim;
using Game.Session.Entities;
using Assets.Code.Music;

namespace BetterPlacement
{
	public static class BookAllPerformers
	{
		public static void DoBookAll()
		{
			while(Game.Game.ctx.sim.eventspaces.data.performers.FirstOrDefault(r => r.CanBeBooked()) is PerformerRecord rec)
			{
				Log.Debug($"Bookable : {rec.name}");
				BookIt(rec);
			}
		}

		public static void BookIt(PerformerRecord rec)
		{
			foreach (string eventspaceTmpl in rec.def.eventspaces)
			{
				if (Game.Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(eventspaceTmpl) is HashSet<Entity> spaces)
				{
					foreach (Entity room in spaces)
					{
						Game.Game.ctx.sim.eventspaces.GetRoomAssignmentState(room, out bool isAssigned, out bool isReadyToAssign);
						if (isReadyToAssign)
						{
							Game.Game.ctx.sim.eventspaces.BookPerformerAndAssignToRoom(rec, room);
							//Game.Game.serv.audio.PlayUISFX((!rec.def.IsEntertainer()) ? UIEffectType.BookBusinessEvent : UIEffectType.BookMusicEvent);
							Log.Debug($"Assigned {rec.name} to {room}:{room.id}");

							return;
						}
					}
				}
			}
		}
	}

	//shift-click the ticker

	//shift-click on performer list, book all of current type

	//auto select a room

	//add 'auto-book' checkbox on I guess the event setup service?
}
