// Nodes as readable text, for AI assistants and for people.
//
//    Right-click > Copy as Text (for AI)      copies the selected nodes and their links as text.
//    Right-click > Paste Text Nodes            turns that kind of text back into real nodes and links.
//
// The format, one item per line ("#" starts a comment line):
//
//    [USCRIPT-TEXT 1]
//    node 863 uScriptAct_Delay "Delay" @ 120,40
//       Duration = "0.1"
//    var 94 "Dead" System.Boolean = "false" exposed @ 80,200
//    listbox 872 System.String[] gives System.String key "Headshot" default "False" addkey @ 300,200
//    property 878 UnityEngine.UI.Image fillAmount @ 500,40
//    method 625 HeroNetwork "SendSignalToOthers" @ 600,40
//    event 2 uScript_Global "Global Events" @ 0,0
//    external 7 "Damage In" @ 0,100
//    owner 6 @ 0,300
//    comment 5 "Title" "Body text" @ 0,-100
//    link 863.AfterDelay -> 93.In
//
// Numbers after the kind are ids, used only by the link lines in the same text.
// Positions ("@ x,y") are optional; nodes without one are laid out in rows.
// Socket names in links can be the real name (AfterDelay) or the name shown on the node ("After Delay", in quotes).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Detox.Drawing;

namespace Detox.ScriptEditor
{
   public static class uScriptTextGraph
   {
      public const string Header = "[USCRIPT-TEXT 1]";

      public static bool IsText(string text)
      {
         return text != null && text.TrimStart().StartsWith("[USCRIPT-TEXT", StringComparison.Ordinal);
      }

      // =========================================================================================== writing

      /// <summary>The selected nodes and the links between them, as text. Ids are the graph's node numbers.</summary>
      public static string Write(ScriptEditor script, string graphName, ICollection<Guid> selected)
      {
         EntityNode[] all = script.DirectEntityNodes;
         var idOf = new Dictionary<Guid, int>();
         for (int i = 0; i < all.Length; i++) idOf[all[i].Guid] = i;

         var text = new StringBuilder();
         text.AppendLine(Header + (string.IsNullOrEmpty(graphName) ? "" : "  graph: " + graphName));
         text.AppendLine("# uScript nodes as text. Ids are this graph's node numbers (View > Go to Node #).");

         var outside = new List<string>();

         for (int i = 0; i < all.Length; i++)
         {
            EntityNode node = all[i];
            if (node is LinkNode || !selected.Contains(node.Guid)) continue;

            string line = Describe(node, i);
            if (line == null)
            {
               text.AppendLine("# " + i + ": a node of an unknown kind was left out");
               continue;
            }

            text.AppendLine(line + " @ " + node.Position.X + "," + node.Position.Y);

            foreach (string valueLine in Values(node))
            {
               text.AppendLine("   " + valueLine);
            }
         }

         foreach (LinkNode link in script.Links)
         {
            int from, to;
            if (!idOf.TryGetValue(link.Source.Guid, out from) || !idOf.TryGetValue(link.Destination.Guid, out to)) continue;

            bool fromIn = selected.Contains(link.Source.Guid);
            bool toIn = selected.Contains(link.Destination.Guid);
            string line = "link " + from + "." + Anchor(link.Source.Anchor) + " -> " + to + "." + Anchor(link.Destination.Anchor);

            if (fromIn && toIn)
            {
               text.AppendLine(line);
            }
            else if (fromIn || toIn)
            {
               int other = fromIn ? to : from;
               outside.Add("# " + line + "   (" + other + " is not selected: " + ShortName(all[other]) + ")");
            }
         }

         if (outside.Count > 0)
         {
            text.AppendLine("# Links to nodes outside the selection (not pasted):");
            foreach (string line in outside) text.AppendLine(line);
         }

         return text.ToString();
      }

