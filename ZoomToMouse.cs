using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using Game.Session.Input;
using Game.Session.Board;
using Game.Services;
using UnityEngine;

namespace BetterPlacement
{
	[HarmonyPatch(typeof(BaseInputMode), nameof(BaseInputMode.OnZoom))]
	public static class StackZoomWheel
	{
		//public virtual bool OnZoom(Vector2? pos, float zoomfactor, bool tween)
		public static bool Prefix(ref bool __result, BaseInputMode __instance, Vector2? pos, float zoomfactor, bool tween)
		{
			if (Game.Util.UIUtil.IsInputOverUI)
			{
				__result = false;
			}
			else if (pos.HasValue)
			{
				zoomfactor /= 3;//Honestly it's zooming too fast.

				//float zoom = Game.Game.serv.camera.GetZoom();
				//This is the current zoom, but where is it currently zooming to?

				float zoom = DesiredZoom.desiredZoom;

				float zoomFactor = 1f + Mathf.Abs(zoomfactor * __instance.ZoomMultiplier);
				zoom = zoomfactor >= 0f ? (zoom / zoomFactor) : (zoom * zoomFactor);
				__instance._isDown = false;
				Game.Game.serv.camera.SetZoom(zoom, tween, 0.15f);
				__result = true;
			}
			else
				return true;

			return false;
		}
	}

	[HarmonyPatch(typeof(CameraService), nameof(CameraService.SetZoom))]
	public static class DesiredZoom
	{
		public static float desiredZoom;
		//public void SetZoom(float zoom, bool tween, float tweenSeconds = 0.3f)
		public static void Prefix(CameraService __instance, float zoom)
		{
			desiredZoom = __instance.ClampZoom(zoom);//Whether it's instant or gradual, this is where we're headed.
		}
	}

	[HarmonyPatch(typeof(CameraService), nameof(CameraService.CameraZoomHelper))]
	public static class ZoomToMouse
	{
		//private void CameraZoomHelper(float val)
		public static bool Prefix(CameraService __instance, float val)
		{
			//Get where the mouse is
			Vector3 mousePosition = UnityEngine.Input.mousePosition;
			WorldPos oldpos = Game.Game.serv.camera.ScreenToWorldPos(mousePosition);
			
			//Old Zoom code
			val = __instance.ClampZoom(val);
			__instance.main.orthographicSize = val;
			Transform transform = __instance.main.transform;
			Vector2 transformPos = __instance.ClampPos(transform.position, val);
			float x = transformPos.x;
			float y = transformPos.y;
			Vector3 newTransform = new Vector3(transformPos.x, transformPos.y, transform.position.z);
			transform.position = newTransform;
			//----

			//Find out where we are now:
			WorldPos newpos = Game.Game.serv.camera.ScreenToWorldPos(mousePosition);

			//Find how much we slide away from cursor and adjust:
			newTransform.x += oldpos.x - newpos.x; newTransform.y += oldpos.y - newpos.y;
			transform.position = __instance.ClampPos(newTransform, val);

			Game.Game.serv.camera.OnCameraZoomChanged.Invoke();

			return false;
		}
	}
}
