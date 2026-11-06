// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptGraphFileCommands.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Rename and delete uScript graphs, together with their generated code, from inside the uScript editor.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Detox.ScriptEditor;

using UnityEditor;

using UnityEngine;

public sealed partial class uScript
{
   private const int MaxListedUsages = 10;

   private Guid pendingFocusNode = Guid.Empty;
   private int pendingFocusUpdates;

   /// <summary>
   /// Opens uScript and the given graph (asking to save the current one if needed), then selects and centers the node.
   /// Used by the Console "Show node in uScript" links.
   /// </summary>
   public static void ShowGraphNode(string graphName, Guid nodeGuid)
   {
      var graphPath = GetGraphPath(graphName + ".uscript");
      if (string.IsNullOrEmpty(graphPath))
      {
         EditorUtility.DisplayDialog("uScript", string.Format("The graph \"{0}\" could not be found in this project.", graphName), "OK");
         return;
      }

      Detox.Editor.EditorCommands.OpenEditorWindow();
      var instance = Instance;
      if (instance == null)
      {
         return;
      }

      if (!instance.IsOpenGraph(graphPath) && !instance.OpenGraph(graphPath, false))
      {
         return;
      }

      instance.Focus();

      if (nodeGuid != Guid.Empty)
      {
         // Applied over the next few updates, after the graph has restored its saved canvas position
         instance.pendingFocusNode = nodeGuid;
         instance.pendingFocusUpdates = 3;
      }

      RequestRepaint(3);
   }

   /// <summary>Called from Update(): selects and centers a node requested by ShowGraphNode().</summary>
   private void ProcessPendingNodeFocus()
   {
      if (this.pendingFocusUpdates <= 0 || this.pendingFocusNode == Guid.Empty || this.m_ScriptEditorCtrl == null)
      {
         return;
      }

      this.pendingFocusUpdates--;

      var node = this.m_ScriptEditorCtrl.GetNode(this.pendingFocusNode);
      if (node == null)
      {
         if (this.pendingFocusUpdates == 0)
         {
            uScriptDebug.Log("The node could not be found in the graph. It may have been deleted since the error happened.", uScriptDebug.Type.Warning);
         }

         return;
      }

      this.m_ScriptEditorCtrl.SelectNodes(new[] { this.pendingFocusNode }, false);
      this.m_ScriptEditorCtrl.CenterOnNode(node);
      this.Repaint();

      if (this.pendingFocusUpdates == 0)
      {
         this.pendingFocusNode = Guid.Empty;
      }
   }

   /// <summary>
   /// Renames a graph and its generated scripts. The generated scripts are moved (not recreated), so they keep
   /// their GUIDs and every scene or prefab that uses the graph's component stays connected.
   /// </summary>
   /// <param name="graphPath">Absolute path of the .uscript file.</param>
   /// <param name="requestedName">The new graph name (without extension).</param>
   /// <returns>True if the graph was renamed.</returns>
   public bool RenameGraph(string graphPath, string requestedName)
   {
      graphPath = NormalizeSlashes(graphPath);
      if (!File.Exists(graphPath))
      {
         EditorUtility.DisplayDialog("Rename Graph", "The graph file could not be found:\n" + graphPath, "OK");
         return false;
      }

      var oldName = Path.GetFileNameWithoutExtension(graphPath);
      var newName = (requestedName ?? string.Empty).Trim();

      if (newName.EndsWith(".uscript", StringComparison.OrdinalIgnoreCase))
      {
         newName = newName.Substring(0, newName.Length - ".uscript".Length);
      }

      if (newName == oldName)
      {
         return false;
      }

      bool isSafe;
      var safeName = UnityCSharpGenerator.MakeSyntaxSafe(newName, out isSafe);
      if (string.IsNullOrEmpty(newName) || !isSafe)
      {
         EditorUtility.DisplayDialog(
            "Rename Graph",
            "Graph names must be all letters, numbers or underscores and must not start with a number.\n\nSuggested name: " + safeName,
            "OK");
         return false;
      }

      var directory = NormalizeSlashes(Path.GetDirectoryName(graphPath));
      var newGraphPath = directory + "/" + newName + ".uscript";

      // Case-only renames would collide with the old file on Windows/macOS
      if (string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase))
      {
         EditorUtility.DisplayDialog("Rename Graph", "Changing only the upper/lower case of a graph name is not supported. Choose a different name.", "OK");
         return false;
      }