      private static string Describe(EntityNode node, int id)
      {
         if (node is LogicNode)
         {
            var logic = (LogicNode)node;
            return "node " + id + " " + logic.Type + " " + Quote(logic.FriendlyName);
         }

         if (ListBox.Is(node))
         {
            var box = (EntityProperty)node;
            ListBox.Settings settings = ListBox.Read(box);
            var line = new StringBuilder("listbox " + id + " " + box.Instance.Type + " gives " + box.Parameter.Type);
            line.Append(settings.FindBy == uScript_ListBoxFindBy.Key ? " key " : " index ").Append(Quote(settings.Selector ?? ""));
            if (settings.SelectorIsVariable) line.Append(" variable");
            if (!string.IsNullOrEmpty(settings.Default)) line.Append(" default ").Append(Quote(settings.Default));
            if (settings.ShowDefaultSocket) line.Append(" defaultsocket");
            if (settings.AddIfMissing) line.Append(" addkey");
            return line.ToString();
         }

         if (node is EntityProperty)
         {
            var property = (EntityProperty)node;
            return "property " + id + " " + property.ComponentType + " " + property.Parameter.Name;
         }

         if (node is EntityMethod)
         {
            var method = (EntityMethod)node;
            return "method " + id + " " + method.ComponentType + " " + Quote(method.Input.FriendlyName);
         }

         if (node is EntityEvent)
         {
            var e = (EntityEvent)node;
            return "event " + id + " " + e.ComponentType + " " + Quote(e.FriendlyType);
         }

         if (node is LocalNode)
         {
            var local = (LocalNode)node;
            return "var " + id + " " + Quote(local.Name.Default) + " " + local.Value.Type + " = " + Quote(local.Value.Default) +
                   (local.Externaled.Default == "true" ? " exposed" : "");
         }

         if (node is ExternalConnection)
         {
            return "external " + id + " " + Quote(((ExternalConnection)node).Name.Default);
         }

         if (node is OwnerConnection)
         {
            return "owner " + id;
         }

         if (node is CommentNode)
         {
            var comment = (CommentNode)node;
            return "comment " + id + " " + Quote(comment.TitleText.Default) + " " + Quote(comment.BodyText.Default);
         }

         return null;
      }

      /// <summary>Input values typed on the node (not the ones that come from links).</summary>
      private static IEnumerable<string> Values(EntityNode node)
      {
         if (node is LocalNode || node is CommentNode || node is OwnerConnection || node is ExternalConnection || ListBox.Is(node)) yield break;

         foreach (Parameter p in node.Parameters)
         {
            if (!p.Input || string.IsNullOrEmpty(p.Default)) continue;
            yield return p.Name + " = " + Quote(p.Default);
         }

         if (node.Instance != Parameter.Empty && !string.IsNullOrEmpty(node.Instance.Default))
         {
            yield return node.Instance.Name + " = " + Quote(node.Instance.Default);
         }
      }

      private static string ShortName(EntityNode node)
      {
         string line = Describe(node, 0);
         if (line == null) return "?";
         int space = line.IndexOf(' ', line.IndexOf(' ') + 1);
         return line.Substring(0, line.IndexOf(' ')) + line.Substring(space);
      }

      private static string Anchor(string anchor)
      {
         if (string.IsNullOrEmpty(anchor)) return "\"\"";
         foreach (char c in anchor)
         {
            if (!char.IsLetterOrDigit(c) && c != '_') return Quote(anchor);
         }

         return anchor;
      }

      private static string Quote(string text)
      {
         text = text ?? "";
         return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
      }

      // =========================================================================================== reading

      private class Item
      {
         public EntityNode Node;
         public bool HasPosition;
         public int Line;
      }

