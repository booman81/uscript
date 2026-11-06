// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScriptEditorCtrlLinkDrop.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Drag a link onto empty canvas: find nodes that can connect to the dragged socket, then create the chosen node
//   already connected (one undo step).
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Detox.ScriptEditor
{
   using System;
   using System.Collections.Generic;

   using Detox.Drawing;
   using Detox.FlowChart;

   /// <summary>The socket a link was dragged from when it was dropped on empty canvas.</summary>
   public sealed class LinkDropInfo
   {
      public Guid NodeGuid;
      public string Anchor;
      public bool Input;
      public bool Output;

      /// <summary>Type of the dragged socket, or empty for a signal (In/Out) socket.</summary>
      public string Type;
   }

   public partial class ScriptEditorCtrl
   {
      public struct SocketRef
      {
         public string Name;
         public bool Input;
         public bool Output;
      }

      private void FlowchartLinkDroppedOnCanvas(Detox.FlowChart.Node node, AnchorPoint anchor)
      {
         var displayNode = node as DisplayNode;
         if (displayNode == null)
         {
            return;
         }

         var info = new LinkDropInfo
         {
            NodeGuid = displayNode.Guid,
            Anchor = anchor.Name,
            Input = anchor.Input,
            Output = anchor.Output,
            Type = string.Empty
         };

         var parameter = m_ScriptEditor.FindNodeParameter(displayNode.EntityNode, anchor.Name);
         if (parameter != Parameter.Empty && parameter.Type != null)
         {
            info.Type = parameter.Type;
         }

         uScript.Instance.OnLinkDroppedOnCanvas(info);
      }

      /// <summary>The sockets of a node in the order they are tried when connecting automatically.</summary>
      public static List<SocketRef> GetConnectableSockets(EntityNode node)
      {
         var list = new List<SocketRef>();

         Action<string, bool, bool> add = (name, input, output) =>
         {
            if (!string.IsNullOrEmpty(name) && (input || output))
            {
               list.Add(new SocketRef { Name = name, Input = input, Output = output });
            }
         };

         if (node is LogicNode)
         {
            var logic = (LogicNode)node;
            foreach (var plug in logic.Inputs) add(plug.Name, true, false);
            foreach (var plug in logic.Outputs) add(plug.Name, false, true);
            foreach (var plug in logic.Events) add(plug.Name, false, true);
            foreach (var p in logic.Parameters)
            {
               if (!p.IsHidden()) add(p.Name, p.Input, p.Output);
            }
         }
         else if (node is EntityMethod)
         {
            var method = (EntityMethod)node;
            add(method.Input.Name, true, false);
            add(method.Output.Name, false, true);
            if (method.Instance.IsVisible() && !method.IsStatic) add(method.Instance.Name, true, false);
            foreach (var p in method.Parameters)
            {
               if (!p.IsHidden()) add(p.Name, p.Input, p.Output);
            }
         }
         else if (node is EntityEvent)
         {
            var entityEvent = (EntityEvent)node;
            foreach (var plug in entityEvent.Outputs) add(plug.Name, false, true);
            if (entityEvent.Instance.IsVisible() && !entityEvent.IsStatic) add(entityEvent.Instance.Name, true, false);
            foreach (var p in entityEvent.Parameters)
            {
               if (!p.Input && !p.IsHidden()) add(p.Name, false, p.Output);
            }
         }
         else if (node is EntityProperty)
         {
            var property = (EntityProperty)node;
            add(property.Parameter.Name, true, true);
            if (property.Instance.IsVisible() && !property.IsStatic) add(property.Instance.Name, true, false);
         }
         else if (node is LocalNode)
         {
            var local = (LocalNode)node;
            add(local.Value.Name, local.Value.Input, local.Value.Output);
         }
         else if (node is ExternalConnection)
         {
            add(((ExternalConnection)node).Connection, true, true);
         }
         else if (node is OwnerConnection)
         {
            var owner = (OwnerConnection)node;
            add(owner.Connection.Name, owner.Connection.Input, owner.Connection.Output);
         }

         return list;
      }

      /// <summary>
      /// Finds the first socket of <paramref name="candidate"/> that can be linked to the dropped socket.
      /// The candidate does not need to be in the graph.
      /// </summary>
      public bool FindAutoLink(LinkDropInfo drop, EntityNode candidate, out LinkNode link)
      {
         link = new LinkNode();

         if (drop == null || null == m_ScriptEditor.GetNode(drop.NodeGuid))
         {
            return false;
         }

         foreach (var socket in GetConnectableSockets(candidate))
         {
            LinkNode attempt;

            // Same direction rules as dragging onto a socket (FlowChartCtrl.OnMouseUp)
            if (drop.Output && socket.Input)
            {
               attempt = new LinkNode(drop.NodeGuid, drop.Anchor, candidate.Guid, socket.Name);
            }
            else if (drop.Input && socket.Output)
            {
               attempt = new LinkNode(candidate.Guid, socket.Name, drop.NodeGuid, drop.Anchor);
            }
            else
            {
               continue;
            }

            string reason;
            if (m_ScriptEditor.VerifyLinkWithCandidate(attempt, candidate, out reason))
            {
               link = attempt;
               return true;
            }
         }

         return false;
      }

      /// <summary>True when the node (a toolbox prototype) has a socket that can connect to the dropped socket.</summary>
      public bool CanAutoLink(LinkDropInfo drop, EntityNode prototype)
      {
         LinkNode link;
         return FindAutoLink(drop, prototype.Copy(false), out link);
      }

      /// <summary>
      /// Adds <paramref name="entityNode"/> so that its connecting socket sits at <paramref name="canvasPosition"/>,
      /// links it to the dropped socket and selects it. Node and link are one undo step.
      /// </summary>
      public void AddNodeLinkedTo(EntityNode entityNode, Point canvasPosition, LinkDropInfo drop)
      {
         LinkNode link;
         if (!FindAutoLink(drop, entityNode, out link))
         {
            // Nothing to connect after all (the graph changed): just add the node
            ContextCursor = canvasPosition;
            AddVariableNode(entityNode);
            return;
         }

         // Graph-space point under the mouse
         ContextCursor = canvasPosition;
         Point graphPoint = m_ContextCursor;

         string newSocket = link.Source.Guid == entityNode.Guid ? link.Source.Anchor : link.Destination.Anchor;
         entityNode.Position = PositionForSocketAt(entityNode, newSocket, graphPoint);

         string autoInstance = uScript.Instance.AutoAssignInstance(entityNode);
         if ("" != autoInstance)
         {
            Parameter instance = entityNode.Instance;
            instance.Default = autoInstance;
            entityNode.Instance = instance;
         }

         var batch = new Patch.Batch("Add Connected Node");

         m_ScriptEditor.AddNode(entityNode);
         batch.Add(new Patch.EntityNode("", null, entityNode.Copy(true)));

         // AddNode verifies the link again and silently drops it if it is not allowed
         m_ScriptEditor.AddNode(link);
         if (null != m_ScriptEditor.GetNode(link.Guid))
         {
            batch.Add(new Patch.EntityNode("", null, link));
         }

         m_Dirty = true;

         uScript.Instance.RegisterUndo(batch);
         PatchDisplay(batch, new List<Guid> { entityNode.Guid }, true);
      }

      /// <summary>Node position that puts the named socket at <paramref name="graphPoint"/>.</summary>
      private Point PositionForSocketAt(EntityNode entityNode, string socketName, Point graphPoint)
      {
         try
         {
            entityNode.Position = Point.Empty;
            DisplayNode measure = CreateDisplayNode(entityNode);

            if (measure != null && measure.AnchorPoints != null)
            {
               foreach (var point in measure.AnchorPoints)
               {
                  if (point.Name == socketName)
                  {
                     int x = (int)(point.X / 100.0f * measure.Size.Width);
                     int y = (int)(point.Y / 100.0f * measure.Size.Height);
                     return new Point(graphPoint.X - x, graphPoint.Y - y);
                  }
               }
            }
         }
         catch (Exception)
         {
            // Fall back to the normal placement below
         }

         return new Point(graphPoint.X - 10, graphPoint.Y - 10);
      }

      /// <summary>Creates the display node for an entity without adding it to the canvas.</summary>
      private DisplayNode CreateDisplayNode(EntityNode entityNode)
      {
         if (entityNode is CommentNode) return new CommentDisplayNode((CommentNode)entityNode, this);
         if (entityNode is EntityEvent) return new EntityEventDisplayNode((EntityEvent)entityNode, this);
         if (entityNode is EntityMethod) return new EntityMethodDisplayNode((EntityMethod)entityNode, this);
         if (entityNode is EntityProperty) return new EntityPropertyDisplayNode((EntityProperty)entityNode, this);
         if (entityNode is LocalNode) return new LocalNodeDisplayNode((LocalNode)entityNode, this);
         if (entityNode is LogicNode) return new LogicNodeDisplayNode((LogicNode)entityNode, this);
         if (entityNode is ExternalConnection) return new ExternalConnectionDisplayNode((ExternalConnection)entityNode, this);
         if (entityNode is OwnerConnection) return new OwnerConnectionDisplayNode((OwnerConnection)entityNode, this);
         return null;
      }
   }
}