      if (File.Exists(newGraphPath) || !string.IsNullOrEmpty(GetGraphPath(newName + ".uscript")))
      {
         EditorUtility.DisplayDialog("Rename Graph", string.Format("A graph called \"{0}\" already exists.", newName), "OK");
         return false;
      }

      if (GetAssemblyQualifiedType(newName) != null
          || GetAssemblyQualifiedType(newName + uScriptConfig.Files.GeneratedComponentExtension) != null)
      {
         EditorUtility.DisplayDialog(
            "Rename Graph",
            string.Format("\"{0}\" is already the name of a class in this project. Choose a different name.", newName),
            "OK");
         return false;
      }

      // Other graphs that use this graph (as a nested graph, or through its component) refer to it by
      // name. They are updated to the new name after the rename.
      var dependentGraphs = this.FindGraphsUsingGraph(oldName);
      if (dependentGraphs.Count > 0)
      {
         var proceed = EditorUtility.DisplayDialog(
            "Rename Graph",
            string.Format(
               "\"{0}\" is used by these graphs (as a nested graph, or through its component's variables, events or properties):\n\n{1}\n\nThey will be updated to use the new name.",
               oldName,
               FormatList(dependentGraphs.Keys.ToList())),
            "Rename and Update",
            "Cancel");

         if (!proceed)
         {
            return false;
         }
      }

      // The open graph must be saved first, otherwise the rename would lose its unsaved changes, or
      // (if it uses the renamed graph) saving it later would bring back the old name.
      var isOpenGraph = this.IsOpenGraph(graphPath);
      var openGraphPath = this.fullPath;
      var openGraphIsDependent = !string.IsNullOrEmpty(openGraphPath)
         && dependentGraphs.ContainsKey(Path.GetFileNameWithoutExtension(openGraphPath));
      if ((isOpenGraph || openGraphIsDependent) && !this.WasCurrentGraphSaved(true))
      {
         return false;
      }

      // The same goes for graphs kept in other tabs
      var unsavedTabs = this.UnsavedBackgroundTabs(new[] { oldName }.Concat(dependentGraphs.Keys));
      if (unsavedTabs.Count > 0)
      {
         EditorUtility.DisplayDialog(
            "Rename Graph",
            "Save or close these tabs first, they have unsaved changes:\n\n" + FormatList(unsavedTabs),
            "OK");
         return false;
      }

      // Work out where the generated scripts are before anything moves
      var oldWrapperPath = NormalizeSlashes(this.GetGeneratedScriptPath(graphPath));
      var oldLogicPath = NormalizeSlashes(this.GetNestedScriptPath(graphPath));
      var newWrapperPath = NormalizeSlashes(Path.GetDirectoryName(oldWrapperPath)) + "/" + newName + uScriptConfig.Files.GeneratedComponentExtension + ".cs";
      var newLogicPath = NormalizeSlashes(Path.GetDirectoryName(oldLogicPath)) + "/" + newName + uScriptConfig.Files.GeneratedCodeExtension + ".cs";

      if (File.Exists(newWrapperPath) || File.Exists(newLogicPath))
      {
         EditorUtility.DisplayDialog("Rename Graph", string.Format("Generated scripts called \"{0}\" already exist. Choose a different name.", newName), "OK");
         return false;
      }

      // Move the files. AssetDatabase.MoveAsset keeps the .meta files (GUIDs and labels).
      string error = MoveAssetIfExists(graphPath, newGraphPath);
      if (!string.IsNullOrEmpty(error))
      {
         EditorUtility.DisplayDialog("Rename Graph", "Could not rename the graph:\n" + error, "OK");
         return false;
      }