      /// <summary>
      /// Builds a small script from the text. Returns null when the text has no usable nodes.
      /// Problems (unknown node types, wrong socket names) are added to warnings; the rest is still built.
      /// </summary>
      public static ScriptEditor Read(string text, ScriptEditor target, List<string> warnings)
      {
         var result = new ScriptEditor("", target.EntityDescs, target.LogicNodes);
         var items = new Dictionary<string, Item>();
         var order = new List<Item>();
         var links = new List<KeyValuePair<int, string>>();
         Item current = null;

         string[] lines = text.Replace("\r", "").Split('\n');

         for (int n = 0; n < lines.Length; n++)
         {
            string raw = lines[n];
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("[USCRIPT-TEXT", StringComparison.Ordinal)) continue;

            try
            {
               List<string> words = Split(line);
               string kind = words[0].ToLowerInvariant();

               if (kind == "link")
               {
                  links.Add(new KeyValuePair<int, string>(n + 1, line));
                  current = null;
                  continue;
               }

               // a value line under a node: Name = "value"
               if (words.Count >= 3 && words[1] == "=" && (raw.StartsWith(" ") || raw.StartsWith("\t")) && current != null)
               {
                  if (!SetValue(ref current.Node, words[0], words[2]))
                  {
                     warnings.Add("Line " + (n + 1) + ": this node has no value called \"" + words[0] + "\".");
                  }

                  continue;
               }

               if (words.Count < 2)
               {
                  warnings.Add("Line " + (n + 1) + ": not understood: " + line);
                  continue;
               }

               string id = words[1];
               Point position;
               bool hasPosition = TakePosition(words, out position);

               EntityNode node;
               string problem;
               if (!Create(kind, words, target, out node, out problem))
               {
                  warnings.Add("Line " + (n + 1) + ": " + problem);
                  current = null;
                  continue;
               }

               if (items.ContainsKey(id))
               {
                  warnings.Add("Line " + (n + 1) + ": id " + id + " is used twice; the second one was skipped.");
                  current = null;
                  continue;
               }

               node.Guid = Guid.NewGuid();
               node.Position = position;
               current = new Item { Node = node, HasPosition = hasPosition, Line = n + 1 };
               items[id] = current;
               order.Add(current);
            }
            catch (Exception e)
            {
               warnings.Add("Line " + (n + 1) + ": " + e.Message);
               current = null;
            }
         }

         if (order.Count == 0) return null;

         Layout(order);

         foreach (Item item in order)
         {
            result.AddNode(item.Node);
         }

         foreach (KeyValuePair<int, string> pair in links)
         {
            LinkNode link;
            string problem;
            if (!CreateLink(pair.Value, items, out link, out problem))
            {
               warnings.Add("Line " + pair.Key + ": " + problem);
               continue;
            }

            string reason;
            if (!result.VerifyLink(link, out reason))
            {
               warnings.Add("Line " + pair.Key + ": link not made: " + reason);
               continue;
            }

            result.AddNode(link);
         }

         return result;
      }

