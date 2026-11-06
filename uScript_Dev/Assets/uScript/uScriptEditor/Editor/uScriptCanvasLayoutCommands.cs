// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptCanvasLayoutCommands.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Canvas view and layout commands: frame selected/all nodes, zoom toward the mouse, align and distribute nodes.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Detox.Drawing;

using UnityEngine;

using FlowNode = Detox.FlowChart.Node;

public sealed partial class uScript
{
   private const float FramePadding = 40f;
   private const float MinZoom = 0.1f;
   private const float MaxZoom = 1.0f;

   // Minimum space kept between nodes by Align and Distribute. Vertical is larger because variable
   // nodes draw their name below the node.
   private const int LayoutGapX = 30;
   private const int LayoutGapY = 30;

   public enum NodeAlignment
   {
      Left,
      Center,
      Right,
      Top,
      Middle,
      Bottom
   }

   public int SelectedNodeCount
   {
      get
      {
         return m_ScriptEditorCtrl == null ? 0 : m_ScriptEditorCtrl.FlowChart.SelectedNodes.Length;
      }
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Framing
   // ---------------------------------------------------------------------------------------------------------------

   /// <summary>Frames the selected nodes, or every node when nothing is selected.</summary>
   public void CommandCanvasFrameSelected()
   {
      if (m_ScriptEditorCtrl == null) return;

      var nodes = m_ScriptEditorCtrl.FlowChart.SelectedNodes;
      this.FrameNodes(nodes.Length > 0 ? nodes : m_ScriptEditorCtrl.FlowChart.Nodes);
   }

   public void CommandCanvasFrameAll()
   {
      if (m_ScriptEditorCtrl == null) return;

      this.FrameNodes(m_ScriptEditorCtrl.FlowChart.Nodes);
   }

   private void FrameNodes(FlowNode[] nodes)
   {
      if (m_ScriptEditorCtrl == null || nodes == null || nodes.Length == 0) return;

      float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
      foreach (var node in nodes)
      {
         var b = node.Bounds;
         minX = Math.Min(minX, b.Left);
         minY = Math.Min(minY, b.Top);
         maxX = Math.Max(maxX, b.Right);
         maxY = Math.Max(maxY, b.Bottom);
      }

      Vector2 view = this.CanvasViewSize();
      float boundsWidth = Math.Max(1f, maxX - minX);
      float boundsHeight = Math.Max(1f, maxY - minY);

      float zoom = Mathf.Min(
         (view.x - (2 * FramePadding)) / boundsWidth,
         (view.y - (2 * FramePadding)) / boundsHeight);
      zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);

      this.SetCanvasView(zoom, (minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
   }

   /// <summary>Centers the canvas on a graph point at the current zoom.</summary>
   public void CenterCanvasOn(float graphX, float graphY)
   {
      this.SetCanvasView(this.mapScale, graphX, graphY);
   }

   private void SetCanvasView(float zoom, float graphCenterX, float graphCenterY)
   {
      if (m_ScriptEditorCtrl == null) return;

      Vector2 view = this.CanvasViewSize();

      // Screen position = (graph position + FlowChart.Location) * Zoom, relative to the canvas.
      // The canvas can not scroll into negative graph space, the same as everywhere else in uScript.
      float x = (view.x * 0.5f / zoom) - graphCenterX;
      float y = (view.y * 0.5f / zoom) - graphCenterY;
      var location = new Point(
         (int)Math.Min(0, Math.Max(-UInt16.MaxValue, x)),
         (int)Math.Min(0, Math.Max(-UInt16.MaxValue, y)));

      // Keep uScript's zoom value in step so the paint code does not re-apply its zoom-point compensation.
      this.mapScale = zoom;
      m_ScriptEditorCtrl.FlowChart.SetViewport(zoom, location);

      this.SaveCanvasPosition();
   }

   private Vector2 CanvasViewSize()
   {
      if (_canvasRect.width > 1 && _canvasRect.height > 1)
      {
         return new Vector2(_canvasRect.width, _canvasRect.height);
      }

      return new Vector2(
         Math.Max(1f, NodeWindowRect.width),
         Math.Max(1f, NodeWindowRect.height - NodeToolbarRect.height));
   }

   private void SaveCanvasPosition()
   {
      var location = m_ScriptEditorCtrl.FlowChart.Location;
      this.currentCanvasPosition = location.X + "," + location.Y;
      if (!string.IsNullOrEmpty(this.fullPath))
      {
         SetSetting(
            "uScript\\" + uScriptConfig.ConstantPaths.RelativePath(this.fullPath) + "\\CanvasPosition",
            this.currentCanvasPosition);
      }
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Zoom toward the mouse
   // ---------------------------------------------------------------------------------------------------------------

   /// <summary>
   /// The point zoom commands keep fixed: the mouse when it is over the canvas, otherwise the canvas center.
   /// </summary>
   private Vector2 GetZoomAnchor()
   {
      var e = Event.current;
      if (e != null && (e.isKey || e.type == EventType.ScrollWheel) && _canvasRect.Contains(e.mousePosition))
      {
         return e.mousePosition - _canvasRect.position;
      }

      return new Vector2(_canvasRect.width * 0.5f, _canvasRect.height * 0.5f);
   }

   /// <summary>Adds Frame, Align and Distribute items to a canvas or View menu.</summary>
   private void AddLayoutMenuItems(UnityEditor.GenericMenu menu, string prefix)
   {
      int selected = this.SelectedNodeCount;

      menu.AddItem(new GUIContent(prefix + (selected > 0 ? "Frame Selected  (F)" : "Frame All  (F)")), false, this.CommandCanvasFrameSelected);
      menu.AddItem(new GUIContent(prefix + "Frame All  (A)"), false, this.CommandCanvasFrameAll);

      AddLayoutItem(menu, prefix + "Align/Left", selected >= 2, () => this.CommandAlignSelectedNodes(NodeAlignment.Left));
      AddLayoutItem(menu, prefix + "Align/Center", selected >= 2, () => this.CommandAlignSelectedNodes(NodeAlignment.Center));
      AddLayoutItem(menu, prefix + "Align/Right", selected >= 2, () => this.CommandAlignSelectedNodes(NodeAlignment.Right));
      menu.AddSeparator(prefix + "Align/");
      AddLayoutItem(menu, prefix + "Align/Top", selected >= 2, () => this.CommandAlignSelectedNodes(NodeAlignment.Top));
      AddLayoutItem(menu, prefix + "Align/Middle", selected >= 2, () => this.CommandAlignSelectedNodes(NodeAlignment.Middle));
      AddLayoutItem(menu, prefix + "Align/Bottom", selected >= 2, () => this.CommandAlignSelectedNodes(NodeAlignment.Bottom));
      AddLayoutItem(menu, prefix + "Distribute/Horizontally", selected >= 3, () => this.CommandDistributeSelectedNodes(true));
      AddLayoutItem(menu, prefix + "Distribute/Vertically", selected >= 3, () => this.CommandDistributeSelectedNodes(false));
   }

   private static void AddLayoutItem(UnityEditor.GenericMenu menu, string path, bool enabled, UnityEditor.GenericMenu.MenuFunction action)
   {
      if (enabled)
      {
         menu.AddItem(new GUIContent(path), false, action);
      }
      else
      {
         menu.AddDisabledItem(new GUIContent(path));
      }
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Align and distribute
   // ---------------------------------------------------------------------------------------------------------------

   public void CommandAlignSelectedNodes(NodeAlignment alignment)
   {
      if (m_ScriptEditorCtrl == null) return;

      var nodes = m_ScriptEditorCtrl.FlowChart.SelectedNodes;
      if (nodes.Length < 2) return;

      int left = nodes.Min(n => n.Bounds.Left);
      int right = nodes.Max(n => n.Bounds.Right);
      int top = nodes.Min(n => n.Bounds.Top);
      int bottom = nodes.Max(n => n.Bounds.Bottom);
      int centerX = (left + right) / 2;
      int centerY = (top + bottom) / 2;

      var moved = new List<FlowNode>();
      foreach (var node in nodes)
      {
         var location = node.Location;
         var size = node.Size;
         int x = location.X;
         int y = location.Y;

         switch (alignment)
         {
            case NodeAlignment.Left:   x = left; break;
            case NodeAlignment.Center: x = centerX - (size.Width / 2); break;
            case NodeAlignment.Right:  x = right - size.Width; break;
            case NodeAlignment.Top:    y = top; break;
            case NodeAlignment.Middle: y = centerY - (size.Height / 2); break;
            case NodeAlignment.Bottom: y = bottom - size.Height; break;
         }

         if (x != location.X || y != location.Y)
         {
            node.Location = new Point(x, y);
            moved.Add(node);
         }
      }

      // Nodes that now share a row (Top/Middle/Bottom) or a column (Left/Center/Right) are pushed apart
      // along that row/column, keeping their order, so nothing ends up on top of something else.
      bool row = alignment == NodeAlignment.Top || alignment == NodeAlignment.Middle || alignment == NodeAlignment.Bottom;
      this.SeparateOverlaps(nodes, row, moved);

      this.CommitNodeMoves(moved);
   }

   /// <summary>
   /// Sweeps along one axis and moves each node just far enough to leave a gap after the previous one.
   /// The first node never moves.
   /// </summary>
   private void SeparateOverlaps(FlowNode[] nodes, bool horizontal, List<FlowNode> moved)
   {
      var sorted = horizontal
         ? nodes.OrderBy(n => n.Location.X).ThenBy(n => n.Location.Y).ToArray()
         : nodes.OrderBy(n => n.Location.Y).ThenBy(n => n.Location.X).ToArray();

      int gap = horizontal ? LayoutGapX : LayoutGapY;
      int previousEnd = int.MinValue;

      foreach (var node in sorted)
      {
         var location = node.Location;
         int start = horizontal ? location.X : location.Y;

         if (previousEnd != int.MinValue && start < previousEnd + gap)
         {
            start = previousEnd + gap;
            node.Location = horizontal ? new Point(start, location.Y) : new Point(location.X, start);
            if (!moved.Contains(node)) moved.Add(node);
         }

         previousEnd = start + (horizontal ? node.Size.Width : node.Size.Height);
      }
   }

   /// <summary>
   /// Spaces the selected nodes so the gaps between them are equal. The outer nodes stay put unless the nodes
   /// do not fit between them, then the gap is the minimum and the last node moves out.
   /// </summary>
   public void CommandDistributeSelectedNodes(bool horizontal)
   {
      if (m_ScriptEditorCtrl == null) return;

      var nodes = m_ScriptEditorCtrl.FlowChart.SelectedNodes;
      if (nodes.Length < 3) return;

      var sorted = horizontal
         ? nodes.OrderBy(n => n.Bounds.Left + (n.Size.Width / 2)).ToArray()
         : nodes.OrderBy(n => n.Bounds.Top + (n.Size.Height / 2)).ToArray();

      int start = horizontal ? sorted.Min(n => n.Bounds.Left) : sorted.Min(n => n.Bounds.Top);
      int end = horizontal ? sorted.Max(n => n.Bounds.Right) : sorted.Max(n => n.Bounds.Bottom);
      int totalSize = sorted.Sum(n => horizontal ? n.Size.Width : n.Size.Height);
      float gap = (float)(end - start - totalSize) / (sorted.Length - 1);

      // Not enough room between the outer nodes: use the minimum gap and let the row/column grow instead of
      // overlapping nodes.
      gap = Math.Max(gap, horizontal ? LayoutGapX : LayoutGapY);

      var moved = new List<FlowNode>();
      float position = start;
      foreach (var node in sorted)
      {
         var location = node.Location;
         var newLocation = horizontal
            ? new Point(Mathf.RoundToInt(position), location.Y)
            : new Point(location.X, Mathf.RoundToInt(position));

         if (newLocation.X != location.X || newLocation.Y != location.Y)
         {
            node.Location = newLocation;
            moved.Add(node);
         }

         position += (horizontal ? node.Size.Width : node.Size.Height) + gap;
      }

      this.CommitNodeMoves(moved);
   }

   private void CommitNodeMoves(List<FlowNode> moved)
   {
      if (moved.Count == 0) return;

      // One undo step for the whole operation; this is the same path a mouse drag uses.
      m_ScriptEditorCtrl.FlowChart.CommitNodeMoves(moved.ToArray());
   }
}