      error = MoveAssetIfExists(oldLogicPath, newLogicPath);
      if (string.IsNullOrEmpty(error))
      {
         error = MoveAssetIfExists(oldWrapperPath, newWrapperPath);
      }

      if (!string.IsNullOrEmpty(error))
      {
         uScriptDebug.Log("Rename Graph: could not move the generated scripts: " + error, uScriptDebug.Type.Warning);
      }

      DeleteGraphStatusFile(oldName);

      // Regenerate the code so the class names inside the moved scripts match the new file names
      AssetDatabase.StartAssetEditing();
      try
      {
         this.RebuildScript(newGraphPath, false, false);
      }
      finally
      {
         AssetDatabase.StopAssetEditing();
      }

      // Point the graphs that use the renamed graph at the new class names
      var failedUpdates = new List<string>();
      foreach (var dependent in dependentGraphs)
      {
         if (!this.UpdateGraphReferences(dependent.Key, dependent.Value, oldName, newName))
         {
            failedUpdates.Add(dependent.Key);
         }
      }

      this.RefreshAfterGraphFileChange();

      this.RenameGraphTab(graphPath, newGraphPath);

      if (isOpenGraph)
      {
         this.OpenGraph(newGraphPath, false);
      }
      else if (openGraphIsDependent)
      {
         this.OpenGraph(openGraphPath, false);
      }

      if (failedUpdates.Count > 0)
      {
         EditorUtility.DisplayDialog(
            "Rename Graph",
            "The graph was renamed, but these graphs could not be updated automatically:\n\n" + FormatList(failedUpdates)
            + "\n\nOpen them and replace the nodes that use the renamed graph.",
            "OK");
      }

