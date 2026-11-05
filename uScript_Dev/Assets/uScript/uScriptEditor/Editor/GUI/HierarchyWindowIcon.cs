// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HierarchyWindowIcon.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Defines the HierarchyWindowIcon type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Detox.Editor.GUI
{
   using UnityEditor;

   using UnityEngine;

   [InitializeOnLoad]
   internal static class HierarchyWindowIcon
   {
      private static readonly Texture2D ComponentIcon;

      static HierarchyWindowIcon()
      {
         ComponentIcon = uScriptGUI.GetTexture("iconScriptFile02");
#if UNITY_6000_4_OR_NEWER
         EditorApplication.hierarchyWindowItemByEntityIdOnGUI += HierarchyWindowItemOnGUI;
#else
         EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
#endif
      }

#if UNITY_6000_4_OR_NEWER
      private static void HierarchyWindowItemOnGUI(EntityId entityId, Rect selectionRect)
      {
         OnItemGUI(EditorUtility.EntityIdToObject(entityId) as GameObject, selectionRect);
      }
#else
      private static void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
      {
         OnItemGUI(EditorUtility.InstanceIDToObject(instanceID) as GameObject, selectionRect);
      }
#endif

      private static void OnItemGUI(GameObject go, Rect selectionRect)
      {
         if (EditorApplication.isPlayingOrWillChangePlaymode)
         {
            return;
         }

         if (Preferences.ShowHierarchyIcon && HasValidComponent(go))
         {
            DrawIcon(selectionRect);
         }
      }

      private static void DrawIcon(Rect rect)
      {
         if (ComponentIcon == null)
         {
            return;
         }

         rect = GetRightAligned(rect, ComponentIcon);
         GUI.Box(rect, uScriptGUIContent.Temp(ComponentIcon, "uScript attached"), GUIStyle.none);
      }

      private static Rect GetRightAligned(Rect rect, Texture texture)
      {
         rect.x = rect.xMax - texture.width;
         rect.width = texture.width;
         rect.y += (rect.height - texture.height) / 2f;
         rect.height = texture.height;

         return rect;
      }

      private static bool HasValidComponent(GameObject go)
      {
         return go != null
                && (go.GetComponent<uScriptEvent>() != null || go.GetComponent<uScriptCode>() != null
                    || go.GetComponent<uScript_MasterComponent>() != null);
      }
   }
}
