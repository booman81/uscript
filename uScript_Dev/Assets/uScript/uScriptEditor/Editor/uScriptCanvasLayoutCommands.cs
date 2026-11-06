// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptCanvasLayoutCommands.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Canvas view commands: frame selected/all nodes and zoom toward the mouse.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;

using Detox.Drawing;

using UnityEngine;

using FlowNode = Detox.FlowChart.Node;

public sealed partial class uScript
{
   private const float FramePadding = 40f;
   private const float MinZoom = 0.1f;
   private const float MaxZoom = 1.0f;

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

   /// <summary>Adds the Frame items to a canvas or View menu.</summary>
   private void AddLayoutMenuItems(UnityEditor.GenericMenu menu, string prefix)
   {
      int selected = this.SelectedNodeCount;

      menu.AddItem(new GUIContent(prefix + (selected > 0 ? "Frame Selected  (F)" : "Frame All  (F)")), false, this.CommandCanvasFrameSelected);
      menu.AddItem(new GUIContent(prefix + "Frame All  (A)"), false, this.CommandCanvasFrameAll);

      var hasReroutes = m_ScriptEditorCtrl != null
         && m_ScriptEditorCtrl.FlowChart.SelectedLinks.Any(link => link.RoutePoints != null && link.RoutePoints.Length > 0);
      if (hasReroutes)
      {
         menu.AddItem(new GUIContent(prefix + "Remove Reroute Points from Selected Links"), false, () => m_ScriptEditorCtrl.StraightenSelectedLinks());
      }
   }
}
