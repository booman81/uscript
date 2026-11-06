// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptErrorLocator.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Finds the uScript graph and node behind Console errors and adds a "Show node in uScript" link.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Detox.Editor;

using UnityEditor;

using UnityEngine;

/// <summary>
/// Watches the Console. When an error or exception comes from generated uScript code, it logs one extra line
/// naming the graph and node, with a link that opens the graph and selects the node. Editor only.
/// </summary>
[InitializeOnLoad]
public static class uScriptErrorLocator
{
   private const string MessagePrefix = "uScript ▶ ";
   private const int MaxReported = 500;

   // "Zombie_Manager:Relay_AtIndex_823 ()" (Debug.Log stack) or "Zombie_Manager.Relay_AtIndex_823 ()" (exception stack)
   private static readonly Regex RelayFrame = new Regex(@"(?<type>[A-Za-z_][\w.+]*)[:.](?<method>Relay_\w+?_(?<id>\d+))\s*\(", RegexOptions.Compiled);
   private static readonly Regex NodeMapLine = new Regex(@"^\s*// uScriptNode (?<id>\d+) (?<guid>[0-9a-fA-F-]{36}) ?(?<name>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
   private static readonly Regex DebugBreakCall = new Regex(@"CheckDebugBreak\(""(?<guid>[0-9a-fA-F-]{36})"", ""(?<name>[^""]*)""", RegexOptions.Compiled);

   private static readonly HashSet<string> Reported = new HashSet<string>();
   private static readonly Dictionary<string, string> LogicFileCache = new Dictionary<string, string>();
   private static readonly Dictionary<string, CachedFile> FileCache = new Dictionary<string, CachedFile>();

   private static bool isLogging;

   static uScriptErrorLocator()
   {
      Application.logMessageReceived += OnLogMessageReceived;
#if UNITY_2021_2_OR_NEWER
      EditorGUI.hyperLinkClicked += OnHyperLinkClicked;
#endif
      EditorApplication.playModeStateChanged += state =>
      {
         Reported.Clear();
         LogicFileCache.Clear();
      };
   }

   private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
   {
      if (isLogging || string.IsNullOrEmpty(stackTrace) || string.IsNullOrEmpty(condition))
      {
         return;
      }

      var isProblem = type == LogType.Error || type == LogType.Exception || type == LogType.Assert
         || (type == LogType.Warning && condition.StartsWith("uScript", StringComparison.Ordinal));
      if (!isProblem || condition.StartsWith(MessagePrefix, StringComparison.Ordinal))
      {
         return;
      }

      try
      {
         // The first generated graph method in the stack is the node that was running
         foreach (Match frame in RelayFrame.Matches(stackTrace))
         {
            var typeName = frame.Groups["type"].Value;
            var graphName = typeName.Substring(typeName.LastIndexOfAny(new[] { '.', '+' }) + 1);

            // Errors often repeat every frame: report each graph/node/message once per Play session
            var firstLine = condition.Split('\n')[0];
            var key = graphName + "|" + frame.Groups["id"].Value + "|" + firstLine;
            if (Reported.Contains(key))
            {
               return;
            }

            string logicPath;
            if (!LogicFileCache.TryGetValue(graphName, out logicPath))
            {
               logicPath = FindGeneratedLogicFile(graphName);
               LogicFileCache[graphName] = logicPath;
            }

            if (logicPath == null)
            {
               continue;
            }

            if (Reported.Count > MaxReported)
            {
               Reported.Clear();
            }

            Reported.Add(key);

            Guid nodeGuid;
            string nodeName;
            var found = TryFindNode(logicPath, frame.Groups["method"].Value, int.Parse(frame.Groups["id"].Value), out nodeGuid, out nodeName);

            LogLocation(graphName, frame.Groups["method"].Value, found ? nodeGuid : Guid.Empty, nodeName);
            return;
         }
      }
      catch (Exception)
      {
         // Never let the locator itself cause Console noise
      }
   }

   private static void LogLocation(string graphName, string method, Guid nodeGuid, string nodeName)
   {
      string message;
      if (nodeGuid != Guid.Empty)
      {
         message = string.Format(
            "{0}The error above came from graph <b>{1}</b>, node <b>{2}</b>.  {3}",
            MessagePrefix,
            graphName,
            string.IsNullOrEmpty(nodeName) ? method : nodeName.Replace('_', ' '),
            Link(graphName, nodeGuid.ToString(), "Show node in uScript"));
      }
      else
      {
         message = string.Format(
            "{0}The error above came from graph <b>{1}</b> ({2}).  {3}\nSave that graph once with this uScript version so the exact node can be shown next time.",
            MessagePrefix,
            graphName,
            method,
            Link(graphName, string.Empty, "Open graph in uScript"));
      }

      isLogging = true;
      try
      {
         Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", message);
      }
      finally
      {
         isLogging = false;
      }
   }

   private static string Link(string graphName, string nodeGuid, string text)
   {
#if UNITY_6000_0_OR_NEWER
      // Unity 6 Console (TextCore): <link="name='value' ...">
      return string.Format(
         "<color=#4C9AFF><link=\"uscriptgraph='{0}' uscriptnode='{1}'\">{2}</link></color>",
         graphName,
         nodeGuid,
         text);
#elif UNITY_2021_2_OR_NEWER
      // Unity 2021.2 - 2023 Console: <a name="value" ...>
      return string.Format(
         "<color=#4C9AFF><a uscriptgraph=\"{0}\" uscriptnode=\"{1}\">{2}</a></color>",
         graphName,
         nodeGuid,
         text);
#else
      return string.Empty;
#endif
   }

#if UNITY_2021_2_OR_NEWER
   private static void OnHyperLinkClicked(EditorWindow window, HyperLinkClickedEventArgs args)
   {
      string graphName;
      if (args == null || args.hyperLinkData == null || !args.hyperLinkData.TryGetValue("uscriptgraph", out graphName))
      {
         return;
      }

      string nodeText;
      args.hyperLinkData.TryGetValue("uscriptnode", out nodeText);

      Guid nodeGuid;
      try
      {
         nodeGuid = string.IsNullOrEmpty(nodeText) ? Guid.Empty : new Guid(nodeText);
      }
      catch (Exception)
      {
         nodeGuid = Guid.Empty;
      }

      uScript.ShowGraphNode(graphName, nodeGuid);
   }
#endif

   private static string FindGeneratedLogicFile(string graphName)
   {
      if (string.IsNullOrEmpty(graphName) || graphName.EndsWith(uScriptConfig.Files.GeneratedComponentExtension, StringComparison.Ordinal))
      {
         return null;
      }

      // The graph must exist; this filters out other code that happens to have "Relay_" methods
      if (string.IsNullOrEmpty(uScript.GetGraphPath(graphName + ".uscript")))
      {
         return null;
      }

      var fileName = graphName + uScriptConfig.Files.GeneratedCodeExtension + ".cs";
      foreach (var path in uScript.GetGraphPaths("uScriptCode"))
      {
         if (Path.GetFileName(path) == fileName && File.Exists(path))
         {
            return path;
         }
      }

      return null;
   }

   private static bool TryFindNode(string logicPath, string method, int id, out Guid nodeGuid, out string nodeName)
   {
      nodeGuid = Guid.Empty;
      nodeName = string.Empty;

      var file = ReadCached(logicPath);
      if (file == null)
      {
         return false;
      }

      // 1. The node map written by this uScript version
      NodeInfo info;
      if (file.NodeMap.TryGetValue(id, out info))
      {
         nodeGuid = info.Guid;
         nodeName = info.Name;
         return true;
      }

      // 2. Graphs saved with debug info call CheckDebugBreak("guid", "Name", ...) at the start of each relay method
      var start = file.Text.IndexOf("void " + method + "()", StringComparison.Ordinal);
      if (start >= 0)
      {
         var body = file.Text.Substring(start, Math.Min(800, file.Text.Length - start));
         var match = DebugBreakCall.Match(body);
         if (match.Success)
         {
            nodeGuid = new Guid(match.Groups["guid"].Value);
            nodeName = match.Groups["name"].Value;
            return true;
         }
      }

      return false;
   }

   private static CachedFile ReadCached(string path)
   {
      try
      {
         var modified = File.GetLastWriteTimeUtc(path);
         CachedFile cached;
         if (FileCache.TryGetValue(path, out cached) && cached.Modified == modified)
         {
            return cached;
         }

         cached = new CachedFile { Modified = modified, Text = File.ReadAllText(path), NodeMap = new Dictionary<int, NodeInfo>() };
         foreach (Match match in NodeMapLine.Matches(cached.Text))
         {
            cached.NodeMap[int.Parse(match.Groups["id"].Value)] = new NodeInfo
            {
               Guid = new Guid(match.Groups["guid"].Value),
               Name = match.Groups["name"].Value.Trim()
            };
         }

         FileCache[path] = cached;
         return cached;
      }
      catch (Exception)
      {
         return null;
      }
   }

   private sealed class CachedFile
   {
      public DateTime Modified;
      public string Text;
      public Dictionary<int, NodeInfo> NodeMap;
   }

   private struct NodeInfo
   {
      public Guid Guid;
      public string Name;
   }
}
