// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptGUIPanelVariables.cs" company="Detox Studios LLC">
//   Copyright 2010-2015 Detox Studios LLC. All rights reserved.
// </copyright>
// <summary>
//   The Variables panel: one row per variable of the open graph (like a blackboard), with editing.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Detox.Editor;
using Detox.Editor.GUI;
using Detox.ScriptEditor;

using UnityEditor;

using UnityEngine;

public sealed class uScriptGUIPanelVariables : uScriptGUIPanel
{
   /// <summary>DragAndDrop generic data key used when a variable is dragged onto the canvas.</summary>
   public const string DragDataKey = "uScriptVariable";

   /// <summary>The panel names shown in the Toolbox / Contents / Variables dropdown.</summary>
   public static readonly string[] PaletteModes = { "Toolbox", "Contents", "Variables" };

   public const int PaletteMode = 2;

   private const string RenameControlName = "uScriptVariableName";
   private const float RowHeight = 20;

   private static readonly string[] CommonTypes =
   {
      typeof(bool).ToString(),
      typeof(int).ToString(),
      typeof(float).ToString(),
      typeof(string).ToString(),
      typeof(Vector2).ToString(),
      typeof(Vector3).ToString(),
      typeof(Vector4).ToString(),
      typeof(Quaternion).ToString(),
      typeof(Color).ToString(),
      typeof(Rect).ToString(),
      typeof(GameObject).ToString()
   };

   private string filterText = string.Empty;
   private string selectedKey;
   private int cycleIndex;
   private bool focusNameField;

   private bool foldoutExposed = true;
   private bool foldoutGraph = true;
   private bool foldoutUnnamed = true;

   private Variable dragCandidate;
   private Vector2 dragStartPosition;

   private GUIStyle rowNameStyle;
   private GUIStyle rowNameSelectedStyle;
   private GUIStyle rowDetailStyle;
   private GUIStyle exposedStyle;

   static uScriptGUIPanelVariables()
   {
      Instance = new uScriptGUIPanelVariables();
   }

   private uScriptGUIPanelVariables()
   {
      this.Name = "Variables";
      InUScriptPanel = true;
   }

   public static uScriptGUIPanelVariables Instance { get; private set; }

   public bool FocusSearchBox { get; set; }

   public override bool InUScriptPanel
   {
      set
      {
         _inUScriptPanel = value;
         if (uScriptGUIPanelToolbox.Instance != null && uScriptGUIPanelToolbox.Instance.InUScriptPanel != value)
         {
            uScriptGUIPanelToolbox.Instance.InUScriptPanel = value;
         }
      }
   }

   /// <summary>Draws the Toolbox / Contents / Variables dropdown used in the toolbar of all three panels.</summary>
   public static void DrawPaletteModePopup()
   {
      var size = uScriptGUIStyle.PanelTitleDropDown.CalcSize(new GUIContent(PaletteModes[1]));
      var index = EditorGUILayout.Popup(uScript._paletteMode, PaletteModes, uScriptGUIStyle.PanelTitleDropDown, GUILayout.Width(size.x));
      if (uScript._paletteMode != index)
      {
         uScript.SetPaletteMode(index);
      }
   }

   public override void Draw()
   {
      var uScriptInstance = uScript.WeakInstance;

      if (uScriptInstance == null && !InUScriptPanel)
      {
         EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
         {
            this.DrawOrphanNotification();
         }
         EditorGUILayout.EndVertical();
         return;
      }

      this.InitStyles();

      if (InUScriptPanel && !uScriptInstance.IsOnlyBottomPanelVisible(GetType().ToString()))
      {
         uScript.Instance.paletteRect = EditorGUILayout.BeginVertical(GUILayout.Width(uScriptGUI.PanelLeftWidth));
      }
      else
      {
         uScript.Instance.paletteRect = EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
      }
      {
         uScript.Instance.paletteRect = EditorGUILayout.BeginVertical(uScriptGUIStyle.PanelBox);
         {
            this.DrawToolbar();

            if (uScriptInstance.wasCanvasDragged && Preferences.DrawPanelsOnUpdate == false)
            {
               this.DrawHiddenNotification();
            }
            else
            {
               this.DrawList();
            }
         }
         EditorGUILayout.EndVertical();
      }
      EditorGUILayout.EndVertical();

      if ((int)uScript.Instance.paletteRect.width != 0 && (int)uScript.Instance.paletteRect.width != uScriptGUI.PanelLeftWidth)
      {
         uScriptGUI.PanelLeftWidth = (int)uScript.Instance.paletteRect.width;
      }

      if (InUScriptPanel)
      {
         uScriptInstance.SetMouseRegion(uScript.MouseRegion.Palette);
      }
   }

