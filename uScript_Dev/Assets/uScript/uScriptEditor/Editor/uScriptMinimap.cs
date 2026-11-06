// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptMinimap.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   A small overview of the whole graph in the corner of the canvas. Click or drag in it to move the view.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;

using Detox.ScriptEditor;

using UnityEditor;

using UnityEngine;

using FlowNode = Detox.FlowChart.Node;

public sealed partial class uScript
{
   private const string ShowMinimapSetting = "uScript.ShowMinimap";
   private const float MinimapMaxWidth = 200f;
   private const float MinimapMaxHeight = 140f;
   private const float MinimapMargin = 14f;
   private const float MinimapPadding = 6f;

   private static readonly Color MinimapBackground = new Color(0.08f, 0.08f, 0.1f, 0.72f);
   private static readonly Color MinimapBorder = new Color(1f, 1f, 1f, 0.18f);
   private static readonly Color MinimapNode = new Color(0.78f, 0.78f, 0.84f, 0.9f);
   private static readonly Color MinimapEvent = new Color(0.98f, 0.65f, 0.25f, 0.95f);
   private static readonly Color MinimapVariable = new Color(0.62f, 0.42f, 0.92f, 0.95f);
   private static readonly Color MinimapComment = new Color(1f, 1f, 1f, 0.09f);
   private static readonly Color MinimapSelected = new Color(0.35f, 0.7f, 1f, 1f);
   private static readonly Color MinimapView = new Color(1f, 1f, 1f, 0.85f);

   private bool minimapDragging;

   // Layout frozen while dragging, otherwise the overview would move under the mouse as the view moves
   private Rect minimapDragScreenRect;
   private Rect minimapDragGraphRect;
   private float minimapDragScale;

   public static bool ShowMinimap
   {
      get { return EditorPrefs.GetBool(ShowMinimapSetting, true); }
      set { EditorPrefs.SetBool(ShowMinimapSetting, value); }
   }

   public void CommandToggleMinimap()
   {
      ShowMinimap = !ShowMinimap;
      this.Repaint();
   }

   /// <summary>Graph area shown by the minimap (all nodes plus the visible part of the canvas).</summary>
   private bool GetMinimapLayout(out Rect screenRect, out Rect graphRect, out float scale)
   {
      screenRect = new Rect();
      graphRect = new Rect();
      scale = 1f;

      if (!ShowMinimap || m_ScriptEditorCtrl == null || _canvasRect.width < 260 || _canvasRect.height < 200)
      {
         return false;
      }

      var nodes = m_ScriptEditorCtrl.FlowChart.Nodes;
      if (nodes.Length == 0)
      {
         return false;
      }

      float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
      foreach (var node in nodes)
      {
         var b = node.Bounds;
         minX = Math.Min(minX, b.Left);
         minY = Math.Min(minY, b.Top);
         maxX = Math.Max(maxX, b.Right);
         maxY = Math.Max(maxY, b.Bottom);
      }

      var view = this.GetVisibleGraphRect();
      minX = Math.Min(minX, view.xMin);
      minY = Math.Min(minY, view.yMin);
      maxX = Math.Max(maxX, view.xMax);
      maxY = Math.Max(maxY, view.yMax);

      graphRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
      if (graphRect.width < 1 || graphRect.height < 1)
      {
         return false;
      }

      var inner = MinimapMaxWidth - (2 * MinimapPadding);
      var innerHeight = MinimapMaxHeight - (2 * MinimapPadding);
      scale = Math.Min(inner / graphRect.width, innerHeight / graphRect.height);

      var width = (graphRect.width * scale) + (2 * MinimapPadding);
      var height = (graphRect.height * scale) + (2 * MinimapPadding);

      screenRect = new Rect(
         _canvasRect.xMax - width - MinimapMargin,
         _canvasRect.yMax - height - MinimapMargin,
         width,
         height);

      return true;
   }

   /// <summary>The part of the graph visible in the canvas, in graph coordinates.</summary>
   private Rect GetVisibleGraphRect()
   {
      var location = m_ScriptEditorCtrl.FlowChart.Location;
      var zoom = Math.Max(0.01f, this.mapScale);
      return new Rect(-location.X, -location.Y, _canvasRect.width / zoom, _canvasRect.height / zoom);
   }

   private static Rect GraphToMinimap(Rect r, Rect screenRect, Rect graphRect, float scale)
   {
      return new Rect(
         screenRect.x + MinimapPadding + ((r.x - graphRect.x) * scale),
         screenRect.y + MinimapPadding + ((r.y - graphRect.y) * scale),
         Math.Max(1f, r.width * scale),
         Math.Max(1f, r.height * scale));
   }

