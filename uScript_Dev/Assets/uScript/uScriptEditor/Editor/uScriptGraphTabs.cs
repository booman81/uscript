// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptGraphTabs.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Several graphs open in tabs. The open graph is the normal uScript graph; the other tabs keep their unsaved
//   changes in memory (as the same base64 data uScript uses to survive script reloads) until they are shown again.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Detox.Editor;
using Detox.ScriptEditor;

using UnityEditor;

using UnityEngine;

public sealed partial class uScript
{
   private const string OpenTabsSetting = "uScript\\OpenTabs";
   private const char TabPathSeparator = '|';

   [SerializeField]
   private List<GraphTab> graphTabs = new List<GraphTab>();

   [SerializeField]
   private int activeGraphTab = -1;

   [Serializable]
   private sealed class GraphTab
   {
      /// <summary>Full path of the .uscript file; empty for a graph that was never saved.</summary>
      public string Path = string.Empty;

      /// <summary>Unsaved graph data while the tab is in the background (null = load from file).</summary>
      public string Script;

      public string ScriptName = string.Empty;
      public bool Dirty;
      public string CanvasPosition = string.Empty;
   }

   private GraphTab ActiveGraphTab
   {
      get
      {
         return this.graphTabs != null && this.activeGraphTab >= 0 && this.activeGraphTab < this.graphTabs.Count
            ? this.graphTabs[this.activeGraphTab]
            : null;
      }
   }

   private static bool SameGraphPath(string a, string b)
   {
      if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
      {
         return false;
      }

      try
      {
         a = System.IO.Path.GetFullPath(a);
         b = System.IO.Path.GetFullPath(b);
      }
      catch (Exception)
      {
         // Compare as given
      }

      return string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
   }

   private int FindGraphTab(string path)
   {
      if (this.graphTabs == null)
      {
         return -1;
      }

      for (var i = 0; i < this.graphTabs.Count; i++)
      {
         if (SameGraphPath(this.graphTabs[i].Path, path))
         {
            return i;
         }
      }

      return -1;
   }