   // === Data ======================================================================================================

   private static ScriptEditorCtrl Ctrl
   {
      get { return uScript.Instance != null ? uScript.Instance.ScriptEditorCtrl : null; }
   }

   private static List<Variable> CollectVariables()
   {
      var result = new List<Variable>();
      var ctrl = Ctrl;
      if (ctrl == null || ctrl.ScriptEditor == null)
      {
         return result;
      }

      var named = new Dictionary<string, Variable>();

      foreach (var local in ctrl.VariableNodes)
      {
         var name = local.Name.Default ?? string.Empty;

         if (string.IsNullOrEmpty(name))
         {
            // Unnamed variables are separate variables, one per node
            result.Add(new Variable(local, "#" + local.Guid));
            continue;
         }

         // Variables with the same name AND type are one shared variable
         var key = name + "\n" + local.Value.Type;
         Variable variable;
         if (named.TryGetValue(key, out variable))
         {
            variable.Add(local);
         }
         else
         {
            variable = new Variable(local, key);
            named.Add(key, variable);
            result.Add(variable);
         }
      }

      // Same name, different type: these are NOT the same variable, which is easy to miss
      foreach (var group in named.Values.GroupBy(v => v.Name).Where(g => g.Count() > 1))
      {
         foreach (var variable in group)
         {
            variable.TypeConflict = true;
         }
      }

      return result;
   }