      private static bool Create(string kind, List<string> w, ScriptEditor target, out EntityNode node, out string problem)
      {
         node = null;
         problem = null;

         switch (kind)
         {
            case "node":
            {
               string type = Word(w, 2);
               foreach (LogicNode logic in target.LogicNodes)
               {
                  if (logic.Type == type)
                  {
                     node = logic.Copy(false);
                     return true;
                  }
               }

               string friendly = Word(w, 3) ?? type;
               foreach (LogicNode logic in target.LogicNodes)
               {
                  if (string.Equals(logic.FriendlyName, friendly, StringComparison.OrdinalIgnoreCase))
                  {
                     node = logic.Copy(false);
                     return true;
                  }
               }

               problem = "no node type \"" + type + "\" in this project.";
               return false;
            }

            case "var":
            {
               // var <id> "Name" <Type> = "value" [exposed]
               string name = Word(w, 2) ?? "";
               string type = Word(w, 3);
               if (type == null)
               {
                  problem = "a variable needs a type, for example System.Single.";
                  return false;
               }

               string value = Word(w, 4) == "=" ? (Word(w, 5) ?? "") : "";
               var local = new LocalNode(name, type, value);
               if (w.Contains("exposed"))
               {
                  Parameter exposed = local.Externaled;
                  exposed.Default = "true";
                  local.Externaled = exposed;
               }

               node = local;
               return true;
            }

            case "listbox":
            {
               // listbox <id> <ListType> gives <ValueType> key|index "<selector>" [variable] [default "x"] [defaultsocket] [addkey]
               string listType = Word(w, 2);
               if (listType == null || !listType.EndsWith("[]", StringComparison.Ordinal))
               {
                  problem = "a List Box needs a list type, for example System.String[].";
                  return false;
               }

               EntityProperty box = ListBox.Create(listType);
               ListBox.Settings settings = ListBox.Read(box);
               string gives = null;

               for (int i = 3; i < w.Count; i++)
               {
                  switch (w[i].ToLowerInvariant())
                  {
                     case "gives": gives = Word(w, ++i); break;
                     case "key": settings.FindBy = uScript_ListBoxFindBy.Key; settings.Selector = Word(w, ++i) ?? ""; break;
                     case "index": settings.FindBy = uScript_ListBoxFindBy.Index; settings.Selector = Word(w, ++i) ?? "0"; break;
                     case "variable": settings.SelectorIsVariable = true; break;
                     case "default": settings.Default = Word(w, ++i) ?? ""; break;
                     case "defaultsocket": settings.ShowDefaultSocket = true; break;
                     case "addkey": settings.AddIfMissing = true; break;
                  }
               }

               if (!ListBox.IsStringList(box)) settings.FindBy = uScript_ListBoxFindBy.Index;
               box.ComponentType = ListBox.Write(settings);

               if (gives != null && ListBox.IsStringList(box) && gives != box.Parameter.Type)
               {
                  Parameter value = box.Parameter;
                  value.Type = gives;
                  value.AutoLinkType = gives;
                  box.Parameter = value;
               }

               node = box;
               return true;
            }

            case "property":
            case "method":
            case "event":
            {
               string component = Word(w, 2);
               string name = Word(w, 3);

               foreach (EntityDesc desc in target.EntityDescs)
               {
                  if (desc.Type != component) continue;

                  if (kind == "property" && desc.Properties != null)
                  {
                     foreach (EntityProperty p in desc.Properties)
                     {
                        if (p.Parameter.Name == name || p.Parameter.FriendlyName == name) { node = p.Copy(false); return true; }
                     }
                  }

                  if (kind == "method" && desc.Methods != null)
                  {
                     foreach (EntityMethod m in desc.Methods)
                     {
                        if (m.Input.FriendlyName == name) { node = m.Copy(false); return true; }
                     }

                     foreach (EntityMethod m in desc.Methods)
                     {
                        if (m.Input.Name == name) { node = m.Copy(false); return true; }
                     }
                  }

                  if (kind == "event" && desc.Events != null)
                  {
                     foreach (EntityEvent e in desc.Events)
                     {
                        if (e.FriendlyType == name || name == null) { node = e.Copy(false); return true; }
                     }
                  }
               }

               problem = "no " + kind + " \"" + name + "\" on " + component + " in this project.";
               return false;
            }

            case "external":
            {
               var external = new ExternalConnection(Guid.NewGuid());
               Parameter name = external.Name;
               name.Default = Word(w, 2) ?? "";
               external.Name = name;
               node = external;
               return true;
            }

            case "owner":
               node = new OwnerConnection(Guid.NewGuid());
               return true;

            case "comment":
            {
               var comment = new CommentNode(Word(w, 2) ?? "Comment");
               Parameter title = comment.TitleText;
               title.Default = Word(w, 2) ?? "Comment";
               comment.TitleText = title;
               Parameter body = comment.BodyText;
               body.Default = Word(w, 3) ?? "";
               comment.BodyText = body;
               node = comment;
               return true;
            }
         }

         problem = "unknown kind \"" + kind + "\" (use node, var, listbox, property, method, event, external, owner, comment or link).";
         return false;
      }