   private static string GraphTabName(GraphTab tab)
   {
      return string.IsNullOrEmpty(tab.Path) ? "Untitled" : System.IO.Path.GetFileNameWithoutExtension(tab.Path);
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Keeping the list in step with the open graph
   // ---------------------------------------------------------------------------------------------------------------

   /// <summary>Adds the open graph as the active tab (or activates its existing tab).</summary>
   private void SetActiveGraphTab(string path)
   {
      if (this.graphTabs == null)
      {
         this.graphTabs = new List<GraphTab>();
      }

      var index = this.FindGraphTab(path);
      if (index < 0)
      {
         // An unchanged "Untitled" tab is replaced instead of keeping an empty graph around
         var active = this.ActiveGraphTab;
         if (active != null && string.IsNullOrEmpty(active.Path) && !active.Dirty)
         {
            active.Path = path;
            index = this.activeGraphTab;
         }
         else
         {
            this.graphTabs.Add(new GraphTab { Path = path ?? string.Empty });
            index = this.graphTabs.Count - 1;
         }
      }

      this.activeGraphTab = index;
      this.graphTabs[index].Script = null;
      this.graphTabs[index].Dirty = false;
      this.SaveGraphTabList();
   }

   private void SaveGraphTabList()
   {
      var paths = this.graphTabs
         .Where(tab => !string.IsNullOrEmpty(tab.Path))
         .Select(tab => uScriptConfig.ConstantPaths.RelativePath(tab.Path))
         .Where(path => path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
         .Select(path => path.Substring("Assets".Length));

      SetSetting(OpenTabsSetting, string.Join(TabPathSeparator.ToString(), paths.ToArray()));
   }

   /// <summary>Called when uScript starts: brings back the tabs of the last session (paths only).</summary>
   private void RestoreGraphTabs()
   {
      if (this.graphTabs == null)
      {
         this.graphTabs = new List<GraphTab>();
      }

      if (this.graphTabs.Count > 0)
      {
         return;
      }

      var saved = Preferences.GetString(OpenTabsSetting, string.Empty);
      foreach (var relative in saved.Split(new[] { TabPathSeparator }, StringSplitOptions.RemoveEmptyEntries))
      {
         var path = Application.dataPath + relative;
         if (File.Exists(path) && this.FindGraphTab(path) < 0)
         {
            this.graphTabs.Add(new GraphTab { Path = path });
         }
      }

      this.activeGraphTab = -1;
   }

   /// <summary>Runs on Layout: follows Save As / first save, and drops tabs whose file is gone.</summary>
   private void SyncGraphTabs()
   {
      if (this.graphTabs == null)
      {
         this.graphTabs = new List<GraphTab>();
      }

      if (m_ScriptEditorCtrl == null)
      {
         return;
      }

      var active = this.ActiveGraphTab;
      if (active == null)
      {
         var index = this.FindGraphTab(this.fullPath);
         if (index < 0)
         {
            this.graphTabs.Add(new GraphTab { Path = this.fullPath ?? string.Empty });
            index = this.graphTabs.Count - 1;
         }

         this.activeGraphTab = index;
         active = this.graphTabs[index];
         this.SaveGraphTabList();
      }

      if (!SameGraphPath(active.Path, this.fullPath) && !string.IsNullOrEmpty(this.fullPath))
      {
         // Saved under a new name (Save As or first save). If that file has its own tab already, merge them.
         var other = this.FindGraphTab(this.fullPath);
         active.Path = this.fullPath;
         if (other >= 0 && other != this.activeGraphTab)
         {
            this.graphTabs.RemoveAt(other);
            if (other < this.activeGraphTab)
            {
               this.activeGraphTab--;
            }
         }

         this.SaveGraphTabList();
      }

      // Unchanged background tabs whose file was deleted or moved outside uScript
      for (var i = this.graphTabs.Count - 1; i >= 0; i--)
      {
         var tab = this.graphTabs[i];
         if (i != this.activeGraphTab && tab.Script == null && !string.IsNullOrEmpty(tab.Path) && !File.Exists(tab.Path))
         {
            this.graphTabs.RemoveAt(i);
            if (i < this.activeGraphTab)
            {
               this.activeGraphTab--;
            }
         }
      }
   }

   /// <summary>
   /// Moves the open graph into its tab (keeping unsaved changes in memory) so another graph can be shown without
   /// asking to save. Returns False when there is no tab to keep it in.
   /// </summary>
   private bool StashCurrentGraphIntoTab()
   {
      var tab = this.ActiveGraphTab;
      if (m_ScriptEditorCtrl == null || tab == null)
      {
         return false;
      }

      tab.Dirty = m_ScriptEditorCtrl.IsDirty;
      tab.ScriptName = m_ScriptEditorCtrl.ScriptEditor.Name;
      tab.Script = tab.Dirty || string.IsNullOrEmpty(tab.Path) ? m_ScriptEditorCtrl.ScriptEditor.ToBase64(null) : null;

      var location = m_ScriptEditorCtrl.FlowChart.Location;
      tab.CanvasPosition = location.X + "," + location.Y;

      return true;
   }

   /// <summary>Shows a tab whose graph data is held in memory.</summary>
   private void ShowCachedGraphTab(int index)
   {
      var tab = this.graphTabs[index];

      this.fullPath = tab.Path ?? string.Empty;
      this.currentScript = tab.Script;
      this.currentScriptName = tab.ScriptName;
      this.currentScriptDirty = tab.Dirty;
      this.currentCanvasPosition = tab.CanvasPosition;
      this.mapScale = 1.0f;

      ClearChangeStack();
      OpenFromCache();

      if (m_ScriptEditorCtrl != null)
      {
         m_ScriptEditorCtrl.IsDirty = tab.Dirty;
      }

      SetSetting(
         "uScript\\LastOpened",
         string.IsNullOrEmpty(this.fullPath) ? string.Empty : uScriptConfig.ConstantPaths.RelativePath(this.fullPath).Substring("Assets".Length));

      this.activeGraphTab = index;
      tab.Script = null;
      this.SaveGraphTabList();
      this.Repaint();
   }

   /// <summary>
   /// Called by OpenGraph before it opens a file. Returns True when the graph was shown from a tab's memory
   /// (nothing more to do), and sets <paramref name="canceled"/> when the user canceled.
   /// </summary>
   private bool PrepareOpenGraphInTab(string path, out bool canceled)
   {
      canceled = false;

      var index = this.FindGraphTab(path);
      var current = this.ActiveGraphTab;

      if (current != null && index == this.activeGraphTab && SameGraphPath(path, this.fullPath))
      {
         // Opening the graph that is already showing: keep it as it is (unsaved changes included)
         if (m_ScriptEditorCtrl != null && m_ScriptEditorCtrl.IsDirty)
         {
            return true;
         }
      }

      if (!this.StashCurrentGraphIntoTab() && !this.WasCurrentGraphSaved(true))
      {
         canceled = true;
         return false;
      }

      if (index >= 0 && this.graphTabs[index].Script != null)
      {
         this.ShowCachedGraphTab(index);
         return true;
      }

      return false;
   }

   // ---------------------------------------------------------------------------------------------------------------
   // User actions
   // ---------------------------------------------------------------------------------------------------------------

   private void ActivateGraphTab(GraphTab tab)
   {
      try
      {
         this.ActivateGraphTabNow(tab);
      }
      finally
      {
         this.RefreshAfterTabChange();
      }
   }

   /// <summary>
   /// Tab actions run from EditorApplication.delayCall, outside OnGUI, so nothing would redraw the window until the
   /// mouse moves; ask for a couple of redraws so the new graph (and the tab bar) show straight away.
   /// </summary>
   private void RefreshAfterTabChange()
   {
      if (m_ScriptEditorCtrl != null)
      {
         m_ScriptEditorCtrl.FlowChart.Invalidate();
      }

      RequestRepaint(3);
      this.Repaint();
   }

   private bool IsShowing(GraphTab tab)
   {
      return string.IsNullOrEmpty(tab.Path)
         ? string.IsNullOrEmpty(this.fullPath)
         : SameGraphPath(tab.Path, this.fullPath);
   }

   private void ActivateGraphTabNow(GraphTab tab)
   {
      var index = this.graphTabs.IndexOf(tab);
      if (index < 0)
      {
         return;
      }

      if (index == this.activeGraphTab)
      {
         if (m_ScriptEditorCtrl != null && this.IsShowing(tab))
         {
            return;   // already showing
         }

         // The tab is marked active but another graph is showing: load it again from file
         this.activeGraphTab = -1;
         if (!string.IsNullOrEmpty(tab.Path) && File.Exists(tab.Path))
         {
            this.OpenGraph(tab.Path, false);
         }

         return;
      }

      if (tab.Script != null)
      {
         this.StashCurrentGraphIntoTab();
         this.ShowCachedGraphTab(index);
         return;
      }

      if (string.IsNullOrEmpty(tab.Path) || !File.Exists(tab.Path))
      {
         this.graphTabs.RemoveAt(index);
         if (index < this.activeGraphTab)
         {
            this.activeGraphTab--;
         }

         this.SaveGraphTabList();
         return;
      }

      this.OpenGraph(tab.Path, false);
   }

   private void CloseGraphTab(GraphTab tab)
   {
      try
      {
         this.CloseGraphTabNow(tab);
      }
      finally
      {
         this.RefreshAfterTabChange();
      }
   }

   private void CloseGraphTabNow(GraphTab tab)
   {
      var index = this.graphTabs.IndexOf(tab);
      if (index < 0)
      {
         return;
      }

      if (index != this.activeGraphTab)
      {
         if (tab.Dirty && tab.Script != null)
         {
            if (string.IsNullOrEmpty(tab.Path))
            {
               // A graph that was never saved needs Save As: show it and ask the usual way
               this.ActivateGraphTab(tab);
               if (this.graphTabs.IndexOf(tab) == this.activeGraphTab)
               {
                  this.CloseGraphTab(tab);
               }

               return;
            }

            if (!this.AskToSaveBackgroundTab(tab, true))
            {
               return;
            }
         }

         this.graphTabs.RemoveAt(index);
         if (index < this.activeGraphTab)
         {
            this.activeGraphTab--;
         }

         this.SaveGraphTabList();
         this.Repaint();
         return;
      }

      if (!this.WasCurrentGraphSaved(true))
      {
         return;
      }

      // Nothing to keep from the closed graph
      if (m_ScriptEditorCtrl != null)
      {
         m_ScriptEditorCtrl.IsDirty = false;
      }

      this.graphTabs.RemoveAt(index);
      this.activeGraphTab = -1;

      if (this.graphTabs.Count == 0)
      {
         this.CreateNewGraph();
      }
      else
      {
         this.ShowGraphTabAfterClose(Math.Min(index, this.graphTabs.Count - 1));
      }

      this.SaveGraphTabList();
      this.Repaint();
   }

   private void ShowGraphTabAfterClose(int index)
   {
      var tab = this.graphTabs[index];

      if (tab.Script != null)
      {
         this.ShowCachedGraphTab(index);
      }
      else if (!string.IsNullOrEmpty(tab.Path) && File.Exists(tab.Path))
      {
         // There is no active tab now, so OpenGraph does not try to keep the closed graph
         this.OpenGraph(tab.Path, false);
      }
      else
      {
         this.graphTabs.RemoveAt(index);
         if (this.graphTabs.Count == 0)
         {
            this.CreateNewGraph();
         }
         else
         {
            this.ShowGraphTabAfterClose(Math.Min(index, this.graphTabs.Count - 1));
         }
      }
   }

   private void CloseOtherGraphTabs(GraphTab keep)
   {
      this.ActivateGraphTab(keep);
      if (this.ActiveGraphTab != keep)
      {
         return;
      }

      foreach (var tab in this.graphTabs.ToArray())
      {
         if (tab == keep)
         {
            continue;
         }

         this.CloseGraphTab(tab);
         if (this.graphTabs.Contains(tab) || this.ActiveGraphTab != keep)
         {
            return;   // canceled
         }
      }
   }

   /// <summary>
   /// Asks whether to save a background tab's unsaved changes and saves them. Returns False when canceled
   /// (or the save failed).
   /// </summary>
   private bool AskToSaveBackgroundTab(GraphTab tab, bool allowCancel)
   {
      var message = string.Format(
         "The graph has been modified, would you like to save the graph and generate source files?\n\n\t{0}\n",
         GraphTabName(tab));

      int result = allowCancel
         ? EditorUtility.DisplayDialogComplex("Save uScript Graph?", message, "Yes", "No", "Cancel")
         : (EditorUtility.DisplayDialog("Save uScript Graph?", message, "Yes", "No") ? 0 : 1);

      if (result == 2)
      {
         return false;
      }

      if (result == 1)
      {
         return true;
      }

      return this.SaveBackgroundTab(tab);
   }

   private bool SaveBackgroundTab(GraphTab tab)
   {
      if (SaveDenied())
      {
         return false;
      }

      try
      {
         var scriptEditor = new ScriptEditor(string.Empty, PopulateEntityTypes(null), PopulateLogicTypes());
         scriptEditor.OpenFromBase64(null, tab.ScriptName, tab.Script, true);

         var quick = Preferences.SaveMethod == Preferences.SaveMethodType.Quick;
         var debug = Preferences.SaveMethod == Preferences.SaveMethodType.Debug;

         bool saved;
         AssetDatabase.StartAssetEditing();
         try
         {
            saved = this.SaveGraph(scriptEditor, tab.Path, !quick, debug, false);
         }
         finally
         {
            AssetDatabase.StopAssetEditing();
         }

         if (saved)
         {
            tab.Dirty = false;
            tab.Script = null;
            this.RefreshAssetDatabase(quick, debug);
         }

         return saved;
      }
      catch (Exception e)
      {
         uScriptDebug.Log("Could not save " + GraphTabName(tab) + ": " + e.Message, uScriptDebug.Type.Error);
         return false;
      }
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Hooks used by other uScript code
   // ---------------------------------------------------------------------------------------------------------------

   /// <summary>A graph file was renamed: point its tab at the new file.</summary>
   private void RenameGraphTab(string oldPath, string newPath)
   {
      var index = this.FindGraphTab(oldPath);
      if (index >= 0)
      {
         this.graphTabs[index].Path = newPath;
         this.SaveGraphTabList();
      }
   }

   /// <summary>Background tabs with unsaved changes whose graph has one of the given names.</summary>
   private List<string> UnsavedBackgroundTabs(IEnumerable<string> graphNames)
   {
      var names = new HashSet<string>(graphNames, StringComparer.OrdinalIgnoreCase);
      var result = new List<string>();

      for (var i = 0; i < this.graphTabs.Count; i++)
      {
         var tab = this.graphTabs[i];
         if (i != this.activeGraphTab && tab.Dirty && tab.Script != null && names.Contains(GraphTabName(tab)))
         {
            result.Add(GraphTabName(tab));
         }
      }

      return result;
   }

   /// <summary>A graph file is being deleted: drop its tab without asking to save.</summary>
   private void RemoveGraphTab(string path)
   {
      var index = this.FindGraphTab(path);
      if (index < 0 || index == this.activeGraphTab)
      {
         return;
      }

      this.graphTabs.RemoveAt(index);
      if (index < this.activeGraphTab)
      {
         this.activeGraphTab--;
      }

      this.SaveGraphTabList();
   }

   /// <summary>The open graph is being deleted: show another tab (or a new graph) without asking to save it.</summary>
   private void CloseDeletedOpenGraph()
   {
      var index = this.activeGraphTab;
      if (m_ScriptEditorCtrl != null)
      {
         m_ScriptEditorCtrl.IsDirty = false;
      }

      if (index >= 0 && index < this.graphTabs.Count)
      {
         this.graphTabs.RemoveAt(index);
      }

      this.activeGraphTab = -1;

      if (this.graphTabs.Count == 0)
      {
         this.CreateNewGraph();
      }
      else
      {
         this.ShowGraphTabAfterClose(Math.Min(Math.Max(index, 0), this.graphTabs.Count - 1));
      }

      this.SaveGraphTabList();
   }

   /// <summary>Asks to save background tabs with unsaved changes (uScript window closing).</summary>
   private void SaveBackgroundTabsOnClose()
   {
      if (this.graphTabs == null)
      {
         return;
      }

      for (var i = 0; i < this.graphTabs.Count; i++)
      {
         var tab = this.graphTabs[i];
         if (i != this.activeGraphTab && tab.Dirty && tab.Script != null && !string.IsNullOrEmpty(tab.Path))
         {
            this.AskToSaveBackgroundTab(tab, false);
         }
      }
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Drawing
   // ---------------------------------------------------------------------------------------------------------------

   private static GUIStyle graphTabStyle;
   private static GUIStyle graphTabCloseStyle;

   private void DrawGraphTabs()
   {
      var e = Event.current;

      if (e.type == EventType.Layout)
      {
         this.SyncGraphTabs();
      }

      if (graphTabStyle == null)
      {
         graphTabStyle = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 2, 0, 0) };
         graphTabCloseStyle = new GUIStyle(EditorStyles.toolbarButton) { padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleCenter };
      }

      EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
      {
         var tabs = this.graphTabs ?? new List<GraphTab>();
         for (var i = 0; i < tabs.Count; i++)
         {
            var tab = tabs[i];
            var isActive = i == this.activeGraphTab;
            var isDirty = isActive ? (m_ScriptEditorCtrl != null && m_ScriptEditorCtrl.IsDirty) : tab.Dirty;

            var content = new GUIContent(
               GraphTabName(tab) + (isDirty ? " *" : string.Empty),
               string.IsNullOrEmpty(tab.Path) ? "Not saved yet" : uScriptConfig.ConstantPaths.RelativePath(tab.Path));

            var pressed = GUILayout.Toggle(isActive, content, graphTabStyle, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(200));
            var tabRect = GUILayoutUtility.GetLastRect();

            var closePressed = GUILayout.Button(new GUIContent("×", "Close tab (middle-click a tab also closes it)"), graphTabCloseStyle, GUILayout.Width(16));

            var tabToUse = tab;

            if (e.type == EventType.MouseDown && e.button == 2 && tabRect.Contains(e.mousePosition))
            {
               closePressed = true;
               e.Use();
            }
            else if (e.type == EventType.ContextClick && tabRect.Contains(e.mousePosition))
            {
               var menu = new GenericMenu();
               menu.AddItem(new GUIContent("Close"), false, () => this.CloseGraphTab(tabToUse));
               menu.AddItem(new GUIContent("Close Other Tabs"), false, () => this.CloseOtherGraphTabs(tabToUse));
               if (!string.IsNullOrEmpty(tab.Path))
               {
                  menu.AddItem(new GUIContent("Show in Project"), false, () => uScriptGUI.PingProjectGraph(tabToUse.Path));
               }

               menu.ShowAsContext();
               e.Use();
            }

            if (closePressed)
            {
               EditorApplication.delayCall += () => this.CloseGraphTab(tabToUse);
            }
            else if (pressed && !isActive)
            {
               EditorApplication.delayCall += () => this.ActivateGraphTab(tabToUse);
            }
         }

         GUILayout.FlexibleSpace();

         if (GUILayout.Button(new GUIContent("+", "New graph in a new tab"), EditorStyles.toolbarButton, GUILayout.Width(22)))
         {
            EditorApplication.delayCall += () =>
            {
               FileMenuItem_New();
               this.RefreshAfterTabChange();
            };
         }
      }

      EditorGUILayout.EndHorizontal();
   }
}
