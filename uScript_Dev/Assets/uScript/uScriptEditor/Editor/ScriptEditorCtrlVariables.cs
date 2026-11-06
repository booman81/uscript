// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScriptEditorCtrlVariables.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Variable operations used by the Variables panel: select, modify (with undo) and add variable nodes.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Detox.ScriptEditor
{
   using System;
   using System.Collections.Generic;
   using System.Linq;

   using Detox.Drawing;

   public partial class ScriptEditorCtrl
   {
      /// <summary>All variable nodes in the graph.</summary>
      public LocalNode[] VariableNodes
      {
         get { return m_ScriptEditor.Locals; }
      }

      /// <summary>All variable types uScript knows about (for creating new variables).</summary>
      public string[] VariableTypes
      {
         get { return m_ScriptEditor.Types; }
      }

      /// <summary>Selects the given nodes on the canvas.</summary>
      /// <param name="guids">The nodes to select.</param>
      /// <param name="addToSelection">When false, the current selection is cleared first.</param>
      public void SelectNodes(IEnumerable<Guid> guids, bool addToSelection)
      {
         if (!addToSelection)
         {
            m_FlowChart.DeselectAll();
         }

         m_FlowChart.SelectNodes(guids.ToArray());
      }

      /// <summary>
      /// Applies a change to the given variable nodes as a single undo step. The nodes are replaced directly,
      /// so uScript's automatic syncing of same-named variables does not interfere with the change.
      /// </summary>
      /// <returns>The number of nodes that changed.</returns>
      public int ModifyVariableNodes(IEnumerable<Guid> guids, Func<LocalNode, LocalNode> change, string undoName)
      {
         var wanted = new HashSet<Guid>(guids);
         var targets = m_ScriptEditor.Locals.Where(local => wanted.Contains(local.Guid)).ToArray();
         if (targets.Length == 0)
         {
            return 0;
         }

         var batch = new Patch.Batch(undoName);
         var changed = 0;

         RemoveEventHandlers();
         try
         {
            foreach (var local in targets)
            {
               var oldNode = local.Copy(true);
               EntityNode newNode = change(local);

               if (oldNode.Equals(newNode))
               {
                  continue;
               }

               m_ScriptEditor.TrustedUpdate(newNode);
               batch.Add(new Patch.EntityNode("Node Modified", oldNode, newNode));
               changed++;
            }
         }
         finally
         {
            AddEventHandlers();
         }

         if (batch.HasPatches)
         {
            m_Dirty = true;
            uScript.Instance.RegisterUndo(batch);
            PatchDisplay(batch);
         }

         return changed;
      }

      /// <summary>Adds a variable node at a canvas position (the same space as Cursor.ScaledPosition).</summary>
      /// <returns>The new node's Guid.</returns>
      public Guid AddVariableNodeAt(LocalNode node, Point canvasPosition)
      {
         this.ContextCursor = canvasPosition;
         this.AddVariableNode(node);
         return node.Guid;
      }
   }
}
