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

      // Other graphs that contain this graph as a nested node refer to it by name, so they would break.
      var parentGraphs = this.FindGraphsUsingGraph(oldName);
      if (parentGraphs.Count > 0)
      {
         EditorUtility.DisplayDialog(
            "Rename Graph",
            string.Format(
               "\"{0}\" is used by these graphs (as a nested graph, or through its component's variables, events or properties):\n\n{1}\n\nThey refer to it by name, so renaming it would stop your project from compiling. Remove those nodes from them first, rename it, then add them back.",
               oldName,
               FormatList(parentGraphs)),
            "OK");
         return false;
      }

      // The open graph must be saved first, otherwise the rename would lose its unsaved changes
      var isOpenGraph = this.IsOpenGraph(graphPath);
      if (isOpenGraph && !this.WasCurrentGraphSaved(true))
      {
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

      this.RefreshAfterGraphFileChange();

      if (isOpenGraph)
      {
         this.OpenGraph(newGraphPath, false);
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
      var parentGraphs = this.FindGraphsUsingGraph(graphName);
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
      if (this.IsOpenGraph(graphPath))
      {
         this.CreateNewGraph();
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
   /// Returns the names of graphs whose generated code uses the given graph, either as a nested node
   /// (class "Name") or through its component (class "Name_Component", e.g. reading its variables or
   /// listening to its events). Those graphs refer to the classes by name.
   /// </summary>
   private List<string> FindGraphsUsingGraph(string graphName)
   {
      var result = new List<string>();
      var pattern = new Regex(
         @"\b(" + Regex.Escape(graphName) + "|" + Regex.Escape(graphName + uScriptConfig.Files.GeneratedComponentExtension) + @")\b");
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

         if (!result.Contains(name))
         {
            result.Add(name);
         }
      }

      return result;
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