   private bool MatchesFilter(Variable variable)
   {
      if (string.IsNullOrEmpty(this.filterText))
      {
         return true;
      }

      var haystack = (variable.Name + " " + variable.FriendlyType + " " + variable.Value).ToLowerInvariant();
      return this.filterText.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).All(haystack.Contains);
   }

   // === Drawing ===================================================================================================

   private void InitStyles()
   {
      if (this.rowNameStyle != null)
      {
         return;
      }

      this.rowNameStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
      this.rowNameSelectedStyle = new GUIStyle(this.rowNameStyle) { fontStyle = FontStyle.Bold };
      this.rowDetailStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleRight };
      this.rowDetailStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.38f, 0.38f, 0.38f);
      this.exposedStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
   }

   private void DrawToolbar()
   {
      EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));
      {
         DrawPaletteModePopup();

         GUILayout.FlexibleSpace();

         if (GUILayout.Button(Content.ButtonAdd, EditorStyles.toolbarDropDown, GUILayout.ExpandWidth(false)))
         {
            this.ShowAddVariableMenu();
         }

         if (this.FocusSearchBox)
         {
            this.FocusSearchBox = false;
            EditorGUI.FocusTextInControl("VariablesFilterSearch");
         }

         GUI.SetNextControlName("VariablesFilterSearch");
         var newFilter = uScriptGUI.ToolbarSearchField(this.filterText, GUILayout.MinWidth(50), GUILayout.MaxWidth(100));
         if (newFilter != this.filterText)
         {
            if (newFilter.Contains("\n"))
            {
               FocusedControl.Clear();
            }

            this.filterText = newFilter.Replace("\n", string.Empty).TrimStart();
         }

         if (InUScriptPanel)
         {
            if (GUILayout.Button(Content.ButtonPopout, EditorStyles.toolbarButton, GUILayout.Width(EditorStyles.toolbarButton.CalcSize(Content.ButtonPopout).x)))
            {
               if (uScript.GetUScriptGUIPanelWindow<uScriptGUIPanelVariables>() == null)
               {
                  uScript.OpenPopOutWindow(this);
               }

               uScript.Instance.CommandCanvasShowPalettePanel();
            }

            if (GUILayout.Button(Content.ButtonClose, EditorStyles.toolbarButton, GUILayout.Width(EditorStyles.toolbarButton.CalcSize(Content.ButtonClose).x)))
            {
               uScript.Instance.CommandCanvasShowPalettePanel();
            }
         }
      }
      EditorGUILayout.EndHorizontal();
   }

   private void DrawList()
   {
      var variables = CollectVariables();
      var visible = variables.Where(this.MatchesFilter).ToList();

      var exposed = visible.Where(v => !v.IsUnnamed && v.Exposed).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
      var graph = visible.Where(v => !v.IsUnnamed && !v.Exposed).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
      var unnamed = visible.Where(v => v.IsUnnamed).OrderBy(v => v.FriendlyType, StringComparer.OrdinalIgnoreCase).ToList();

      // Forget the selection if that variable is gone (deleted, renamed, undo...)
      if (this.selectedKey != null && variables.All(v => v.Key != this.selectedKey))
      {
         this.selectedKey = null;
      }

      this.ScrollviewOffset = EditorGUILayout.BeginScrollView(
         this.ScrollviewOffset,
         false,
         false,
         uScriptGUIStyle.HorizontalScrollbar,
         uScriptGUIStyle.VerticalScrollbar,
         "scrollview",
         GUILayout.ExpandWidth(true));
      {
         if (variables.Count == 0)
         {
            GUILayout.Label(
               "This graph has no variables yet.\n\nClick + above to create one, or add a variable node from the Toolbox.",
               uScriptGUIStyle.PanelMessage);
         }
         else if (visible.Count == 0)
         {
            GUILayout.Label("The search found no matches!", uScriptGUIStyle.PanelMessageBold);
         }
         else
         {
            this.foldoutExposed = this.DrawSection("Exposed", exposed, this.foldoutExposed);
            this.foldoutGraph = this.DrawSection("Graph Variables", graph, this.foldoutGraph);
            this.foldoutUnnamed = this.DrawSection("Unnamed", unnamed, this.foldoutUnnamed);
         }

         GUILayout.Space(6);
      }
      EditorGUILayout.EndScrollView();

      // Releasing the mouse anywhere ends a possible drag
      if (Event.current.type == EventType.MouseUp)
      {
         this.dragCandidate = null;
      }
   }

   private bool DrawSection(string title, List<Variable> variables, bool expanded)
   {
      if (variables.Count == 0)
      {
         return expanded;
      }

      expanded = GUILayout.Toggle(expanded, string.Format("{0} ({1})", title, variables.Count), uScriptGUIStyle.PaletteFoldout);

      if (expanded)
      {
         foreach (var variable in variables)
         {
            this.DrawRow(variable);

            if (variable.Key == this.selectedKey)
            {
               this.DrawDetails(variable);
            }
         }
      }

      return expanded;
   }

   private void DrawRow(Variable variable)
   {
      var e = Event.current;
      var rect = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
      var isSelected = variable.Key == this.selectedKey;

      if (e.type == EventType.Repaint)
      {
         if (isSelected)
         {
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f, 0.9f) : new Color(0.24f, 0.49f, 0.91f, 0.35f));
         }
         else if (rect.Contains(e.mousePosition))
         {
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.12f));
         }
      }

      // Exposed marker
      var markerRect = new Rect(rect.x + 4, rect.y, 14, rect.height);
      var previousColor = GUI.contentColor;
      GUI.contentColor = variable.Exposed ? new Color(0.45f, 0.85f, 0.45f) : new Color(0.5f, 0.5f, 0.5f);
      GUI.Label(markerRect, new GUIContent(variable.Exposed ? "●" : "○", variable.Exposed ? "Exposed to Unity (shown in the Inspector)" : "Not exposed"), this.exposedStyle);
      GUI.contentColor = previousColor;

      // Count and type, drawn from the right
      var countRect = new Rect(rect.xMax - 30, rect.y, 28, rect.height);
      var typeWidth = Mathf.Min(110, rect.width * 0.38f);
      var typeRect = new Rect(countRect.x - typeWidth, rect.y, typeWidth, rect.height);

      GUI.Label(countRect, new GUIContent("×" + variable.Guids.Count, "Number of nodes using this variable"), this.rowDetailStyle);
      GUI.Label(typeRect, new GUIContent(variable.FriendlyType, variable.Type), this.rowDetailStyle);

      // Name (and warning icon)
      var nameRect = new Rect(markerRect.xMax + 2, rect.y, typeRect.x - markerRect.xMax - 4, rect.height);
      if (variable.TypeConflict)
      {
         var iconRect = new Rect(nameRect.x, rect.y + 2, 16, 16);
         GUI.Label(iconRect, Content.WarningIcon(string.Format(
            "Another variable called \"{0}\" has a different type. Variables only share their value when the name AND type match.",
            variable.Name)));
         nameRect.xMin += 18;
      }

      var label = variable.IsUnnamed
         ? new GUIContent("(unnamed)  " + Shorten(variable.Value, 24), "Variables without a name are separate variables, one per node.")
         : new GUIContent(variable.Name, string.IsNullOrEmpty(variable.Value) ? variable.Name : variable.Name + " = " + variable.Value);
      GUI.Label(nameRect, label, isSelected ? this.rowNameSelectedStyle : this.rowNameStyle);

      // Input
      if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
      {
         FocusedControl.Clear();
         this.OnRowClicked(variable, e.shift, e.clickCount >= 2);
         this.dragCandidate = variable;
         this.dragStartPosition = e.mousePosition;
         e.Use();
      }
      else if (e.type == EventType.MouseDrag && this.dragCandidate != null && this.dragCandidate.Key == variable.Key
               && (e.mousePosition - this.dragStartPosition).magnitude > 6)
      {
         this.StartDrag(variable);
         this.dragCandidate = null;
         e.Use();
      }
      else if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
      {
         this.selectedKey = variable.Key;
         this.ShowRowContextMenu(variable);
         e.Use();
      }
   }

   private void DrawDetails(Variable variable)
   {
      var previousLabelWidth = EditorGUIUtility.labelWidth;
      EditorGUIUtility.labelWidth = 52;

      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      {
         // Name
         GUI.SetNextControlName(RenameControlName);
         var newName = EditorGUILayout.DelayedTextField(new GUIContent("Name", "Press Enter to rename every node of this variable."), variable.Name);
         if (this.focusNameField)
         {
            EditorGUI.FocusTextInControl(RenameControlName);
            this.focusNameField = false;
         }

         if (newName != variable.Name)
         {
            this.Rename(variable, newName);
            GUIUtility.ExitGUI();
         }

         // Value
         this.DrawValueField(variable);

         // Expose to Unity
         EditorGUI.BeginDisabledGroup(variable.IsUnnamed);
         {
            var exposed = EditorGUILayout.Toggle(new GUIContent("Exposed", "Expose to Unity: show the variable in the Inspector of the graph's component. Requires a name."), variable.Exposed);
            if (exposed != variable.Exposed)
            {
               SetExposed(variable, exposed);
               GUIUtility.ExitGUI();
            }

            if (variable.Exposed)
            {
               var hidden = EditorGUILayout.Toggle(new GUIContent("Hidden", "Hide in Inspector: keep it public for other graphs/scripts but hide it in the Inspector."), variable.HiddenInInspector);
               if (hidden != variable.HiddenInInspector)
               {
                  SetHiddenInInspector(variable, hidden);
                  GUIUtility.ExitGUI();
               }
            }
         }
         EditorGUI.EndDisabledGroup();

         if (variable.TypeConflict)
         {
            EditorGUILayout.HelpBox(
               "Another variable has the same name but a different type, so they do not share their value. Rename one of them.",
               MessageType.Warning);
         }

         EditorGUILayout.BeginHorizontal();
         {
            if (GUILayout.Button(new GUIContent("Select", "Select all nodes of this variable on the canvas"), EditorStyles.miniButtonLeft))
            {
               SelectNodes(variable, false);
            }

            if (GUILayout.Button(new GUIContent("Add Node", "Place another node of this variable in the middle of the canvas (or drag the variable onto the canvas)"), EditorStyles.miniButtonRight))
            {
               AddNodeAtCanvasCenter(variable);
               GUIUtility.ExitGUI();
            }
         }
         EditorGUILayout.EndHorizontal();
      }
      EditorGUILayout.EndVertical();

      EditorGUIUtility.labelWidth = previousLabelWidth;
   }

   private void DrawValueField(Variable variable)
   {
      var label = new GUIContent("Value");
      var parameter = variable.First.Value;

      if (parameter.Type.Contains("[]"))
      {
         EditorGUILayout.LabelField(label, new GUIContent("List - edit it in the Properties panel"));
         return;
      }

      object value;
      try
      {
         value = parameter.DefaultAsObject;
      }
      catch (Exception)
      {
         value = parameter.Default;
      }

      object newValue = value;

      if (value is bool)
      {
         newValue = EditorGUILayout.Toggle(label, (bool)value);
      }
      else if (value is int)
      {
         newValue = EditorGUILayout.DelayedIntField(label, (int)value);
      }
      else if (value is float)
      {
         newValue = EditorGUILayout.DelayedFloatField(label, (float)value);
      }
      else if (value is Vector2)
      {
         newValue = EditorGUILayout.Vector2Field(label, (Vector2)value);
      }
      else if (value is Vector3)
      {
         newValue = EditorGUILayout.Vector3Field(label, (Vector3)value);
      }
      else if (value is Vector4)
      {
         newValue = EditorGUILayout.Vector4Field(label, (Vector4)value);
      }
      else if (value is Quaternion)
      {
         var q = (Quaternion)value;
         var v = EditorGUILayout.Vector4Field(label, new Vector4(q.x, q.y, q.z, q.w));
         newValue = new Quaternion(v.x, v.y, v.z, v.w);
      }
      else if (value is Rect)
      {
         newValue = EditorGUILayout.RectField(label, (Rect)value);
      }
      else if (value is Color)
      {
         newValue = EditorGUILayout.ColorField(label, (Color)value);
      }
      else if (value is Enum)
      {
         newValue = EditorGUILayout.EnumPopup(label, (Enum)value);
      }
      else
      {
         // Strings, and object references (GameObject, Transform...) which uScript stores by name
         var isReference = parameter.Type != typeof(string).ToString();
         var text = EditorGUILayout.DelayedTextField(
            new GUIContent(isReference ? "Name" : "Value", isReference ? "The name of the object in the scene (press Enter to apply)" : "Press Enter to apply"),
            parameter.Default ?? string.Empty);
         if (text != (parameter.Default ?? string.Empty))
         {
            SetValueString(variable, text);
            GUIUtility.ExitGUI();
         }

         return;
      }

      if (!Equals(newValue, value))
      {
         var updated = parameter;
         updated.DefaultAsObject = newValue;
         SetValueString(variable, updated.Default);
         GUIUtility.ExitGUI();
      }
   }

   // === Actions ===================================================================================================

   private void OnRowClicked(Variable variable, bool shift, bool doubleClick)
   {
      if (doubleClick)
      {
         this.selectedKey = variable.Key;
         this.focusNameField = true;
         return;
      }

      if (this.selectedKey == variable.Key && !shift)
      {
         // Clicking the selected variable again jumps to its next node
         this.cycleIndex++;
         CenterOnNode(variable, this.cycleIndex);
         return;
      }

      this.selectedKey = variable.Key;
      this.cycleIndex = 0;
      SelectNodes(variable, shift);
      CenterOnNode(variable, 0);
   }

   private static void SelectNodes(Variable variable, bool addToSelection)
   {
      var ctrl = Ctrl;
      if (ctrl == null)
      {
         return;
      }

      ctrl.SelectNodes(variable.Guids, addToSelection);
      uScript.RequestRepaint(2);
   }

   private static void CenterOnNode(Variable variable, int index)
   {
      var ctrl = Ctrl;
      if (ctrl == null || variable.Guids.Count == 0)
      {
         return;
      }

      var node = ctrl.GetNode(variable.Guids[index % variable.Guids.Count]);
      if (node != null)
      {
         ctrl.CenterOnNode(node);
         uScript.RequestRepaint(2);
      }
   }

   private void Rename(Variable variable, string requestedName)
   {
      var ctrl = Ctrl;
      if (ctrl == null)
      {
         return;
      }

      var newName = (requestedName ?? string.Empty).Trim();
      if (newName == variable.Name)
      {
         return;
      }

      if (string.IsNullOrEmpty(newName))
      {
         EditorUtility.DisplayDialog("Rename Variable", "A variable needs a name. To make the nodes separate unnamed variables, clear the name in the Properties panel.", "OK");
         return;
      }

      var others = ctrl.VariableNodes.Where(local => local.Name.Default == newName && !variable.Guids.Contains(local.Guid)).ToList();

      if (others.Any(local => local.Value.Type != variable.Type))
      {
         EditorUtility.DisplayDialog(
            "Rename Variable",
            string.Format("A variable called \"{0}\" already exists with a different type. Variables that share a name must have the same type.", newName),
            "OK");
         return;
      }

      LocalNode? mergeInto = null;
      if (others.Count > 0)
      {
         var merge = EditorUtility.DisplayDialog(
            "Rename Variable",
            string.Format(
               "A variable called \"{0}\" already exists. Variables with the same name and type are the same variable, so the two will be merged and use the existing variable's value and settings.",
               newName),
            "Merge",
            "Cancel");

         if (!merge)
         {
            return;
         }

         mergeInto = others[0];
      }

      ctrl.ModifyVariableNodes(
         variable.Guids,
         local =>
         {
            if (mergeInto.HasValue)
            {
               local.Value = mergeInto.Value.Value;
               local.Externaled = mergeInto.Value.Externaled;
               local.HideInInspector = mergeInto.Value.HideInInspector;
            }

            var name = local.Name;
            name.Default = newName;
            local.Name = name;
            return local;
         },
         "Rename Variable");

      this.selectedKey = newName + "\n" + variable.Type;
      uScript.RequestRepaint(2);
   }

   private static void SetValueString(Variable variable, string newDefault)
   {
      var ctrl = Ctrl;
      if (ctrl == null)
      {
         return;
      }

      ctrl.ModifyVariableNodes(
         variable.Guids,
         local =>
         {
            var value = local.Value;
            value.Default = newDefault;
            local.Value = value;
            return local;
         },
         "Variable Value Changed");

      uScript.RequestRepaint(2);
   }

   private static void SetExposed(Variable variable, bool exposed)
   {
      var ctrl = Ctrl;
      if (ctrl == null || variable.IsUnnamed)
      {
         return;
      }

      ctrl.ModifyVariableNodes(
         variable.Guids,
         local =>
         {
            var externaled = local.Externaled;
            externaled.Default = exposed ? "true" : "false";
            local.Externaled = externaled;

            if (!exposed)
            {
               var hidden = local.HideInInspector;
               hidden.Default = "false";
               local.HideInInspector = hidden;
            }

            return local;
         },
         exposed ? "Expose Variable" : "Unexpose Variable");

      uScript.RequestRepaint(2);
   }

   private static void SetHiddenInInspector(Variable variable, bool hidden)
   {
      var ctrl = Ctrl;
      if (ctrl == null)
      {
         return;
      }

      ctrl.ModifyVariableNodes(
         variable.Guids,
         local =>
         {
            var hide = local.HideInInspector;
            hide.Default = hidden ? "true" : "false";
            local.HideInInspector = hide;
            return local;
         },
         "Hide Variable In Inspector");

      uScript.RequestRepaint(2);
   }

   private static void AddNodeAtCanvasCenter(Variable variable)
   {
      var ctrl = Ctrl;
      if (ctrl == null)
      {
         return;
      }

      var center = new Detox.Drawing.Point(
         (int)(uScript.Instance.NodeWindowRect.width * 0.5f) - 28,
         (int)(uScript.Instance.NodeWindowRect.height * 0.5f) - 28);

      ctrl.AddVariableNodeAt(variable.CreateNode(), center);
      uScript.RequestRepaint(2);
   }

   /// <summary>Called by the canvas when a variable is dropped on it.</summary>
   public static bool DropVariableOnCanvas(object dragData, Detox.Drawing.Point canvasPosition)
   {
      var data = dragData as DragData;
      var ctrl = Ctrl;
      if (data == null || ctrl == null)
      {
         return false;
      }

      var node = new LocalNode(data.Name, data.Type, data.DefaultValue);
      if (data.Exposed)
      {
         var externaled = node.Externaled;
         externaled.Default = "true";
         node.Externaled = externaled;
      }

      ctrl.AddVariableNodeAt(node, new Detox.Drawing.Point(canvasPosition.X - 28, canvasPosition.Y - 28));
      uScript.RequestRepaint(2);
      return true;
   }

   private void StartDrag(Variable variable)
   {
      DragAndDrop.PrepareStartDrag();
      DragAndDrop.objectReferences = new UnityEngine.Object[0];
      DragAndDrop.SetGenericData(DragDataKey, new DragData
      {
         Name = variable.Name,
         Type = variable.Type,
         DefaultValue = variable.First.Value.Default,
         Exposed = variable.Exposed
      });
      DragAndDrop.StartDrag(variable.IsUnnamed ? variable.FriendlyType : variable.Name);
   }

   private void ShowRowContextMenu(Variable variable)
   {
      var menu = new GenericMenu();
      menu.AddItem(new GUIContent("Select Nodes"), false, () => SelectNodes(variable, false));
      menu.AddItem(new GUIContent("Add Node to Canvas"), false, () => AddNodeAtCanvasCenter(variable));
      menu.AddSeparator(string.Empty);

      menu.AddItem(new GUIContent("Rename..."), false, () =>
      {
         this.selectedKey = variable.Key;
         this.focusNameField = true;
         uScript.RequestRepaint(2);
      });

      if (variable.IsUnnamed)
      {
         menu.AddDisabledItem(new GUIContent("Exposed"));
      }
      else
      {
         menu.AddItem(new GUIContent("Exposed"), variable.Exposed, () => SetExposed(variable, !variable.Exposed));
      }

      menu.ShowAsContext();
   }

   private void ShowAddVariableMenu()
   {
      var ctrl = Ctrl;
      if (ctrl == null)
      {
         return;
      }

      var menu = new GenericMenu();

      foreach (var type in CommonTypes)
      {
         var t = type;
         menu.AddItem(new GUIContent(FriendlyTypeName(t)), false, () => this.CreateVariable(t));
      }

      menu.AddSeparator(string.Empty);

      var others = (ctrl.VariableTypes ?? new string[0])
         .Where(t => !CommonTypes.Contains(t) && IsVariableType(t))
         .OrderBy(FriendlyTypeName, StringComparer.OrdinalIgnoreCase);

      foreach (var type in others)
      {
         var t = type;
         var category = uScriptConfig.Variable.Category(t);
         var path = "More Types/" + (string.IsNullOrEmpty(category) ? string.Empty : category.Replace("Variables/", string.Empty).Trim('/') + "/") + FriendlyTypeName(t);
         menu.AddItem(new GUIContent(path.Replace("//", "/")), false, () => this.CreateVariable(t));
      }

      menu.ShowAsContext();
   }

   private void CreateVariable(string type)
   {
      var ctrl = Ctrl;
      if (ctrl == null)
      {
         return;
      }

      // Pick an unused name like "newFloat", "newFloat2"...
      var baseName = "new" + new string(FriendlyTypeName(type).Where(char.IsLetterOrDigit).ToArray());
      var existing = new HashSet<string>(ctrl.VariableNodes.Select(local => local.Name.Default));
      var name = baseName;
      for (var i = 2; existing.Contains(name); i++)
      {
         name = baseName + i;
      }

      var center = new Detox.Drawing.Point(
         (int)(uScript.Instance.NodeWindowRect.width * 0.5f) - 28,
         (int)(uScript.Instance.NodeWindowRect.height * 0.5f) - 28);

      ctrl.AddVariableNodeAt(new LocalNode(name, type, string.Empty), center);

      // Select it and let the user type the real name straight away
      this.selectedKey = name + "\n" + type;
      this.focusNameField = true;
      this.foldoutGraph = true;
      this.filterText = string.Empty;
      uScript.RequestRepaint(2);
   }

   // === Helpers ===================================================================================================

   private static bool IsVariableType(string type)
   {
      var t = uScript.Instance != null ? uScript.Instance.GetType(type) : null;
      return t == null || !(typeof(uScriptLogic).IsAssignableFrom(t) || typeof(uScriptEvent).IsAssignableFrom(t));
   }

   private static string FriendlyTypeName(string type)
   {
      var name = uScriptConfig.Variable.FriendlyName(type) ?? type;
      return name.Replace("UnityEngine.", string.Empty);
   }

   private static string Shorten(string text, int length)
   {
      if (string.IsNullOrEmpty(text))
      {
         return string.Empty;
      }

      text = text.Replace("\n", " ");
      return text.Length <= length ? text : text.Substring(0, length - 1) + "…";
   }

   /// <summary>One variable: all nodes with the same name and type (or a single unnamed node).</summary>
   private sealed class Variable
   {
      public Variable(LocalNode first, string key)
      {
         this.Key = key;
         this.First = first;
         this.Name = first.Name.Default ?? string.Empty;
         this.Type = first.Value.Type;
         this.FriendlyType = FriendlyTypeName(first.Value.Type);
         this.Value = first.Value.Default ?? string.Empty;
         this.Guids = new List<Guid>();
         this.Add(first);
      }

      public string Key { get; private set; }

      public string Name { get; private set; }

      public string Type { get; private set; }

      public string FriendlyType { get; private set; }

      public string Value { get; private set; }

      public LocalNode First { get; private set; }

      public List<Guid> Guids { get; private set; }

      public bool Exposed { get; private set; }

      public bool HiddenInInspector { get; private set; }

      public bool TypeConflict { get; set; }

      public bool IsUnnamed
      {
         get { return string.IsNullOrEmpty(this.Name); }
      }

      public void Add(LocalNode local)
      {
         this.Guids.Add(local.Guid);
         this.Exposed |= local.Externaled.Default == "true";
         this.HiddenInInspector |= local.HideInInspector.Default == "true";
      }

      public LocalNode CreateNode()
      {
         var node = new LocalNode(this.Name, this.Type, this.First.Value.Default);
         if (this.Exposed)
         {
            var externaled = node.Externaled;
            externaled.Default = "true";
            node.Externaled = externaled;
         }

         return node;
      }
   }

   private sealed class DragData
   {
      public string Name;
      public string Type;
      public string DefaultValue;
      public bool Exposed;
   }

   private static class Content
   {
      private static GUIContent warningIcon;

      static Content()
      {
         ButtonAdd = new GUIContent("+", "Create a new variable");

         ButtonPopout = new GUIContent
         {
            image = uScriptGUI.GetSkinnedTexture("iconPopout"),
            tooltip = "Open a standalone window with this panel's contents within it."
         };

         ButtonClose = new GUIContent
         {
            image = uScriptGUI.GetSkinnedTexture("iconMiniDelete"),
            tooltip = "Close this panel."
         };
      }

      public static GUIContent ButtonAdd { get; private set; }

      public static GUIContent ButtonPopout { get; private set; }

      public static GUIContent ButtonClose { get; private set; }

      public static GUIContent WarningIcon(string tooltip)
      {
         if (warningIcon == null)
         {
            warningIcon = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml"));
         }

         warningIcon.tooltip = tooltip;
         return warningIcon;
      }
   }
}
