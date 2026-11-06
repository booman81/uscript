#if USCRIPT_INPUTSYSTEM
// uScript Input System helpers (only compiled when the Input System package is installed)

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class uScriptInputSystemUtility
{
   private static readonly List<InputAction> EnabledActions = new List<InputAction>();

   /// <summary>All currently enabled actions (project-wide actions, PlayerInput, or actions you enabled yourself).</summary>
   public static List<InputAction> GetEnabledActions()
   {
      EnabledActions.Clear();
      InputSystem.ListEnabledActions(EnabledActions);
      return EnabledActions;
   }

   /// <summary>
   /// Finds an enabled action by name. Use "Jump" or "Player/Jump" (action map / action) when
   /// several maps have an action with the same name. Not case sensitive.
   /// </summary>
   public static InputAction FindAction(string actionName)
   {
      if (string.IsNullOrEmpty(actionName))
      {
         return null;
      }

      InputAction nameMatch = null;

      foreach (InputAction action in GetEnabledActions())
      {
         InputActionMap map = action.actionMap;
         if (map != null && string.Equals(map.name + "/" + action.name, actionName, StringComparison.OrdinalIgnoreCase))
         {
            return action;
         }

         if (nameMatch == null && string.Equals(action.name, actionName, StringComparison.OrdinalIgnoreCase))
         {
            nameMatch = action;
         }
      }

      return nameMatch;
   }

   /// <summary>Reads the action's current value as a float (button / axis) and a Vector2 (stick / 2D vector).</summary>
   public static void ReadValue(InputAction action, out float value, out Vector2 vector2Value)
   {
      value = 0f;
      vector2Value = Vector2.zero;

      if (action == null)
      {
         return;
      }

      object raw = action.ReadValueAsObject();

      if (raw is float)
      {
         value = (float)raw;
         vector2Value = new Vector2(value, 0f);
      }
      else if (raw is Vector2)
      {
         vector2Value = (Vector2)raw;
         value = vector2Value.magnitude;
      }
      else if (raw is Vector3)
      {
         Vector3 vector3 = (Vector3)raw;
         vector2Value = new Vector2(vector3.x, vector3.y);
         value = vector3.magnitude;
      }
      else if (raw is double)
      {
         value = (float)(double)raw;
      }
      else if (raw is int)
      {
         value = (int)raw;
      }
      else if (raw is bool)
      {
         value = (bool)raw ? 1f : 0f;
      }
   }
}
#endif
