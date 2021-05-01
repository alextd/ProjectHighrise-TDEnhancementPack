using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

using Game.Session.Input;
using Game.Session.Entities.Config;

//Everything is drag-paintable. Also remove the error sound when dragging.

namespace BetterPlacement
{
	[HarmonyPatch(typeof(AbstractPaintInputMode), "DoDragMove")]
	class EverythingIsPaintable
	{
		//protected override void DoDragMove(Vector2 scrpos, Vector2 lastpos)
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			FieldInfo paintInfo = AccessTools.Field(typeof(PlacementConfig), nameof(PlacementConfig.paint));

			foreach (var inst in instructions)
			{
				yield return inst;
				if (inst.LoadsField(paintInfo))
				{
					//if(_cursor.config.placement.paint)
					//Pop that value and just return true. We can always drag-place things.
					yield return new CodeInstruction(OpCodes.Pop);
					yield return new CodeInstruction(OpCodes.Ldc_I4_1);//true
				}
			}
		}
	}

	[HarmonyPatch(typeof(BuyEntityInputMode), "OnFailedPaint")]
	class NoHonking
	{
		//protected override void OnFailedPaint(bool dragging)
		public static MethodInfo PlayPaintSoundInfo = AccessTools.Method(typeof(AbstractPaintInputMode), "PlayPaintSound");
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo PlayPaintSoundUnlessDraggingInfo = AccessTools.Method(typeof(NoHonking), nameof(PlayPaintSoundUnlessDragging));

			foreach (var inst in instructions)
			{
				if (inst.Calls(PlayPaintSoundInfo))
				{
					//this.PlayPaintSound(success: false);
					//Do not play paint sound if failure while dragging
					yield return new CodeInstruction(OpCodes.Ldarg_1);//dragging
					yield return new CodeInstruction(OpCodes.Call, PlayPaintSoundUnlessDraggingInfo);//this.PlayPaintSoundUnlessDragging(success,dragging)
				}
				else
					yield return inst;
			}
		}

		//class AbstractPaintInputMode
		//protected void PlayPaintSound(bool success)
		public static void PlayPaintSoundUnlessDragging(AbstractPaintInputMode instance, bool success, bool dragging)
		{
			if (!dragging || success)
				PlayPaintSoundInfo.Invoke(instance, new object[] { success });
		}
	}

	[HarmonyPatch(typeof(AbstractPaintInputMode), nameof(AbstractPaintInputMode.DoMouseUp))]
	public static class MouseUpDragging
	{
		//protected override void DoMouseUp(Vector2 scrpos, bool wasDragging)
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo TryPaintInfo = AccessTools.Method(typeof(AbstractPaintInputMode), nameof(AbstractPaintInputMode.TryPaint));

			foreach (var inst in instructions)
			{
				if (inst.Calls(TryPaintInfo))
				{
					//TryPaint(false) => TryPaint(wasDragging)
					yield return new CodeInstruction(OpCodes.Pop);
					yield return new CodeInstruction(OpCodes.Ldarg_2);//wasDragging
				}
				yield return inst;
			}
		}
	}
}