      uScriptDebug.Log(string.Format("Renamed graph \"{0}\" to \"{1}\".", oldName, newName));
      return true;
   }

   /// <summary>
   /// Moves a graph and its generated scripts to the trash (they can be restored from there), after
   /// showing where the graph is still used.
   /// </summary>
   /// <param name="graphPath">Absolute path of the .uscript file.</param>
   /// <returns>True if the graph was deleted.</returns>
   public bool DeleteGraph(string graphPath)
   {
      graphPath = NormalizeSlashes(graphPath);
      if (!File.Exists(graphPath))
      {
         EditorUtility.DisplayDialog("Delete Graph", "The graph file could not be found:\n" + graphPath, "OK");
         return false;
      }

      var graphName = Path.GetFileNameWithoutExtension(graphPath);

      // Nested usage would leave other graphs' generated code referring to a class that no longer exists,
      // which stops the whole project from compiling.
      var parentGraphs = this.FindGraphsUsingGraph(graphName).Keys.ToList();
      if (parentGraphs.Count > 0)
      {
         EditorUtility.DisplayDialog(
            "Delete Graph",
            string.Format(
               "\"{0}\" is used by these graphs (as a nested graph, or through its component's variables, events or properties):\n\n{1}\n\nDeleting it would stop your project from compiling. Remove those nodes from them first.",
               graphName,
               FormatList(parentGraphs)),
            "OK");
         return false;
      }

      var wrapperPath = NormalizeSlashes(this.GetGeneratedScriptPath(graphPath));
      var logicPath = NormalizeSlashes(this.GetNestedScriptPath(graphPath));

      var files = new List<string> { graphPath };
      if (File.Exists(logicPath)) files.Add(logicPath);
      if (File.Exists(wrapperPath)) files.Add(wrapperPath);

      var message = new StringBuilder();
      message.AppendFormat("Delete the graph \"{0}\"?\n\nThese files will be moved to the trash:\n", graphName);
      foreach (var file in files)
      {
         message.AppendLine("  " + Path.GetFileName(file));
      }

      var users = FindAssetsUsingScript(wrapperPath);
      if (users.Count > 0)
      {
         message.AppendFormat(
            "\nWARNING: the graph is attached to objects in:\n{0}\nThose objects will show a \"Missing Script\" component.\n",
            FormatList(users));
      }

      if (!EditorUtility.DisplayDialog("Delete Graph", message.ToString(), "Move to Trash", "Cancel"))
      {
         return false;
      }

      // Don't leave the deleted graph open in the editor
      this.RemoveGraphTab(graphPath);
      if (this.IsOpenGraph(graphPath))
      {
         this.CloseDeletedOpenGraph();
      }

      var failed = new List<string>();
      foreach (var file in files)
      {
         if (!AssetDatabase.MoveAssetToTrash(ToAssetPath(file)))
         {
            failed.Add(Path.GetFileName(file));
         }
      }

      DeleteGraphStatusFile(graphName);
      this.RefreshAfterGraphFileChange();

      if (failed.Count > 0)
      {
         EditorUtility.DisplayDialog("Delete Graph", "Some files could not be moved to the trash:\n\n" + FormatList(failed), "OK");
         return false;
      }

      uScriptDebug.Log(string.Format("Deleted graph \"{0}\" (moved to the trash).", graphName));
      return true;
   }

   /// <summary>
   /// Finds the graphs whose generated code uses the given graph, either as a nested node (class "Name")
   /// or through its component (class "Name_Component", e.g. reading its variables or listening to its
   /// events). Those graphs refer to the classes by name.
   /// </summary>
   /// <returns>Graph name -> the generated script files of that graph that mention the given graph.</returns>
   private Dictionary<string, List<string>> FindGraphsUsingGraph(string graphName)
   {
      var result = new Dictionary<string, List<string>>();
      var pattern = GraphClassNamePattern(graphName);
      var ownFiles = new[] { graphName + ".cs", graphName + uScriptConfig.Files.GeneratedComponentExtension + ".cs" };

      foreach (var file in GetFilePathsWithLabel("uScriptCode"))
      {
         var fileName = Path.GetFileName(file);
         if (ownFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase) || !File.Exists(file))
         {
            continue;
         }

         string contents;
         try
         {
            contents = File.ReadAllText(file);
         }
         catch (Exception)
         {
            continue;
         }

         if (!pattern.IsMatch(contents))
         {
            continue;
         }

         var name = Path.GetFileNameWithoutExtension(file);
         if (name.EndsWith(uScriptConfig.Files.GeneratedComponentExtension, StringComparison.Ordinal))
         {
            name = name.Substring(0, name.Length - uScriptConfig.Files.GeneratedComponentExtension.Length);
         }

         List<string> files;
         if (!result.TryGetValue(name, out files))
         {
            files = new List<string>();
            result[name] = files;
         }

         files.Add(file);
      }

      return result;
   }

   /// <summary>Matches the graph's two class names ("Name" and "Name_Component") as whole identifiers.</summary>
   private static Regex GraphClassNamePattern(string graphName)
   {
      // The component name is listed first so it wins over the shorter graph name
      return new Regex(
         @"\b(" + Regex.Escape(graphName + uScriptConfig.Files.GeneratedComponentExtension) + "|" + Regex.Escape(graphName) + @")\b");
   }

   /// <summary>
   /// Updates a graph that uses a renamed graph: the class names stored in its nodes (saved .uscript data) and
   /// in its generated code. The generated code is patched directly so the project compiles straight away;
   /// it is regenerated the next time the graph is saved.
   /// </summary>
   private bool UpdateGraphReferences(string graphName, List<string> generatedFiles, string oldName, string newName)
   {
      var ok = true;
      var component = uScriptConfig.Files.GeneratedComponentExtension;

      // 1. Generated code
      var pattern = GraphClassNamePattern(oldName);
      foreach (var file in generatedFiles)
      {
         try
         {
            var bytes = File.ReadAllBytes(file);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));

            var updated = pattern.Replace(text, match => match.Value == oldName + component ? newName + component : newName);
            if (updated != text)
            {
               File.WriteAllText(file, updated, new UTF8Encoding(hasBom));
            }
         }
         catch (Exception e)
         {
            uScriptDebug.Log(string.Format("Rename Graph: could not update {0}: {1}", file, e.Message), uScriptDebug.Type.Warning);
            ok = false;
         }
      }

      // 2. Saved graph data
      var graphPath = GetGraphPath(graphName + ".uscript");
      if (string.IsNullOrEmpty(graphPath) || !File.Exists(graphPath))
      {
         uScriptDebug.Log(string.Format("Rename Graph: could not find the graph file for \"{0}\".", graphName), uScriptDebug.Type.Warning);
         return false;
      }

      try
      {
         var reader = new ScriptEditor(string.Empty, null, null);
         if (!reader.Open(graphPath))
         {
            uScriptDebug.Log(string.Format("Rename Graph: could not open {0}.", graphPath), uScriptDebug.Type.Warning);
            return false;
         }

         var data = reader.ScriptEditorData;
         var changed = false;

         Func<string, string> rename = value => RenameTypeName(value, oldName, newName);
         foreach (var nodeData in data.NodeDatas)
         {
            RewriteTypeNames(nodeData, null, rename, ref changed, 0);

            // A nested graph node shows the graph's name unless a friendly name was set
            var logicNode = nodeData as Detox.Data.ScriptEditor.LogicNodeData;
            if (logicNode != null && logicNode.FriendlyName == oldName)
            {
               logicNode.FriendlyName = newName;
               changed = true;
            }
         }

         if (changed)
         {
            // The data setter adds nodes, so load it into a fresh editor before writing it back out.
            // ToBase64(path) also refreshes uScript's in-memory cache of this graph.
            var writer = new ScriptEditor(string.Empty, null, null);
            writer.ScriptEditorData = data;

            var base64 = writer.ToBase64(graphPath);
            File.WriteAllText(graphPath, "/*[[BEGIN BASE64\r\n" + base64 + "\r\nEND BASE64]]*/");
         }
      }
      catch (Exception e)
      {
         uScriptDebug.Log(string.Format("Rename Graph: could not update {0}: {1}", graphPath, e.Message), uScriptDebug.Type.Warning);
         ok = false;
      }

      return ok;
   }

   // Fields of the graph data that hold class names
   private static readonly HashSet<string> TypeNameFields = new HashSet<string> { "Type", "ComponentType", "EventArgs", "AutoLinkType" };

   /// <summary>Renames "Old", "Old_Component" and forms like "Old_Component[]" or "Old+Nested" in a stored type name.</summary>
   private static string RenameTypeName(string value, string oldName, string newName)
   {
      if (string.IsNullOrEmpty(value))
      {
         return value;
      }

      var component = uScriptConfig.Files.GeneratedComponentExtension;
      var pairs = new[]
      {
         new KeyValuePair<string, string>(oldName + component, newName + component),
         new KeyValuePair<string, string>(oldName, newName)
      };

      foreach (var pair in pairs)
      {
         if (value == pair.Key)
         {
            return pair.Value;
         }

         if (value.StartsWith(pair.Key, StringComparison.Ordinal) && "[+.,<&".IndexOf(value[pair.Key.Length]) >= 0)
         {
            return pair.Value + value.Substring(pair.Key.Length);
         }
      }

      return value;
   }

   /// <summary>
   /// Walks uScript's graph data objects (classes, structs and arrays in the Detox namespaces) and renames
   /// class names held in the fields listed in TypeNameFields. Returns the (possibly boxed and updated) value.
   /// </summary>
   private static object RewriteTypeNames(object value, string fieldName, Func<string, string> rename, ref bool changed, int depth)
   {
      if (value == null || depth > 12)
      {
         return value;
      }

      var text = value as string;
      if (text != null)
      {
         if (fieldName != null && TypeNameFields.Contains(fieldName))
         {
            var renamed = rename(text);
            if (renamed != text)
            {
               changed = true;
               return renamed;
            }
         }

         return value;
      }

      var type = value.GetType();
      if (type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(Guid))
      {
         return value;
      }

      var array = value as Array;
      if (array != null)
      {
         for (var i = 0; i < array.Length; i++)
         {
            var element = array.GetValue(i);
            var updated = RewriteTypeNames(element, fieldName, rename, ref changed, depth + 1);
            if (!ReferenceEquals(element, updated) || (element != null && element.GetType().IsValueType))
            {
               array.SetValue(updated, i);
            }
         }

         return value;
      }

      if (type.Namespace == null || !type.Namespace.StartsWith("Detox", StringComparison.Ordinal))
      {
         return value;
      }

      const System.Reflection.BindingFlags Flags =
         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
         | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;

      for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
      {
         foreach (var field in t.GetFields(Flags))
         {
            if (field.IsLiteral || field.IsInitOnly)
            {
               continue;
            }

            var fieldValue = field.GetValue(value);
            if (fieldValue == null || (fieldValue.GetType().IsPrimitive || fieldValue.GetType().IsEnum))
            {
               continue;
            }

            var updated = RewriteTypeNames(fieldValue, CleanFieldName(field.Name), rename, ref changed, depth + 1);
            if (!ReferenceEquals(fieldValue, updated) || fieldValue.GetType().IsValueType)
            {
               field.SetValue(value, updated);
            }
         }
      }

      return value;
   }

   /// <summary>"&lt;Type&gt;k__BackingField" -> "Type"</summary>
   private static string CleanFieldName(string name)
   {
      if (name.StartsWith("<", StringComparison.Ordinal))
      {
         var end = name.IndexOf('>');
         if (end > 1)
         {
            return name.Substring(1, end - 1);
         }
      }

      return name;
   }

   /// <summary>
   /// Returns the scenes and prefabs that reference a script. Only works for text-serialized assets
   /// (the Unity default, "Force Text").
   /// </summary>
   private static List<string> FindAssetsUsingScript(string scriptPath)
   {
      var result = new List<string>();
      if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
      {
         return result;
      }

      var guid = AssetDatabase.AssetPathToGUID(ToAssetPath(scriptPath));
      if (string.IsNullOrEmpty(guid))
      {
         return result;
      }

      var needle = "guid: " + guid;
      var candidates = Directory.GetFiles(Application.dataPath, "*.unity", SearchOption.AllDirectories)
         .Concat(Directory.GetFiles(Application.dataPath, "*.prefab", SearchOption.AllDirectories));

      foreach (var file in candidates)
      {
         try
         {
            if (File.ReadAllText(file).Contains(needle))
            {
               result.Add(ToAssetPath(file));
            }
         }
         catch (Exception)
         {
            // Unreadable or binary file, skip it
         }
      }

      return result;
   }

   private bool IsOpenGraph(string graphPath)
   {
      return !string.IsNullOrEmpty(this.fullPath)
         && string.Equals(ToAssetPath(this.fullPath), ToAssetPath(graphPath), StringComparison.OrdinalIgnoreCase);
   }

   private void RefreshAfterGraphFileChange()
   {
      AssetDatabase.Refresh();
      uScriptBackgroundProcess.ForceFileRefresh();
      this.Repaint();
   }

   private static string MoveAssetIfExists(string fromPath, string toPath)
   {
      if (!File.Exists(fromPath))
      {
         return string.Empty;
      }

      return AssetDatabase.MoveAsset(ToAssetPath(fromPath), ToAssetPath(toPath));
   }

   private static void DeleteGraphStatusFile(string graphName)
   {
      try
      {
         var statusFile = Application.dataPath + "/../Library/uScriptCache/" + graphName + ".status";
         if (File.Exists(statusFile))
         {
            File.Delete(statusFile);
         }
      }
      catch (Exception)
      {
         // The status cache is rebuilt automatically
      }
   }

   private static string ToAssetPath(string path)
   {
      path = NormalizeSlashes(path);
      var dataPath = NormalizeSlashes(Application.dataPath);
      return path.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)
         ? "Assets" + path.Substring(dataPath.Length)
         : path;
   }

   private static string NormalizeSlashes(string path)
   {
      return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
   }

   private static string FormatList(List<string> items)
   {
      var shown = items.Take(MaxListedUsages).Select(item => "  • " + item);
      var text = string.Join("\n", shown.ToArray());
      if (items.Count > MaxListedUsages)
      {
         text += string.Format("\n  ...and {0} more", items.Count - MaxListedUsages);
      }

      return text;
   }
}
