using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Clicks;
using Kingmaker.GameModes;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._VM.ServiceWindows;
using Kingmaker.UI.PhotoMode;
using Kingmaker.UI.Selection;
using Kingmaker.View;
using TurnBased.Controllers;
using UnityEngine;

namespace WotRRMBCameraRotate;
[HarmonyPatch]
internal class Patches
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(CameraRig), nameof(CameraRig.RotateByMiddleButton))]
    internal static bool RotateByMiddleButton(CameraRig __instance)
    {
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            return false;
        }
        if (Input.GetMouseButtonDown(1) && !__instance.m_RotationByMouse && !__instance.m_RotationByKeyboard)
        {
            __instance.m_RotationByMouse = true;
            Game.Instance.CursorController.SetMoveCameraCursor(true);
            __instance.m_BaseMousePoint = new Vector3?(__instance.GetLocalPointerPosition());
            __instance.m_RotateDistance = 0f;
            __instance.m_TargetRotate = __instance.transform.eulerAngles;
        }
        if (Input.GetMouseButtonUp(1) && __instance.m_RotationByMouse)
        {
            Game.Instance.CursorController.SetMoveCameraCursor(false);
            __instance.m_BaseMousePoint = null;
            __instance.m_RotationByMouse = false;
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(CameraRig), nameof(CameraRig.FreeRotateByMiddleButton))]
    internal static bool FreeRotateByMiddleButton(CameraRig __instance, ref Vector2 __result)
    {
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            __result = Vector2.zero;
            return false;
        }
        if (Input.GetMouseButtonDown(1) && !__instance.m_RotationByMouse)
        {
            __instance.m_RotationByMouse = true;
            Game.Instance.CursorController.SetMoveCameraCursor(true);
            __instance.m_BaseMousePoint = new Vector3?(__instance.GetLocalPointerPosition());
            __instance.m_RotateDistance2D = Vector2.zero;
            __instance.m_TargetRotate = __instance.transform.eulerAngles;
        }
        if (Input.GetMouseButtonUp(1) && __instance.m_RotationByMouse)
        {
            Game.Instance.CursorController.SetMoveCameraCursor(false);
            __instance.m_BaseMousePoint = null;
            __instance.m_RotationByMouse = false;
        }
        if (__instance.m_BaseMousePoint != null)
        {
            Vector2 vector = (Vector2)__instance.m_BaseMousePoint.Value - __instance.GetLocalPointerPosition();
            Vector2 result = (__instance.m_RotateDistance2D - vector) * (BlueprintRoot.Instance ? PhotoModeRoot.PhotoModeSettings.CameraRotationPhotoSpeedMouse.GetValue() : 2f);
            __instance.m_RotateDistance2D = vector;
            __result = result;
            return false;
        }
        __result = Vector2.zero;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(PointerController), nameof(PointerController.Tick))]
    public static bool TickReplace(PointerController __instance)
    {
        if (PointerController.DebugThisFrame)
        {
            PointerController.DebugThisFrame = false;
        }
        bool isControllerGamepad = Game.Instance.IsControllerGamepad;
        Vector3 zero = Vector3.zero;
        GameObject gameObject = null;
        IClickEventHandler clickEventHandler = null;
        if (!PointerController.InGui && Game.GetCamera())
        {
            __instance.SelectClickObject(PointerController.PointerPosition, out gameObject, out zero, out clickEventHandler);
            __instance.m_SimulateClickHandler = clickEventHandler;
            __instance.m_WorldPositionForSimulation = __instance.WorldPosition;
        }
        if (gameObject != null)
        {
            __instance.WorldPosition = zero;
        }
        bool flag;
        if (isControllerGamepad)
        {
            flag = __instance.GamePadConfirm;
        }
        else
        {
            flag = Input.GetMouseButton(__instance.m_MouseDownButton);
        }
        if (!flag && __instance.m_MouseDown)
        {
            __instance.m_MouseDown = false;
            if (__instance.m_MouseDrag && __instance.m_DragFrames < 2)
            {
                __instance.m_MouseDrag = false;
                if (Game.Instance.UI.MultiSelection)
                {
                    Game.Instance.UI.MultiSelection.Cancel();
                }
            }
            if (__instance.m_MouseDownButton == 1 && __instance.Mode != PointerMode.Default)
            {
                __instance.ClearPointerMode();
            }
            else if (__instance.m_MouseDrag && __instance.Mode == PointerMode.Default)
            {
                if (__instance.m_MouseDownButton == 0)
                {
                    if (Game.Instance.UI.MultiSelection)
                    {
                        Game.Instance.UI.MultiSelection.SelectEntities();
                    }
                }
                //else
                //{
                //    IDragClickEventHandler dragClickEventHandler = __instance.m_MouseDownHandler as IDragClickEventHandler;
                //    if (dragClickEventHandler != null && __instance.m_MouseDownOn != null && dragClickEventHandler.OnClick(__instance.m_MouseDownOn, __instance.m_MouseDownWorldPosition, zero))
                //    {
                //        EventBus.RaiseEvent<IClickMarkHandler>(delegate (IClickMarkHandler h)
                //        {
                //            h.OnClickHandled(__instance.m_MouseDownWorldPosition);
                //        }, true);
                //    }
                //}
            }
            else if (__instance.m_MouseDownHandler != null && __instance.m_MouseDownOn != null && (!isControllerGamepad || __instance.m_MouseDownButton == 0) && (Game.Instance.CurrentMode == GameModeType.TacticalCombat || RootUIContext.Instance.CurrentServiceWindow != ServiceWindowsType.LocalMap))
            {
                bool flag2 = false;
                if (CombatController.IsInTurnBasedCombat())
                {
                    TurnController currentTurn = Game.Instance.TurnBasedCombatController.CurrentTurn;
                    flag2 = (currentTurn == null || currentTurn.IgnoreClick());
                }
                if (!flag2 && __instance.m_MouseDownHandler.OnClick(__instance.m_MouseDownOn, __instance.m_MouseDownWorldPosition, __instance.m_MouseDownButton, false, false, false))
                {
                    EventBus.RaiseEvent<IClickMarkHandler>(delegate (IClickMarkHandler h)
                    {
                        h.OnClickHandled(__instance.m_MouseDownWorldPosition);
                    }, true);
                }
            }
            __instance.m_MouseDownOn = null;
            __instance.m_MouseDrag = false;
        }
        if (__instance.PointerOn != gameObject)
        {
            __instance.OnHoverChanged(__instance.PointerOn, gameObject);
            __instance.PointerOn = gameObject;
        }
        if (!isControllerGamepad && __instance.m_MouseDown && Vector2.Distance(__instance.m_MouseDownCoord, PointerController.PointerPosition) > 4f && !__instance.m_MouseDrag && !CombatController.IsInTurnBasedCombat() && __instance.Mode == PointerMode.Default)
        {
            __instance.m_MouseDrag = true;
            __instance.m_DragFrames = 0;
            if (__instance.m_MouseDownButton == 0)
            {
                if (Game.Instance.UI.MultiSelection)
                {
                    Game.Instance.UI.MultiSelection.CreateBoxSelection(__instance.m_MouseDownCoord);
                }
            }
            //else
            //{
            //    IDragClickEventHandler dragClickEventHandler2 = __instance.m_MouseDownHandler as IDragClickEventHandler;
            //    if (dragClickEventHandler2 != null)
            //    {
            //        dragClickEventHandler2.OnStartDrag(__instance.m_MouseDownOn, __instance.m_MouseDownWorldPosition);
            //    }
            //}
        }
        if (__instance.m_MouseDrag && Time.unscaledTime - __instance.m_MouseButtonTime >= 0.07f)
        {
            if (__instance.m_MouseDownButton == 0 && Game.Instance.UI.MultiSelection)
            {
                Game.Instance.UI.MultiSelection.DragBoxSelection();
            }
            __instance.m_DragFrames++;
        }
        if (isControllerGamepad)
        {
            if (!__instance.m_MouseDown && flag && !PointerController.InGui)
            {
                __instance.m_MouseDownButton = (flag ? 0 : 1);
                __instance.m_MouseDown = true;
                __instance.m_MouseDownOn = gameObject;
                __instance.m_MouseDownHandler = clickEventHandler;
                __instance.m_MouseDownCoord = PointerController.PointerPosition;
                __instance.m_MouseDownWorldPosition = zero;
                __instance.m_MouseButtonTime = Time.unscaledTime;
            }
        }
        else if (!__instance.m_MouseDown && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && !PointerController.InGui)
        {
            __instance.m_MouseDownButton = (Input.GetMouseButtonDown(0) ? 0 : 1);
            __instance.m_MouseDown = true;
            __instance.m_MouseDownOn = gameObject;
            __instance.m_MouseDownHandler = clickEventHandler;
            __instance.m_MouseDownCoord = Input.mousePosition;
            __instance.m_MouseDownWorldPosition = zero;
            __instance.m_MouseButtonTime = Time.unscaledTime;
        }
        //if (!isControllerGamepad && __instance.m_MouseDown && __instance.m_MouseDownButton == 1 && !CombatController.IsInTurnBasedCombat())
        //{
        //    IDragClickEventHandler dragClickEventHandler3 = __instance.m_MouseDownHandler as IDragClickEventHandler;
        //    if (dragClickEventHandler3 != null && __instance.m_MouseDownOn != null)
        //    {
        //        dragClickEventHandler3.OnDrag(__instance.m_MouseDownOn, __instance.m_MouseDownWorldPosition, zero);
        //    }
        //}
        if (!__instance.m_MouseDown)
        {
            MultiplySelection multiSelection = Game.Instance.UI.MultiSelection;
            if (multiSelection == null)
            {
                return false;
            }
            multiSelection.Cancel();
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(TurnController), nameof(TurnController.TryChangeMovementLimit))]
    internal static bool TryChangeMovementLimit()
    {
        if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            return false;
        }
        return true;
    }
}
