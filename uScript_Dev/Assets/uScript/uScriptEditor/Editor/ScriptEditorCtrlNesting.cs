// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScriptEditorCtrlNesting.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Make Nested Graph: works out which selected nodes move into a new graph, the External Connections that new
//   graph needs, and how the new nested node connects back into this graph.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Detox.ScriptEditor
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;

   using Detox.Drawing;

   /// <summary>How a socket of the new nested node connects to a node that stays in the parent graph.</summary>
   [Serializable]
   public class NestedGraphConnection
   {
      /// <summary>True: the parent node feeds the nested node; False: the nested node feeds the parent node.</summary>
      public bool Incoming;

      public string ExternalName;
      public string OutsideGuid;
      public string OutsideAnchor;
   }

   public class NestedGraphPlan
   {
      public ScriptEditor Graph;
      public List<Guid> MovedNodes = new List<Guid>();
      public List<NestedGraphConnection> Connections = new List<NestedGraphConnection>();
      public List<string> Problems = new List<string>();
      public List<string> KeptInParent = new List<string>();
      public Point Position;
   }

   public partial class ScriptEditorCtrl
   {
      private static bool IsVariableLike(EntityNode node)
      {
         return node is LocalNode || node is EntityProperty || node is OwnerConnection;
      }

      private static bool IsActionLike(EntityNode node)
      {
         return node is LogicNode || node is EntityMethod || node is EntityEvent;
      }

      /// <summary>
      /// Builds the nested graph for the selected nodes into <paramref name="nestedGraph"/> (an empty graph) without
      /// changing this graph. Check Problems before using the result.
      /// </summary>
      public NestedGraphPlan PlanNestedGraph(ScriptEditor nestedGraph)
      {
         var plan = new NestedGraphPlan { Graph = nestedGraph };

         var selected = m_FlowChart.SelectedNodes
            .OfType<DisplayNode>()
            .Select(display => m_ScriptEditor.GetNode(display.Guid))
            .Where(node => node != null && !(node is LinkNode))
            .ToList();

         var selectedGuids = new HashSet<Guid>(selected.Select(node => node.Guid));
         var links = m_ScriptEditor.Links;

         // Decide what moves. External Connections are part of this graph's own interface and stay. Variables
         // stay when nodes outside the selection use them too: a nested node can't feed an action node directly.
         var moved = new List<EntityNode>();
         foreach (var node in selected)
         {
            if (node is ExternalConnection)
            {
               plan.KeptInParent.Add("External Connection \"" + ((ExternalConnection)node).Name.Default + "\"");
               continue;
            }

            if (IsVariableLike(node))
            {
               var usedOutside = links.Any(link =>
               {
                  Guid other;
                  if (link.Source.Guid == node.Guid) other = link.Destination.Guid;
                  else if (link.Destination.Guid == node.Guid) other = link.Source.Guid;
                  else return false;

                  return !selectedGuids.Contains(other) && IsActionLike(m_ScriptEditor.GetNode(other));
               });

               var sharedName = false;
               if (node is LocalNode)
               {
                  var name = ((LocalNode)node).Name.Default;
                  sharedName = !string.IsNullOrEmpty(name) && m_ScriptEditor.Locals.Any(local =>
                     local.Name.Default == name && !selectedGuids.Contains(local.Guid));
               }

               if (usedOutside || sharedName)
               {
                  plan.KeptInParent.Add(DescribeNode(node));
                  continue;
               }
            }

            moved.Add(node);
         }

         if (!moved.Any(IsActionLike))
         {
            plan.Problems.Add("Select at least one action, condition or event node.");
            return plan;
         }

         var movedGuids = new HashSet<Guid>(moved.Select(node => node.Guid));
         plan.MovedNodes.AddRange(movedGuids);

         // Copy the nodes (same guids: the new graph is a separate file)
         int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue;
         foreach (var node in moved)
         {
            nestedGraph.AddNode(node.Copy(true));

            var display = m_FlowChart.GetNode(node.Guid);
            var bounds = display != null ? display.Bounds : new Rectangle(node.Position.X, node.Position.Y, 100, 50);
            minX = Math.Min(minX, bounds.Left);
            minY = Math.Min(minY, bounds.Top);
            maxX = Math.Max(maxX, bounds.Right);
         }

         plan.Position = new Point(minX, minY);

         // Links inside the selection come along unchanged
         foreach (var link in links)
         {
            if (movedGuids.Contains(link.Source.Guid) && movedGuids.Contains(link.Destination.Guid))
            {
               nestedGraph.AddNode(link.Copy(true));
            }
         }

         // Links crossing the selection become External Connections
         var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         var incoming = new Dictionary<string, ExternalConnection>();
         var outgoing = new Dictionary<string, ExternalConnection>();
         int order = 0;
         int inputY = minY;
         int outputY = minY;

         foreach (var link in links)
         {
            var sourceInside = movedGuids.Contains(link.Source.Guid);
            var destInside = movedGuids.Contains(link.Destination.Guid);
            if (sourceInside == destInside)
            {
               continue;
            }

            ExternalConnection external;
            LinkNode nestedLink;

            if (destInside)
            {
               // Something in the parent feeds the selection: one external per parent socket
               var key = link.Source.Guid + "|" + link.Source.Anchor;
               if (!incoming.TryGetValue(key, out external))
               {
                  var dest = m_ScriptEditor.GetNode(link.Destination.Guid);
                  external = MakeExternal(UniqueExternalName(SocketFriendlyName(dest, link.Destination.Anchor), usedNames), order++);
                  external.Position = new Point(minX - 180, inputY);
                  inputY += 70;
                  incoming[key] = external;
                  nestedGraph.AddNode(external);

                  plan.Connections.Add(new NestedGraphConnection
                  {
                     Incoming = true,
                     ExternalName = external.Name.Default,
                     OutsideGuid = link.Source.Guid.ToString(),
                     OutsideAnchor = link.Source.Anchor
                  });
               }

               nestedLink = new LinkNode(external.Guid, external.Connection, link.Destination.Guid, link.Destination.Anchor);
            }
            else
            {
               // The selection feeds something in the parent: one external per selected socket
               var key = link.Source.Guid + "|" + link.Source.Anchor;
               if (!outgoing.TryGetValue(key, out external))
               {
                  var source = m_ScriptEditor.GetNode(link.Source.Guid);
                  external = MakeExternal(UniqueExternalName(SocketFriendlyName(source, link.Source.Anchor), usedNames), order++);
                  external.Position = new Point(maxX + 120, outputY);
                  outputY += 70;
                  outgoing[key] = external;
                  nestedGraph.AddNode(external);
               }

               plan.Connections.Add(new NestedGraphConnection
               {
                  Incoming = false,
                  ExternalName = external.Name.Default,
                  OutsideGuid = link.Destination.Guid.ToString(),
                  OutsideAnchor = link.Destination.Anchor
               });

               nestedLink = new LinkNode(link.Source.Guid, link.Source.Anchor, external.Guid, external.Connection);
            }

            // Several parent links can share one external; add its link to the new graph once
            if (nestedGraph.Links.Any(existing => existing.Source.Guid == nestedLink.Source.Guid && existing.Source.Anchor == nestedLink.Source.Anchor &&
                                                  existing.Destination.Guid == nestedLink.Destination.Guid && existing.Destination.Anchor == nestedLink.Destination.Anchor))
            {
               continue;
            }

            string reason;
            if (!nestedGraph.VerifyLink(nestedLink, out reason))
            {
               plan.Problems.Add(DescribeSocket(link) + ": " + reason);
               continue;
            }

            nestedGraph.AddNode(nestedLink);
         }

         return plan;
      }

      private static ExternalConnection MakeExternal(string name, int order)
      {
         var external = new ExternalConnection(Guid.NewGuid());
         external.Name.Default = name;
         external.Order.Default = order.ToString(System.Globalization.CultureInfo.InvariantCulture);
         return external;
      }

      /// <summary>Names the generated code can use as they are, unique within the new graph.</summary>
      private static string UniqueExternalName(string friendly, HashSet<string> used)
      {
         var builder = new StringBuilder();
         foreach (var c in friendly ?? string.Empty)
         {
            if (char.IsLetterOrDigit(c) || c == '_') builder.Append(c);
            else if (c == ' ' && builder.Length > 0) builder.Append('_');
         }

         var name = builder.ToString().Trim('_');
         if (name.Length == 0) name = "Connection";
         if (char.IsDigit(name[0])) name = "_" + name;

         var unique = name;
         for (var i = 2; used.Contains(unique); i++)
         {
            unique = name + "_" + i;
         }

         used.Add(unique);
         return unique;
      }

      private static string SocketFriendlyName(EntityNode node, string anchor)
      {
         if (node is LogicNode)
         {
            var logic = (LogicNode)node;
            foreach (var plug in logic.Inputs.Concat(logic.Outputs).Concat(logic.Events))
            {
               if (plug.Name == anchor) return plug.FriendlyName;
            }
         }
         else if (node is EntityMethod)
         {
            var method = (EntityMethod)node;
            if (method.Input.Name == anchor) return method.Input.FriendlyName;
            if (method.Output.Name == anchor) return method.Output.FriendlyName;
         }
         else if (node is EntityEvent)
         {
            foreach (var plug in ((EntityEvent)node).Outputs)
            {
               if (plug.Name == anchor) return plug.FriendlyName;
            }
         }

         if (node != null)
         {
            foreach (var p in node.Parameters)
            {
               if (p.Name == anchor) return p.FriendlyName;
            }

            if (node.Instance.Name == anchor) return node.Instance.FriendlyName;
         }

         return anchor;
      }

      private string DescribeNode(EntityNode node)
      {
         var display = m_FlowChart.GetNode(node.Guid);
         var name = display != null && !string.IsNullOrEmpty(display.Name) ? display.Name : ScriptEditor.FindNodeType(node);

         if (node is LocalNode && !string.IsNullOrEmpty(((LocalNode)node).Name.Default))
         {
            name = "variable \"" + ((LocalNode)node).Name.Default + "\"";
         }

         return name;
      }

      private string DescribeSocket(LinkNode link)
      {
         var source = m_ScriptEditor.GetNode(link.Source.Guid);
         var dest = m_ScriptEditor.GetNode(link.Destination.Guid);
         return DescribeNode(source) + " (" + SocketFriendlyName(source, link.Source.Anchor) + ") to " +
                DescribeNode(dest) + " (" + SocketFriendlyName(dest, link.Destination.Anchor) + ")";
      }

      /// <summary>Deletes the given nodes (and their links) as one undo step.</summary>
      public void DeleteNodes(IEnumerable<Guid> guids)
      {
         m_FlowChart.DeselectAll();
         m_FlowChart.SelectNodes(guids.ToArray());
         DeleteSelectedNodes();
      }

      /// <summary>
      /// Adds the nested graph node and connects it to the parent nodes. Returns the connections that could not
      /// be made.
      /// </summary>
      public List<string> AddNestedNode(EntityNode nestedNode, Point position, IEnumerable<NestedGraphConnection> connections)
      {
         var failed = new List<string>();

         nestedNode.Position = position;

         var batch = new Patch.Batch("Make Nested Graph");
         m_ScriptEditor.AddNode(nestedNode);
         batch.Add(new Patch.EntityNode("", null, nestedNode.Copy(true)));

         foreach (var connection in connections)
         {
            Guid outsideGuid;
            try
            {
               outsideGuid = new Guid(connection.OutsideGuid);
            }
            catch (Exception)
            {
               continue;
            }

            var outside = m_ScriptEditor.GetNode(outsideGuid);
            var socket = FindNestedSocket(nestedNode, connection.ExternalName);

            if (outside == null || socket == null)
            {
               failed.Add(connection.ExternalName);
               continue;
            }

            var link = connection.Incoming
               ? new LinkNode(outsideGuid, connection.OutsideAnchor, nestedNode.Guid, socket)
               : new LinkNode(nestedNode.Guid, socket, outsideGuid, connection.OutsideAnchor);

            m_ScriptEditor.AddNode(link);
            if (null != m_ScriptEditor.GetNode(link.Guid))
            {
               batch.Add(new Patch.EntityNode("", null, link));
            }
            else
            {
               failed.Add(connection.ExternalName);
            }
         }

         m_Dirty = true;
         uScript.Instance.RegisterUndo(batch);
         PatchDisplay(batch, new List<Guid> { nestedNode.Guid }, true);

         return failed;
      }

      /// <summary>The socket of the nested node created for the External Connection with this name.</summary>
      private static string FindNestedSocket(EntityNode nestedNode, string externalName)
      {
         if (!(nestedNode is LogicNode))
         {
            return null;
         }

         var logic = (LogicNode)nestedNode;

         foreach (var plug in logic.Inputs.Concat(logic.Outputs).Concat(logic.Events))
         {
            if (plug.FriendlyName == externalName || plug.Name == externalName) return plug.Name;
         }

         foreach (var p in logic.Parameters)
         {
            if (p.FriendlyName == externalName || p.Name == externalName) return p.Name;
         }

         if (logic.EventParameters != null)
         {
            foreach (var p in logic.EventParameters)
            {
               if (p.FriendlyName == externalName || p.Name == externalName) return p.Name;
            }
         }

         return null;
      }
   }
}