   /// <summary>Draws the minimap over the canvas (call during Repaint, in window coordinates).</summary>
   private void DrawMinimap()
   {
      Rect screenRect, graphRect;
      float scale;
      if (Event.current.type != EventType.Repaint || !this.GetMinimapLayout(out screenRect, out graphRect, out scale))
      {
         return;
      }

      EditorGUI.DrawRect(screenRect, MinimapBackground);
      DrawRectOutline(screenRect, MinimapBorder);

      var nodes = m_ScriptEditorCtrl.FlowChart.Nodes;

      // Comment boxes first so other nodes draw on top of them
      for (var pass = 0; pass < 2; pass++)
      {
         foreach (var node in nodes)
         {
            var isComment = node is CommentDisplayNode;
            if ((pass == 0) != isComment)
            {
               continue;
            }

            var b = node.Bounds;
            var r = GraphToMinimap(new Rect(b.X, b.Y, b.Width, b.Height), screenRect, graphRect, scale);
            EditorGUI.DrawRect(r, MinimapColor(node));
         }
      }

      var view = GraphToMinimap(this.GetVisibleGraphRect(), screenRect, graphRect, scale);
      view = Rect.MinMaxRect(
         Math.Max(view.xMin, screenRect.xMin),
         Math.Max(view.yMin, screenRect.yMin),
         Math.Min(view.xMax, screenRect.xMax),
         Math.Min(view.yMax, screenRect.yMax));
      DrawRectOutline(view, MinimapView);
   }

   private static Color MinimapColor(FlowNode node)
   {
      if (node is CommentDisplayNode) return MinimapComment;
      if (node.Selected) return MinimapSelected;
      if (node is LocalNodeDisplayNode || node is ExternalConnectionDisplayNode || node is OwnerConnectionDisplayNode) return MinimapVariable;
      if (node is EntityEventDisplayNode) return MinimapEvent;

      var logic = node as LogicNodeDisplayNode;
      if (logic != null && logic.LogicNode.Inputs.Length == 0 && logic.LogicNode.Outputs.Length == 0 && logic.LogicNode.Events.Length > 0)
      {
         return MinimapEvent;
      }

      return MinimapNode;
   }

   private static void DrawRectOutline(Rect r, Color color)
   {
      EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, 1), color);
      EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - 1, r.width, 1), color);
      EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, 1, r.height), color);
      EditorGUI.DrawRect(new Rect(r.xMax - 1, r.yMin, 1, r.height), color);
   }

   /// <summary>
   /// Handles clicks and drags in the minimap before the canvas sees them. Returns True when the event was used.
   /// </summary>
   private bool HandleMinimapInput()
   {
      var e = Event.current;
      if (e == null)
      {
         return false;
      }

      if (this.minimapDragging && (e.rawType == EventType.MouseUp || e.type == EventType.MouseUp))
      {
         this.minimapDragging = false;
         e.Use();
         return true;
      }

      if (e.type == EventType.MouseDrag && this.minimapDragging)
      {
         this.MoveViewFromMinimap(e.mousePosition, this.minimapDragScreenRect, this.minimapDragGraphRect, this.minimapDragScale);
         e.Use();
         this.Repaint();
         return true;
      }

      Rect screenRect, graphRect;
      float scale;
      if (!this.GetMinimapLayout(out screenRect, out graphRect, out scale))
      {
         this.minimapDragging = false;
         return false;
      }

      if (!screenRect.Contains(e.mousePosition))
      {
         return false;
      }

      if (e.type == EventType.MouseDown && e.button == 0)
      {
         this.minimapDragging = true;
         this.minimapDragScreenRect = screenRect;
         this.minimapDragGraphRect = graphRect;
         this.minimapDragScale = scale;
         this.MoveViewFromMinimap(e.mousePosition, screenRect, graphRect, scale);
      }
      else if (e.type != EventType.MouseDown && e.type != EventType.ContextClick)
      {
         return false;
      }

      // Keep the canvas from also selecting, starting a marquee or opening its menu under the minimap
      e.Use();
      this.Repaint();
      return true;
   }

   private void MoveViewFromMinimap(Vector2 mouse, Rect screenRect, Rect graphRect, float scale)
   {
      var graphX = graphRect.x + ((mouse.x - screenRect.x - MinimapPadding) / scale);
      var graphY = graphRect.y + ((mouse.y - screenRect.y - MinimapPadding) / scale);
      this.CenterCanvasOn(graphX, graphY);
   }
}