      private static bool SetValue(ref EntityNode node, string name, string value)
      {
         Parameter[] parameters = node.Parameters;
         for (int i = 0; i < parameters.Length; i++)
         {
            if (parameters[i].Name == name || parameters[i].FriendlyName == name)
            {
               parameters[i].Default = value;
               node.Parameters = parameters;
               return true;
            }
         }

         if (node.Instance != Parameter.Empty && (node.Instance.Name == name || node.Instance.FriendlyName == name))
         {
            Parameter instance = node.Instance;
            instance.Default = value;
            node.Instance = instance;
            return true;
         }

         return false;
      }

      private static bool CreateLink(string line, Dictionary<string, Item> items, out LinkNode link, out string problem)
      {
         link = new LinkNode();
         problem = null;

         // link <id>.<socket> -> <id>.<socket>
         string body = line.Substring(4).Trim();
         int arrow = body.IndexOf("->", StringComparison.Ordinal);
         if (arrow < 0)
         {
            problem = "a link needs \"->\", for example: link 1.Out -> 2.In";
            return false;
         }

         string fromId, fromSocket, toId, toSocket;
         if (!SplitEnd(body.Substring(0, arrow), out fromId, out fromSocket) || !SplitEnd(body.Substring(arrow + 2), out toId, out toSocket))
         {
            problem = "write each end as id.socket, for example 863.AfterDelay";
            return false;
         }

         Item from, to;
         if (!items.TryGetValue(fromId, out from) || !items.TryGetValue(toId, out to))
         {
            problem = "link to an id that is not in the text (" + (items.ContainsKey(fromId) ? toId : fromId) + ").";
            return false;
         }

         link = new LinkNode(from.Node.Guid, Socket(from.Node, fromSocket), to.Node.Guid, Socket(to.Node, toSocket));
         return true;
      }

