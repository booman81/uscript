// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptNestedGraphCommands.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Make Nested Graph: moves the selected nodes into a new graph with External Connections, then (after Unity has
//   compiled the new graph) puts the new nested node where the selection was and reconnects it.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Detox.Drawing;
using Detox.Editor;
using Detox.ScriptEditor;

using UnityEditor;

using UnityEngine;

public sealed partial class uScript
{
   private const string PendingNestedKey = "uScript.PendingNestedGraph";

   // Gives up waiting for the nested graph's code after this long (compile errors, for example)
   private const double PendingNestedTimeout = 600;

   [Serializable]
   private class PendingNestedGraph
   {
      public string ParentPath;
      public string NestedName;
      public int X;
      public int Y;
      public double CreatedAt;
      public List<NestedGraphConnection> Connections = new List<NestedGraphConnection>();
   }

   public bool CanMakeNestedGraph
   {
      get { return m_ScriptEditorCtrl != null && m_ScriptEditorCtrl.FlowChart.SelectedNodes.Length > 0; }
   }

   public void CommandMakeNestedGraph()
   {
      if (!this.CanMakeNestedGraph)
      {
         EditorUtility.DisplayDialog("Make Nested Graph", "Select the nodes to move into a new nested graph first.", "OK");
         return;
      }

      if (!string.IsNullOrEmpty(SessionState.GetString(PendingNestedKey, string.Empty)))
      {
         EditorUtility.DisplayDialog("Make Nested Graph", "uScript is still waiting for the previous nested graph to compile. Try again when Unity has finished compiling.", "OK");
         return;
      }

      var nested = new ScriptEditor(string.Empty, PopulateEntityTypes(null), PopulateLogicTypes());
      var plan = m_ScriptEditorCtrl.PlanNestedGraph(nested);

      if (plan.Problems.Count > 0)
      {
         EditorUtility.DisplayDialog(
            "Make Nested Graph",
            "The selection can't be turned into a nested graph:\n\n" + string.Join("\n", plan.Problems.Distinct().Take(10).ToArray()),
            "OK");
         return;
      }

      var info = string.Format(
         "{0} node(s) will move into the new graph, with {1} External Connection(s).",
         plan.MovedNodes.Count,
         plan.Graph.Externals.Length);
      if (plan.KeptInParent.Count > 0)
      {
         info += "\n\nThese stay in this graph because nodes outside the selection use them too:\n"
            + string.Join("\n", plan.KeptInParent.Distinct().Take(10).ToArray());
      }

      if (!EditorUtility.DisplayDialog("Make Nested Graph", info, "Continue", "Cancel"))
      {
         return;
      }

      var folder = uScriptConfig.ConstantPaths.RelativePath(
         !string.IsNullOrEmpty(this.fullPath) ? Path.GetDirectoryName(this.fullPath) : Preferences.UserScripts);

      var assetPath = EditorUtility.SaveFilePanelInProject(
         "Make Nested Graph",
         "NewNestedGraph",
         "uscript",
         "Name of the new nested graph",
         folder);

      if (string.IsNullOrEmpty(assetPath))
      {
         return;
      }

      var name = Path.GetFileNameWithoutExtension(assetPath);
      var nestedPath = Path.GetFullPath(assetPath).Replace('\\', '/');

      if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
      {
         EditorUtility.DisplayDialog("Make Nested Graph", "Graph names can only use letters, numbers and _, and can't start with a number.", "OK");
         return;
      }

      if (File.Exists(nestedPath) || uScriptBackgroundProcess.GraphInfoList.ContainsKey(name) || this.GetType(name) != null)
      {
         EditorUtility.DisplayDialog("Make Nested Graph", string.Format("A graph or class called \"{0}\" already exists. Choose a different name.", name), "OK");
         return;
      }

      if (SaveDenied())
      {
         return;
      }

      // 1. Save the new graph and generate its code
      bool saved;
      AssetDatabase.StartAssetEditing();
      try
      {
         saved = this.SaveGraph(plan.Graph, nestedPath, true, this.GenerateDebugInfo, false);
      }
      finally
      {
         AssetDatabase.StopAssetEditing();
      }

      if (!saved)
      {
         EditorUtility.DisplayDialog("Make Nested Graph", "Could not save " + assetPath + ".", "OK");
         return;
      }

      // 2. Remember how to connect the nested node; it can only be added once Unity has compiled the new graph
      var pending = new PendingNestedGraph
      {
         ParentPath = this.fullPath ?? string.Empty,
         NestedName = name,
         X = plan.Position.X,
         Y = plan.Position.Y,
         CreatedAt = EditorApplication.timeSinceStartup,
         Connections = plan.Connections
      };
      SessionState.SetString(PendingNestedKey, JsonUtility.ToJson(pending));

      // 3. Remove the moved nodes from this graph (one undo step)
      m_ScriptEditorCtrl.DeleteNodes(plan.MovedNodes);

      uScriptDebug.Log(
         string.Format("Created nested graph \"{0}\". The \"{0}\" node is added to this graph when Unity has compiled it; save this graph afterwards.", name),
         uScriptDebug.Type.Message);

      this.RefreshAssetDatabase(false, this.GenerateDebugInfo);
      uScriptBackgroundProcess.ForceFileRefresh();
   }

