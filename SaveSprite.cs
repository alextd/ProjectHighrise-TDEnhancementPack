using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Entities.Components;
using Game.Session.Entities;
using Game.Session.Entities.Config;
using Game.Session.Entities.Data;
using Game.Services;
using Game.Components;
using UnityEngine;

namespace BetterPlacement
{
	//Okay. So. Spawning in someone doesn't enable the renderer.
	//Loading someone doesn't enable the renderer.
	//The Unity Component Renderer is ONLY set enabled after you call SetVisibility.
	//Even though the values are all ready, even if you HIDE something, it will only then rejigger itself and decide "oh yea, I should have been rendering, my bad"

	//Spawned people only get a call to SetVisibilty because they have a PlayAnimation(default), which calls UpdateCartAnimation, which sets the FOLLOWER to hidden
	//- but as a side effect, it recomputes minalpha and enables the renderer
	// Walking called PlayAnimation so moving people would reenable rendering
	// Most people's schedules started with an action that set visibily to true, so loading usually set them visible.
	// Sometimes they didn't though! And who would notice a dude popping into existence among 100 other people in a big tower.
	// Seem that people WITHOUT carts would not be shown on load until they started a new action that set their visibility.

	// But now what I've fixed script loading, they continue on their scripts, and are NOT VISIBLE after load - until their pause finishes.

	[HarmonyPatch(typeof(EntityManager), nameof(EntityManager.SerializeEntity))]
	public static class SaveEntitySprite
	{
		//private Hashtable SerializeEntity(Entity e)
		public static void Postfix(Hashtable __result, Entity e)
		{
			if (e.components.peep != null && //First, only care about people. Oh geez I hope any room animations are already handled fine.
				e.components.sprite is SpriteComponent sprite)
			{
				Hashtable spriteHash = new Hashtable();

				//Using PeepComponent.PlayAnimation to see what is set.
				spriteHash.SerAdd("_animname", sprite._animname);
				if(e.components.peep.isUsingCart)
					spriteHash.SerAdd("usingcart", true);
				//turns out not much else is needed to be saved.
				//I don't think I'll dig into setting exact anim frames.

				if (!sprite.IsVisible)
				{
					Log.Debug($"---saving dose alphas ({string.Join(", ", sprite.vis.alphas.Select(f => $"{f}").ToArray())})");
					spriteHash.SerAdd("vis_alphas", Game.Game.serv.serializer.Serialize(sprite.vis.alphas));
				}

				__result["sprite"] = spriteHash;
			}
		}
	}

	[HarmonyPatch(typeof(EntityManager), nameof(EntityManager.LoadAndCreateEntity))]
	public static class LoadEntitySprite
	{
		//public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		//Transpiler would be needed here but SaveScript is already hijacking the call, which will roundaboutly call this:

		//public Entity CreateByTemplate(EntityTemplate configs, int id = 0, EntityData savedata = null)
		public static Entity AfterCreateByTemplate(Entity e, Hashtable entityHash)
		{
			if (!entityHash.ContainsKey("sprite")) return e;

			if (e.components.sprite is SpriteComponent sprite)
			{
				if (entityHash["sprite"] is Hashtable spriteHash)
				{
					SaveLoadUtils.DeserializeSingleKey<string>(spriteHash, "_animname", x => sprite.SetAnimation(x));
					if (e.components.peep is PeepComponent peep)
					{
						SaveLoadUtils.DeserializeSingleKey(spriteHash, "usingcart",
							delegate (bool x)
							{
								peep.SetUsingCart(x);
								//I'm pretty sure SetUsingCart tries to call UpdateCartAnimation when it changes, but the bool is the opposite of what it should be.
								peep.UpdateCartAnimation(); //Probably would be redundant to check true since we're already here
							});
					}
					if (spriteHash.ContainsKey("vis_alphas"))
					{
						List<float> visAlphas = Game.Game.serv.serializer.Deserialize(spriteHash["vis_alphas"], typeof(List<float>)) as List<float>;
						Log.Debug($"---Got dem alphas ({string.Join(", ", visAlphas.Select(f => $"{f}").ToArray())})");
						visAlphas.CopyTo(sprite.vis.alphas);
					}

					sprite.vis.RecomputeAlphaAndUpdateVisibility(e);
				}
			}

			return e;
		}
	}

	//todo offite peopel aren't selectable
}
