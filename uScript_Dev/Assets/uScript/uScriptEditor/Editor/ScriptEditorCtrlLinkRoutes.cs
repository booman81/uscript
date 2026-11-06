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