   /// <summary>Called from Update: adds the nested node once its code has been compiled.</summary>
   private void ProcessPendingNestedGraph()
   {
      var json = SessionState.GetString(PendingNestedKey, string.Empty);
      if (string.IsNullOrEmpty(json) || m_ScriptEditorCtrl == null || EditorApplication.isCompiling || EditorApplication.isUpdating)
      {
         return;
      }

      PendingNestedGraph pending;
      try
      {
         pending = JsonUtility.FromJson<PendingNestedGraph>(json);
      }
      catch (Exception)
      {
         SessionState.EraseString(PendingNestedKey);
         return;
      }

      if (pending == null || string.IsNullOrEmpty(pending.NestedName))
      {
         SessionState.EraseString(PendingNestedKey);
         return;
      }

      var timedOut = EditorApplication.timeSinceStartup < pending.CreatedAt   // Unity restarted
                     || EditorApplication.timeSinceStartup - pending.CreatedAt > PendingNestedTimeout;

      var isParentOpen = string.IsNullOrEmpty(pending.ParentPath)
         ? string.IsNullOrEmpty(this.fullPath)
         : SameGraphPath(pending.ParentPath, this.fullPath);

      if (!isParentOpen || timedOut)
      {
         if (timedOut)
         {
            SessionState.EraseString(PendingNestedKey);
            uScriptDebug.Log(
               string.Format("The nested graph \"{0}\" was created, but its node could not be added automatically. Add it from the Toolbox and connect it.", pending.NestedName),
               uScriptDebug.Type.Warning);
         }

         return;
      }

      var toolbox = uScriptGUIPanelToolbox.Instance;
      if (toolbox == null)
      {
         return;
      }

      LogicNode? prototype = null;
      foreach (var item in toolbox.GetAllNodeMenuItems())
      {
         if (item.Tag is LogicNode)
         {
            var logic = (LogicNode)item.Tag;
            if (logic.Type == pending.NestedName || (logic.Type != null && logic.Type.EndsWith("." + pending.NestedName, StringComparison.Ordinal)))
            {
               prototype = logic;
               break;
            }
         }
      }

      if (prototype == null)
      {
         return;   // not compiled yet
      }

      SessionState.EraseString(PendingNestedKey);

      var node = prototype.Value.Copy(false);
      var failed = m_ScriptEditorCtrl.AddNestedNode(node, new Point(pending.X, pending.Y), pending.Connections);

      if (failed.Count > 0)
      {
         uScriptDebug.Log(
            "Make Nested Graph: these connections could not be restored, connect them by hand: " + string.Join(", ", failed.Distinct().ToArray()),
            uScriptDebug.Type.Warning);
      }

      this.Repaint();
   }
}
