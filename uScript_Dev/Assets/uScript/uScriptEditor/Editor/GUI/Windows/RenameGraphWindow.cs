// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RenameGraphWindow.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Small dialog that asks for a new graph name.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Detox.Editor.GUI.Windows
{
   using System.IO;

   using UnityEditor;

   using UnityEngine;

   public sealed class RenameGraphWindow : EditorWindow
   {
      private const string NameControl = "uScriptRenameGraphName";

      private string graphPath;
      private string newName;
      private bool focusName = true;

      public static void Open(string graphPath)
      {
         if (string.IsNullOrEmpty(graphPath))
         {
            return;
         }

         var window = CreateInstance<RenameGraphWindow>();
         window.graphPath = graphPath;
         window.newName = Path.GetFileNameWithoutExtension(graphPath);
         window.titleContent = new GUIContent("Rename Graph");

         var size = new Vector2(380, 118);
         var center = uScript.Instance != null ? uScript.Instance.position.center : new Vector2(Screen.currentResolution.width, Screen.currentResolution.height) * 0.5f;
         window.position = new Rect(center - (size * 0.5f), size);
         window.minSize = size;
         window.maxSize = size;
         window.ShowUtility();
         window.Focus();
      }

      private void OnGUI()
      {
         var e = Event.current;
         if (e.type == EventType.KeyDown)
         {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
               e.Use();
               this.Apply();
               return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
               e.Use();
               this.Close();
               return;
            }
         }

         GUILayout.Space(8);
         EditorGUILayout.LabelField("Rename \"" + Path.GetFileNameWithoutExtension(this.graphPath) + "\" to:", EditorStyles.boldLabel);

         GUI.SetNextControlName(NameControl);
         this.newName = EditorGUILayout.TextField(this.newName);

         if (this.focusName)
         {
            EditorGUI.FocusTextInControl(NameControl);
            this.focusName = false;
         }

         EditorGUILayout.LabelField("Generated scripts are renamed too. Scene components stay connected.", EditorStyles.miniLabel);

         GUILayout.FlexibleSpace();
         EditorGUILayout.BeginHorizontal();
         {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
               this.Close();
            }

            if (GUILayout.Button("Rename", GUILayout.Width(80)))
            {
               this.Apply();
            }
         }

         EditorGUILayout.EndHorizontal();
         GUILayout.Space(8);
      }

      private void Apply()
      {
         var path = this.graphPath;
         var name = this.newName;

         // Rename after this window has closed and the current GUI pass has ended: the rename
         // shows its own dialogs and refreshes the asset database.
         EditorApplication.delayCall += () =>
         {
            if (uScript.Instance != null)
            {
               uScript.Instance.RenameGraph(path, name);
            }
         };

         this.Close();
         GUIUtility.ExitGUI();
      }
   }
}
