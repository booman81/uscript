// --------------------------------------------------------------------------------------------------------------------
// <copyright file="QuickAddPopup.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Searchable "quick add" popup: press Space on the canvas, type, press Enter to place a node under the mouse.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Detox.Editor.GUI.Windows
{
   using System;
   using System.Collections.Generic;
   using System.Linq;

   using Detox.ScriptEditor;

   using UnityEditor;

   using UnityEngine;

   public sealed class QuickAddPopup : PopupWindowContent
   {
      private const string SearchControlName = "uScriptQuickAddSearch";
      private const float Width = 360;
      private const float RowHeight = 20;
      private const int MaxVisibleRows = 16;
      private const int MaxResults = 200;
      private const int MaxLinkResults = 2000;

      private static string lastSearch = string.Empty;

      private readonly List<Entry> allEntries;
      private readonly Detox.Drawing.Point canvasPosition;
      private readonly LinkDropInfo link;

      private List<Entry> results = new List<Entry>();
      private string search;
      private int selectedIndex;
      private Vector2 scroll;
      private bool focusSearch = true;
      private bool scrollToSelection;

      private static GUIStyle rowStyle;
      private static GUIStyle pathStyle;

      private QuickAddPopup(List<Entry> entries, Detox.Drawing.Point canvasPosition, LinkDropInfo link)
      {
         this.allEntries = entries;
         this.canvasPosition = canvasPosition;
         this.link = link;

         // When connecting a dropped link the list is already narrowed down, so start with an empty search
         this.search = link == null ? lastSearch : string.Empty;
         this.UpdateResults();
      }

      /// <summary>
      /// When connecting a value socket, variables of exactly that type come first, then other variables.
      /// </summary>
      private static int LinkPriority(LinkDropInfo link, EntityNode node)
      {
         if (link == null || string.IsNullOrEmpty(link.Type))
         {
            return 0;
         }

         if (node is LocalNode)
         {
            return ((LocalNode)node).Value.Type == link.Type ? 0 : 1;
         }

         return 2;
      }

      private int ResultLimit
      {
         get { return this.link == null ? MaxResults : MaxLinkResults; }
      }

      /// <summary>Opens the popup at the given GUI position of the uScript window.</summary>
      /// <param name="guiPosition">Where to open the popup (uScript window GUI coordinates).</param>
      /// <param name="canvasPosition">Where the node should be placed (canvas coordinates).</param>
      public static void Show(Vector2 guiPosition, Detox.Drawing.Point canvasPosition)
      {
         Show(guiPosition, canvasPosition, null);
      }

      /// <summary>
      /// Opens the popup. With <paramref name="link"/> set, only nodes that can connect to that socket are listed
      /// and the chosen node is connected to it.
      /// </summary>
      public static void Show(Vector2 guiPosition, Detox.Drawing.Point canvasPosition, LinkDropInfo link)
      {
         var toolbox = uScriptGUIPanelToolbox.Instance;
         if (toolbox == null || uScript.Instance == null || uScript.Instance.ScriptEditorCtrl == null)
         {
            return;
         }

         var entries = new List<Entry>();
         var ctrl = uScript.Instance.ScriptEditorCtrl;

         // Checking every node against the dropped socket: build the graph's link list only once
         if (link != null)
         {
            ctrl.BeginAutoLinkChecks();
         }

         try
         {
            foreach (var item in toolbox.GetAllNodeMenuItems())
            {
               var node = item.Tag as EntityNode;
               if (node == null)
               {
                  continue;
               }

               if (link != null && !ctrl.CanAutoLink(link, node))
               {
                  continue;
               }

               var path = item.Path ?? item.Name;
               var slash = path.LastIndexOf('/');
               var category = slash > 0 ? path.Substring(0, slash) : string.Empty;

               entries.Add(new Entry
               {
                  Name = item.Name,
                  Category = category,
                  Tooltip = item.Tooltip,
                  Signature = uScript.GetNodeSignature(node),
                  NameLower = item.Name.ToLowerInvariant(),
                  PathLower = path.ToLowerInvariant(),
                  Priority = LinkPriority(link, node)
               });
            }
         }
         finally
         {
            if (link != null)
            {
               ctrl.EndAutoLinkChecks();
            }
         }

         if (entries.Count == 0 && link != null)
         {
            uScriptDebug.Log("No nodes can be connected to the socket '" + link.Anchor + "'.", uScriptDebug.Type.Message);
            return;
         }

         if (entries.Count == 0)
         {
            uScriptDebug.Log("Quick Add: no nodes are available yet. Open or create a graph first.", uScriptDebug.Type.Warning);
            return;
         }

         PopupWindow.Show(new Rect(guiPosition.x, guiPosition.y, 1, 1), new QuickAddPopup(entries, canvasPosition, link));
      }

      public override Vector2 GetWindowSize()
      {
         return new Vector2(Width, 24 + 18 + (RowHeight * MaxVisibleRows) + 4);
      }

      public override void OnGUI(Rect rect)
      {
         InitStyles();

         this.HandleKeyboard();

         // Search field
         GUI.SetNextControlName(SearchControlName);
         var searchRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, 18);
         var newSearch = SearchField(searchRect, this.search);
         if (newSearch != this.search)
         {
            this.search = newSearch;
            lastSearch = newSearch;
            this.UpdateResults();
         }

         if (this.focusSearch)
         {
            EditorGUI.FocusTextInControl(SearchControlName);
            this.focusSearch = false;
         }

         // Results header
         var infoRect = new Rect(rect.x + 6, searchRect.yMax + 2, rect.width - 12, 16);
         GUI.Label(
            infoRect,
            this.results.Count == 0
               ? (this.link != null ? "No connectable nodes match" : "No matching nodes")
               : string.Format((this.link != null ? "Connect to " + this.link.Anchor + ":  " : string.Empty) + "{0}{1} nodes  —  Enter to add, Esc to close", this.results.Count, this.results.Count >= this.ResultLimit ? "+" : string.Empty),
            EditorStyles.miniLabel);

         // Results list
         var listRect = new Rect(rect.x, infoRect.yMax + 2, rect.width, rect.height - (infoRect.yMax + 2 - rect.y));
         var contentRect = new Rect(0, 0, listRect.width - 16, this.results.Count * RowHeight);

         if (this.scrollToSelection)
         {
            var selectedTop = this.selectedIndex * RowHeight;
            if (selectedTop < this.scroll.y)
            {
               this.scroll.y = selectedTop;
            }
            else if (selectedTop + RowHeight > this.scroll.y + listRect.height)
            {
               this.scroll.y = selectedTop + RowHeight - listRect.height;
            }

            this.scrollToSelection = false;
         }

         this.scroll = GUI.BeginScrollView(listRect, this.scroll, contentRect);
         {
            var first = Mathf.Max(0, (int)(this.scroll.y / RowHeight));
            var last = Mathf.Min(this.results.Count, first + MaxVisibleRows + 2);

            for (var i = first; i < last; i++)
            {
               var entry = this.results[i];
               var rowRect = new Rect(0, i * RowHeight, contentRect.width, RowHeight);
               var isSelected = i == this.selectedIndex;

               if (Event.current.type == EventType.Repaint && isSelected)
               {
                  EditorGUI.DrawRect(rowRect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.24f, 0.49f, 0.91f));
               }

               var nameRect = new Rect(rowRect.x + 6, rowRect.y, rowRect.width * 0.55f, rowRect.height);
               var categoryRect = new Rect(nameRect.xMax, rowRect.y, rowRect.width - nameRect.width - 12, rowRect.height);

               GUI.Label(nameRect, new GUIContent(entry.Name, entry.Tooltip), rowStyle);
               GUI.Label(categoryRect, entry.Category, pathStyle);

               if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
               {
                  this.selectedIndex = i;
                  if (Event.current.clickCount >= 2 || Event.current.button == 0)
                  {
                     this.Choose();
                  }

                  Event.current.Use();
               }
            }
         }

         GUI.EndScrollView();

         if (Event.current.type == EventType.MouseMove)
         {
            this.editorWindow.Repaint();
         }
      }

      public override void OnOpen()
      {
         this.editorWindow.wantsMouseMove = true;
      }

      private static string SearchField(Rect rect, string text)
      {
#if UNITY_2019_1_OR_NEWER
         return EditorGUI.TextField(rect, text, EditorStyles.toolbarSearchField);
#else
         return EditorGUI.TextField(rect, text);
#endif
      }

      private static void InitStyles()
      {
         if (rowStyle != null)
         {
            return;
         }

         rowStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
         pathStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
         pathStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f);
      }

      private void HandleKeyboard()
      {
         var e = Event.current;
         if (e.type != EventType.KeyDown)
         {
            return;
         }

         switch (e.keyCode)
         {
            case KeyCode.DownArrow:
               this.MoveSelection(1);
               e.Use();
               break;

            case KeyCode.UpArrow:
               this.MoveSelection(-1);
               e.Use();
               break;

            case KeyCode.PageDown:
               this.MoveSelection(MaxVisibleRows);
               e.Use();
               break;

            case KeyCode.PageUp:
               this.MoveSelection(-MaxVisibleRows);
               e.Use();
               break;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
               this.Choose();
               e.Use();
               break;

            case KeyCode.Escape:
               this.editorWindow.Close();
               e.Use();
               break;
         }
      }

      private void MoveSelection(int delta)
      {
         if (this.results.Count == 0)
         {
            return;
         }

         this.selectedIndex = Mathf.Clamp(this.selectedIndex + delta, 0, this.results.Count - 1);
         this.scrollToSelection = true;
         this.editorWindow.Repaint();
      }

      private void Choose()
      {
         if (this.selectedIndex < 0 || this.selectedIndex >= this.results.Count)
         {
            return;
         }

         var entry = this.results[this.selectedIndex];
         var window = this.editorWindow;

         if (uScript.Instance != null)
         {
            uScript.Instance.PlaceNodeOnCanvas(entry.Signature, this.canvasPosition, this.link);
            uScript.Instance.Focus();
            uScript.Instance.Repaint();
         }

         if (window != null)
         {
            window.Close();
         }
      }

      private void UpdateResults()
      {
         this.selectedIndex = 0;
         this.scroll = Vector2.zero;

         var words = (this.search ?? string.Empty)
            .ToLowerInvariant()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

         if (words.Length == 0)
         {
            this.results = this.allEntries
               .OrderBy(entry => entry.Priority)
               .ThenBy(entry => entry.PathLower, StringComparer.Ordinal)
               .Take(this.ResultLimit)
               .ToList();
            return;
         }

         this.results = this.allEntries
            .Select(entry => new { Entry = entry, Score = Score(entry, words) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Entry.Priority)
            .ThenBy(x => x.Entry.Name.Length)
            .ThenBy(x => x.Entry.NameLower, StringComparer.Ordinal)
            .Take(this.ResultLimit)
            .Select(x => x.Entry)
            .ToList();
      }

      /// <summary>
      /// Every word must appear in the node's name or category. Matches in the name, and at the start of the name
      /// or of a word in the name, rank higher.
      /// </summary>
      private static int Score(Entry entry, string[] words)
      {
         var score = 0;

         foreach (var word in words)
         {
            var nameIndex = entry.NameLower.IndexOf(word, StringComparison.Ordinal);
            if (nameIndex == 0)
            {
               score += 100;
            }
            else if (nameIndex > 0)
            {
               score += entry.NameLower[nameIndex - 1] == ' ' ? 60 : 40;
            }
            else if (entry.PathLower.IndexOf(word, StringComparison.Ordinal) >= 0)
            {
               score += 10;
            }
            else
            {
               return 0;
            }
         }

         if (words.Length == 1 && entry.NameLower == words[0])
         {
            score += 200;
         }

         return score;
      }

      private sealed class Entry
      {
         public string Name;
         public string Category;
         public string Tooltip;
         public string Signature;
         public string NameLower;
         public string PathLower;

         /// <summary>Lower is listed first (a variable of the exact type when connecting a dropped link).</summary>
         public int Priority;
      }
   }
}
