// View > Go to Node #: jumps to a node by its number.
// The number is the one uScript uses in the graph's generated C# code (Relay_In_423 = node 423)
// and the one the AI tools report.

using System;

using Detox.ScriptEditor;

using UnityEditor;

using UnityEngine;

public class uScriptGoToNodeWindow : EditorWindow
{
   private string number = string.Empty;
   private string message = string.Empty;
   private bool focusField = true;

   public static void Open()
   {
      var window = CreateInstance<uScriptGoToNodeWindow>();
      window.titleContent = new GUIContent("Go to Node #");
      window.minSize = new Vector2(280, 92);
      window.maxSize = new Vector2(280, 92);
      window.ShowUtility();
   }

   private void OnGUI()
   {
      Event e = Event.current;
      bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
      if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
      {
         Close();
         return;
      }

      GUILayout.Space(8);
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("Node #", GUILayout.Width(50));
      GUI.SetNextControlName("NodeNumber");
      number = EditorGUILayout.TextField(number);
      if (GUILayout.Button("Go", GUILayout.Width(40)) || enter)
      {
         GoTo();
         if (enter) e.Use();
      }

      EditorGUILayout.EndHorizontal();

      if (focusField)
      {
         EditorGUI.FocusTextInControl("NodeNumber");
         focusField = false;
      }

      GUILayout.Space(4);
      EditorGUILayout.LabelField(message.Length > 0 ? message : "From a Console error (Relay_In_423) or the AI tools.", EditorStyles.wordWrappedMiniLabel);
   }

   private void GoTo()
   {
      uScript editor = uScript.WeakInstance;
      if (editor == null || editor.ScriptEditorCtrl == null || editor.ScriptEditorCtrl.ScriptEditor == null)
      {
         message = "Open a graph in uScript first.";
         return;
      }

      int id;
      if (!int.TryParse(number.Trim().TrimStart('#'), out id))
      {
         message = "Type a node number, for example 423.";
         return;
      }

      EntityNode[] entities = editor.ScriptEditorCtrl.ScriptEditor.DirectEntityNodes;
      if (id < 0 || id >= entities.Length)
      {
         message = "This graph has no node " + id + " (0 to " + (entities.Length - 1) + ").";
         return;
      }

      if (entities[id] is LinkNode)
      {
         message = "Number " + id + " is a link, not a node.";
         return;
      }

      Guid guid = entities[id].Guid;
      Detox.FlowChart.Node node = editor.ScriptEditorCtrl.GetNode(guid);
      if (node == null)
      {
         message = "Node " + id + " is not on the canvas.";
         return;
      }

      editor.ScriptEditorCtrl.SelectNode(guid);
      editor.ScriptEditorCtrl.CenterOnNode(node);
      uScript.RequestRepaint();
      editor.Focus();
      Close();
   }
}
