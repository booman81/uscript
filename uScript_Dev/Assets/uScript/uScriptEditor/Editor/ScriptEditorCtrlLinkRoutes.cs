// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScriptEditorCtrlLinkRoutes.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Stores reroute points edited on the canvas in the graph (with undo).
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Detox.ScriptEditor
{
   using Detox.FlowChart;

   public partial class ScriptEditorCtrl
   {
      private void FlowchartLinkRouteChanged(Link chartLink)
      {
         if (chartLink == null || !(chartLink.Tag is LinkNode))
         {
            return;
         }

         LinkNode oldLink = (LinkNode)chartLink.Tag;
         if (null == m_ScriptEditor.GetNode(oldLink.Guid))
         {
            return;
         }

         LinkNode newLink = oldLink;
         newLink.RoutePoints = LinkRoute.Format(chartLink.RoutePoints);

         if (oldLink.Equals(newLink))
         {
            return;
         }

         // Same link, only its shape changed: replace it without re-checking the connection
         m_ScriptEditor.TrustedUpdate(newLink);
         chartLink.Tag = newLink;
         m_Dirty = true;

         // The canvas already shows the new shape; only record it (undo also keeps the cached graph up to date)
         var patch = new Patch.EntityNode("Reroute Link", oldLink, newLink);
         uScript.Instance.RegisterUndo(patch);
         OnScriptModified();
      }

      /// <summary>Alt+click on a socket: removes every link of that socket (one undo step).</summary>
      private void FlowchartSocketDisconnectRequested(Detox.FlowChart.Node node, string anchor)
      {
         if (node == null)
         {
            return;
         }

         var guid = node.Guid;
         var batch = new Patch.Batch("Disconnect Socket");

         foreach (LinkNode link in m_ScriptEditor.Links)
         {
            if ((link.Source.Guid == guid && link.Source.Anchor == anchor) ||
                (link.Destination.Guid == guid && link.Destination.Anchor == anchor))
            {
               m_ScriptEditor.RemoveNode(link);
               batch.Add(new Patch.EntityNode("", link, null));
            }
         }

         if (batch.HasPatches)
         {
            m_Dirty = true;
            uScript.Instance.RegisterUndo(batch);
            PatchDisplay(batch);
         }
      }

      /// <summary>Alt+click on a link: deletes it (one undo step).</summary>
      private void FlowchartLinkDeleteRequested(Link chartLink)
      {
         if (chartLink == null || !(chartLink.Tag is LinkNode))
         {
            return;
         }

         LinkNode link = (LinkNode)chartLink.Tag;
         if (null == m_ScriptEditor.GetNode(link.Guid))
         {
            return;
         }

         m_ScriptEditor.RemoveNode(link);

         var batch = new Patch.Batch("Delete Link");
         batch.Add(new Patch.EntityNode("", link, null));

         m_Dirty = true;
         uScript.Instance.RegisterUndo(batch);
         PatchDisplay(batch);
      }

      /// <summary>Alt+click on a node: deletes it and its links (one undo step), like Delete does.</summary>
      private void FlowchartNodeDeleteRequested(Detox.FlowChart.Node node)
      {
         if (node == null)
         {
            return;
         }

         m_FlowChart.DeselectAll();
         m_FlowChart.SelectNodes(new[] { node.Guid });
         DeleteSelectedNodes();
      }

      /// <summary>Removes all reroute points from the selected links.</summary>
      public void StraightenSelectedLinks()
      {
         var batch = new Patch.Batch("Straighten Links");

         foreach (Link chartLink in m_FlowChart.SelectedLinks)
         {
            if (!(chartLink.Tag is LinkNode)) continue;

            LinkNode oldLink = (LinkNode)chartLink.Tag;
            if (string.IsNullOrEmpty(oldLink.RoutePoints)) continue;

            LinkNode newLink = oldLink;
            newLink.RoutePoints = null;

            m_ScriptEditor.TrustedUpdate(newLink);
            chartLink.Tag = newLink;
            chartLink.RoutePoints = new Detox.Drawing.Point[0];
            batch.Add(new Patch.EntityNode("", oldLink, newLink));
         }

         if (batch.HasPatches)
         {
            m_Dirty = true;
            uScript.Instance.RegisterUndo(batch);
            OnScriptModified();
         }
      }
   }
}