      private static bool SplitEnd(string end, out string id, out string socket)
      {
         end = end.Trim();
         int dot = end.IndexOf('.');
         id = dot > 0 ? end.Substring(0, dot).Trim() : null;
         socket = dot > 0 ? end.Substring(dot + 1).Trim() : null;
         if (socket != null && socket.Length >= 2 && socket[0] == '"' && socket[socket.Length - 1] == '"')
         {
            socket = socket.Substring(1, socket.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
         }

         return !string.IsNullOrEmpty(id) && socket != null;
      }

      /// <summary>The real socket name for a real or shown name. Unknown names are kept as written.</summary>
      private static string Socket(EntityNode node, string name)
      {
         var sockets = new List<KeyValuePair<string, string>>();

         foreach (Parameter p in node.Parameters) sockets.Add(new KeyValuePair<string, string>(p.Name, p.FriendlyName));
         if (node.Instance != Parameter.Empty) sockets.Add(new KeyValuePair<string, string>(node.Instance.Name, node.Instance.FriendlyName));

         if (node is LogicNode)
         {
            var logic = (LogicNode)node;
            foreach (Plug p in logic.Inputs ?? new Plug[0]) sockets.Add(new KeyValuePair<string, string>(p.Name, p.FriendlyName));
            foreach (Plug p in logic.Outputs ?? new Plug[0]) sockets.Add(new KeyValuePair<string, string>(p.Name, p.FriendlyName));
            foreach (Plug p in logic.Events ?? new Plug[0]) sockets.Add(new KeyValuePair<string, string>(p.Name, p.FriendlyName));
         }
         else if (node is EntityMethod)
         {
            var method = (EntityMethod)node;
            sockets.Add(new KeyValuePair<string, string>(method.Input.Name, method.Input.FriendlyName));
            sockets.Add(new KeyValuePair<string, string>(method.Output.Name, method.Output.FriendlyName));
         }
         else if (node is EntityEvent)
         {
            foreach (Plug p in ((EntityEvent)node).Outputs ?? new Plug[0]) sockets.Add(new KeyValuePair<string, string>(p.Name, p.FriendlyName));
         }

         if (ListBox.Is(node))
         {
            foreach (Parameter p in ListBox.ExtraParameters((EntityProperty)node)) sockets.Add(new KeyValuePair<string, string>(p.Name, p.FriendlyName));
         }

         foreach (var s in sockets) if (s.Key == name) return s.Key;
         foreach (var s in sockets) if (string.Equals((s.Value ?? "").Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)) return s.Key;
         // letters and digits only, so "Equal To" finds "(Equal To)   =" and "EqualTo"
         string wanted = Letters(name);
         foreach (var s in sockets) if (Letters(s.Key) == wanted) return s.Key;
         foreach (var s in sockets) if (Letters(s.Value) == wanted) return s.Key;

         return name;
      }

      private static string Letters(string text)
      {
         var letters = new StringBuilder();
         foreach (char c in text ?? "")
         {
            if (char.IsLetterOrDigit(c)) letters.Append(char.ToLowerInvariant(c));
         }

         return letters.ToString();
      }

      /// <summary>Nodes without "@ x,y" go in rows: signal nodes on top, variables and boxes below.</summary>
      private static void Layout(List<Item> items)
      {
         int maxY = int.MinValue, minX = int.MaxValue;
         foreach (Item item in items)
         {
            if (!item.HasPosition) continue;
            maxY = Math.Max(maxY, item.Node.Position.Y);
            minX = Math.Min(minX, item.Node.Position.X);
         }

         int baseX = minX == int.MaxValue ? 0 : minX;
         int baseY = maxY == int.MinValue ? 0 : maxY + 220;
         int nodeX = baseX, dataX = baseX;

         foreach (Item item in items)
         {
            if (item.HasPosition) continue;

            bool data = item.Node is LocalNode || item.Node is EntityProperty || item.Node is OwnerConnection;
            if (data)
            {
               item.Node.Position = new Point(dataX, baseY + 170);
               dataX += 140;
            }
            else
            {
               item.Node.Position = new Point(nodeX, baseY);
               nodeX += 240;
            }
         }
      }

      private static bool TakePosition(List<string> words, out Point position)
      {
         position = Point.Empty;
         int at = words.IndexOf("@");
         if (at < 0 || at + 1 >= words.Count) return false;

         string[] xy = words[at + 1].Split(',');
         int x = 0, y = 0;
         bool ok = xy.Length == 2 &&
                   int.TryParse(xy[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &
                   int.TryParse(xy[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
         if (ok) position = new Point(x, y);

         words.RemoveRange(at, Math.Min(2, words.Count - at));
         return ok;
      }

      private static string Word(List<string> words, int index)
      {
         return index < words.Count ? words[index] : null;
      }

      /// <summary>Splits a line into words; "quoted text" is one word; "=" and "@" are words of their own.</summary>
      private static List<string> Split(string line)
      {
         var words = new List<string>();
         int i = 0;

         while (i < line.Length)
         {
            char c = line[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '"')
            {
               var word = new StringBuilder();
               i++;
               while (i < line.Length && line[i] != '"')
               {
                  if (line[i] == '\\' && i + 1 < line.Length)
                  {
                     i++;
                     word.Append(line[i] == 'n' ? '\n' : line[i]);
                  }
                  else
                  {
                     word.Append(line[i]);
                  }

                  i++;
               }

               i++;
               words.Add(word.ToString());
               continue;
            }

            if (c == '=' || c == '@')
            {
               words.Add(c.ToString());
               i++;
               continue;
            }

            int start = i;
            while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '"' && line[i] != '=' && line[i] != '@') i++;
            words.Add(line.Substring(start, i - start));
         }

         return words;
      }
   }
}
