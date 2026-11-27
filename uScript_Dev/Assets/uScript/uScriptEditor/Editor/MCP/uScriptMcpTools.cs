// uScript tools for AI assistants, through the "MCP for Unity" package.
// All tools only READ graph files. They never change graphs, generated code or anything else in the project.

using System;
using System.Collections.Generic;
using System.Linq;
using CommentNode = Detox.ScriptEditor.CommentNode;
using EntityEvent = Detox.ScriptEditor.EntityEvent;
using EntityMethod = Detox.ScriptEditor.EntityMethod;
using EntityNode = Detox.ScriptEditor.EntityNode;
using EntityProperty = Detox.ScriptEditor.EntityProperty;
using ExternalConnection = Detox.ScriptEditor.ExternalConnection;
using LinkNode = Detox.ScriptEditor.LinkNode;
using LocalNode = Detox.ScriptEditor.LocalNode;
using LogicNode = Detox.ScriptEditor.LogicNode;
using OwnerConnection = Detox.ScriptEditor.OwnerConnection;
using Parameter = Detox.ScriptEditor.Parameter;
using Plug = Detox.ScriptEditor.Plug;
using ScriptEditor = Detox.ScriptEditor.ScriptEditor;
using ErrorResponse = MCPForUnity.Editor.Helpers.ErrorResponse;
using McpForUnityToolAttribute = MCPForUnity.Editor.Tools.McpForUnityToolAttribute;
using SuccessResponse = MCPForUnity.Editor.Helpers.SuccessResponse;
using ToolParameterAttribute = MCPForUnity.Editor.Tools.ToolParameterAttribute;
using Newtonsoft.Json.Linq;

namespace uScriptMcp
{
   internal static class McpParams
   {
      public static string Text(JObject p, string name)
      {
         JToken token = p == null ? null : p[name];
         if (token == null || token.Type == JTokenType.Null) return null;
         string value = token.ToString().Trim();
         return value.Length == 0 ? null : value;
      }

      public static int Int(JObject p, string name, int fallback, int min, int max)
      {
         string text = Text(p, name);
         int value;
         if (text == null || !int.TryParse(text, out value)) value = fallback;
         return Math.Max(min, Math.Min(max, value));
      }

      public static bool Bool(JObject p, string name, bool fallback)
      {
         string text = Text(p, name);
         bool value;
         return text != null && bool.TryParse(text, out value) ? value : fallback;
      }

      public static bool Contains(string haystack, string needle)
      {
         return !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
      }

      /// <summary>Loads every graph whose name contains the filter (or all graphs). Unreadable graphs are reported in errors.</summary>
      public static List<GraphView> LoadGraphs(string nameFilter, List<string> errors)
      {
         var views = new List<GraphView>();

         foreach (string path in uScriptMcpGraphFiles.AllGraphPaths())
         {
            if (nameFilter != null && !Contains(uScriptMcpGraphFiles.GraphName(path), nameFilter)) continue;

            string error;
            GraphView view = uScriptMcpGraphFiles.Load(path, out error);
            if (view != null) views.Add(view);
            else errors.Add(error);
         }

         return views;
      }
   }

   // ------------------------------------------------------------------------------------------------

   [McpForUnityTool("uscript_list_graphs", Description =
      "Lists the uScript visual-scripting graphs (.uscript files) in the Unity project. Read-only. " +
      "Use it first to find graph names for the other uscript_ tools.")]
   public static class UScriptListGraphsTool
   {
      public class Parameters
      {
         [ToolParameter("Only list graphs whose name contains this text (optional).", Required = false)]
         public string filter { get; set; }
      }

      public static object HandleCommand(JObject @params)
      {
         try
         {
            string filter = McpParams.Text(@params, "filter");
            var graphs = new List<object>();

            foreach (string path in uScriptMcpGraphFiles.AllGraphPaths())
            {
               string name = uScriptMcpGraphFiles.GraphName(path);
               if (filter != null && !McpParams.Contains(name, filter)) continue;

               var info = new System.IO.FileInfo(System.IO.Path.GetFullPath(path));
               graphs.Add(new
               {
                  name,
                  path,
                  size_kb = (int)Math.Ceiling(info.Length / 1024.0),
                  modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
               });
            }

            return new SuccessResponse(graphs.Count + " uScript graph(s).", new { count = graphs.Count, graphs });
         }
         catch (Exception e)
         {
            return new ErrorResponse("Could not list uScript graphs: " + e.Message);
         }
      }
   }

   // ------------------------------------------------------------------------------------------------

   [McpForUnityTool("uscript_read_graph", Description =
      "Reads one uScript graph and describes its nodes, variables, values and connections. Read-only. " +
      "Each node has an id; it is the same number uScript uses in the graph's generated C# code " +
      "(for example Relay_In_865 or logic_..._865 in a console stack trace means id 865), as long as the graph was saved after its last change. " +
      "Big graphs are returned in pages: use search, ids, offset and limit.")]
   public static class UScriptReadGraphTool
   {
      public class Parameters
      {
         [ToolParameter("Graph name (for example \"Zombie_Hero\") or its path.")]
         public string graph { get; set; }

         [ToolParameter("Only nodes whose title, type, comment or values contain this text (optional).", Required = false)]
         public string search { get; set; }

         [ToolParameter("Only these node ids, comma separated, for example \"865,866\" (optional).", Required = false)]
         public string ids { get; set; }

         [ToolParameter("Also include the nodes directly connected to the found nodes (default: true when ids are given, otherwise false).", Required = false)]
         public bool? include_connected { get; set; }

         [ToolParameter("Skip this many nodes, for paging (default 0).", Required = false)]
         public int? offset { get; set; }

         [ToolParameter("Most nodes to return (default 150, max 500).", Required = false)]
         public int? limit { get; set; }
      }

      public static object HandleCommand(JObject @params)
      {
         try
         {
            string error;
            string path = uScriptMcpGraphFiles.Resolve(McpParams.Text(@params, "graph"), out error);
            if (path == null) return new ErrorResponse(error);

            GraphView view = uScriptMcpGraphFiles.Load(path, out error);
            if (view == null) return new ErrorResponse(error);

            string search = McpParams.Text(@params, "search");
            string idsText = McpParams.Text(@params, "ids");
            bool connected = McpParams.Bool(@params, "include_connected", idsText != null);
            int offset = McpParams.Int(@params, "offset", 0, 0, int.MaxValue);
            int limit = McpParams.Int(@params, "limit", 150, 1, 500);

            List<int> selected = Select(view, search, idsText, connected);
            List<object> nodes = selected.Skip(offset).Take(limit).Select(id => Describe(view, id)).ToList();
            int next = offset + nodes.Count;

            var counts = view.Entities.GroupBy(e => GraphView.Kind(e)).ToDictionary(g => g.Key, g => g.Count());

            var exposedVariables = view.Entities.OfType<LocalNode>()
               .Where(v => v.Externaled.Default == "true")
               .GroupBy(v => v.Name.Default + "|" + v.Value.Type) // one uScript variable can appear as many nodes
               .Select(g => new { name = g.First().Name.Default, type = g.First().Value.Type, value = GraphView.ValueOf(g.First().Value) })
               .OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase)
               .ToList();

            var connections = view.Entities.OfType<ExternalConnection>()
               .Select(x => new { id = view.Id(x.Guid), name = x.Name.Default, description = x.Description.Default })
               .ToList();

            var data = new
            {
               graph = new
               {
                  name = view.Name,
                  path = view.Path,
                  friendly_name = view.Script.FriendlyName.Default,
                  description = view.Script.Description.Default,
                  generated_code_out_of_date = view.Script.GeneratedCodeIsStale,
                  counts,
                  broken_links = view.BrokenLinks.Count,
                  exposed_variables = exposedVariables,
                  graph_connections = connections
               },
               total_matching = selected.Count,
               returned = nodes.Count,
               next_offset = next < selected.Count ? (int?)next : null,
               nodes
            };

            string message = "Graph " + view.Name + ": " + nodes.Count + " of " + selected.Count + " node(s)" +
               (next < selected.Count ? ". More with offset " + next + "." : ".");

            return new SuccessResponse(message, data);
         }
         catch (Exception e)
         {
            return new ErrorResponse("Could not read the graph: " + e.Message);
         }
      }

      private static List<int> Select(GraphView view, string search, string idsText, bool connected)
      {
         IEnumerable<int> all = Enumerable.Range(0, view.Entities.Length).Where(i => !(view.Entities[i] is LinkNode));
         var picked = new List<int>();

         if (idsText != null)
         {
            foreach (string part in idsText.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
               int id;
               if (int.TryParse(part, out id) && id >= 0 && id < view.Entities.Length && !(view.Entities[id] is LinkNode)) picked.Add(id);
            }
         }
         else if (search != null)
         {
            picked.AddRange(all.Where(i => Matches(view, i, search)));
         }
         else
         {
            return all.ToList();
         }

         if (search != null && idsText != null)
         {
            picked = picked.Where(i => Matches(view, i, search)).ToList();
         }

         if (connected)
         {
            var withNeighbours = new HashSet<int>(picked);
            foreach (int id in picked)
            {
               foreach (LinkNode link in view.In(id)) withNeighbours.Add(view.Id(link.Source.Guid));
               foreach (LinkNode link in view.Out(id)) withNeighbours.Add(view.Id(link.Destination.Guid));
            }

            picked = withNeighbours.ToList();
         }

         return picked.Distinct().OrderBy(i => i).ToList();
      }

      internal static bool Matches(GraphView view, int id, string search)
      {
         EntityNode entity = view.Entities[id];

         if (McpParams.Contains(GraphView.Title(entity), search)) return true;
         if (McpParams.Contains(GraphView.TypeName(entity), search)) return true;
         if (McpParams.Contains(entity.Comment.Default, search)) return true;
         if (entity is CommentNode && McpParams.Contains(((CommentNode)entity).BodyText.Default, search)) return true;

         foreach (Parameter p in GraphView.SafeParameters(entity))
         {
            if (McpParams.Contains(p.Default, search)) return true;
         }

         return false;
      }

      internal static object Describe(GraphView view, int id)
      {
         EntityNode entity = view.Entities[id];
         var node = new Dictionary<string, object>();

         node["id"] = id;
         node["kind"] = GraphView.Kind(entity);
         node["title"] = GraphView.Title(entity);

         string type = GraphView.TypeName(entity);
         if (!string.IsNullOrEmpty(type)) node["type"] = type;

         string comment = entity.Comment.Default;
         if (!string.IsNullOrEmpty(comment)) node["comment"] = comment;

         if (Detox.ScriptEditor.ListBox.Is(entity))
         {
            var box = (EntityProperty)entity;
            var settings = Detox.ScriptEditor.ListBox.Read(box);
            var info = new Dictionary<string, object>();
            info["find_by"] = settings.FindBy.ToString();
            info[settings.FindBy == uScript_ListBoxFindBy.Key ? "key" : "index"] = settings.Selector ?? "";
            if (settings.SelectorIsVariable) info["selector_is_variable_name"] = true;
            if (Detox.ScriptEditor.ListBox.SocketLinked(view.Script, box, Detox.ScriptEditor.ListBox.SelectorName)) info["selector_from_socket"] = true;
            info["gives"] = box.Parameter.Type;
            if (!string.IsNullOrEmpty(settings.Default)) info["default_if_missing"] = settings.Default;
            if (settings.ShowDefaultSocket) info["default_socket_shown"] = true;
            if (settings.AddIfMissing) info["add_key_if_missing"] = true;
            node["list_box"] = info;
         }

         var inputNames = new HashSet<string>();

         if (entity is CommentNode)
         {
            node["text"] = ((CommentNode)entity).BodyText.Default;
         }
         else if (entity is LocalNode)
         {
            var local = (LocalNode)entity;
            node["value"] = GraphView.ValueOf(local.Value);
            if (local.Externaled.Default == "true") node["exposed"] = true;
         }
         else
         {
            var values = new Dictionary<string, object>();

            foreach (Parameter p in GraphView.SafeParameters(entity))
            {
               if (!p.Input) continue;
               inputNames.Add(p.Name);

               List<string> sources = view.DescribeInput(id, p, null);
               if (sources.Count == 0) continue;

               string key = string.IsNullOrEmpty(p.FriendlyName) ? p.Name : p.FriendlyName;
               values[key] = sources.Count == 1 ? (object)sources[0] : sources;
            }

            if (values.Count > 0) node["values"] = values;
         }

         var wiresIn = new List<string>();
         foreach (LinkNode link in view.In(id))
         {
            if (inputNames.Contains(link.Destination.Anchor)) continue; // already shown under values

            int from = view.Id(link.Source.Guid);
            string socket = GraphView.SocketName(entity, link.Destination.Anchor);
            string fromSocket = GraphView.SocketName(view.Entities[from], link.Source.Anchor);
            wiresIn.Add((socket.Length > 0 ? socket + " " : "") + "<- " + view.Label(from) + (fromSocket.Length > 0 ? "." + fromSocket : ""));
         }

         var wiresOut = new List<string>();
         foreach (LinkNode link in view.Out(id))
         {
            int to = view.Id(link.Destination.Guid);
            string socket = GraphView.SocketName(entity, link.Source.Anchor);
            string toSocket = GraphView.SocketName(view.Entities[to], link.Destination.Anchor);
            wiresOut.Add((socket.Length > 0 ? socket + " " : "") + "-> " + view.Label(to) + (toSocket.Length > 0 ? "." + toSocket : ""));
         }

         if (wiresIn.Count > 0) node["in"] = wiresIn;
         if (wiresOut.Count > 0) node["out"] = wiresOut;

         return node;
      }
   }

   // ------------------------------------------------------------------------------------------------

   [McpForUnityTool("uscript_find", Description =
      "Searches all uScript graphs for a text: node titles and types, comments, typed-in values and variable values " +
      "(for example a signal name, an animation state name or a node type). Read-only. Returns graph, node id and where it matched.")]
   public static class UScriptFindTool
   {
      public class Parameters
      {
         [ToolParameter("Text to look for (not case sensitive).")]
         public string text { get; set; }

         [ToolParameter("Only search graphs whose name contains this text (optional).", Required = false)]
         public string graphs { get; set; }

         [ToolParameter("Only exact, whole-value matches instead of \"contains\" (default false).", Required = false)]
         public bool? exact { get; set; }

         [ToolParameter("Most results to return (default 100, max 500).", Required = false)]
         public int? limit { get; set; }
      }

      public static object HandleCommand(JObject @params)
      {
         try
         {
            string text = McpParams.Text(@params, "text");
            if (text == null) return new ErrorResponse("Give the text to look for.");

            bool exact = McpParams.Bool(@params, "exact", false);
            int limit = McpParams.Int(@params, "limit", 100, 1, 500);
            var errors = new List<string>();
            List<GraphView> views = McpParams.LoadGraphs(McpParams.Text(@params, "graphs"), errors);

            var results = new List<object>();
            int total = 0;

            foreach (GraphView view in views)
            {
               for (int id = 0; id < view.Entities.Length; id++)
               {
                  EntityNode entity = view.Entities[id];
                  if (entity is LinkNode) continue;

                  foreach (KeyValuePair<string, string> field in Fields(entity))
                  {
                     bool hit = exact
                        ? string.Equals(field.Value, text, StringComparison.OrdinalIgnoreCase)
                        : McpParams.Contains(field.Value, text);
                     if (!hit) continue;

                     total++;
                     if (results.Count < limit)
                     {
                        results.Add(new
                        {
                           graph = view.Name,
                           id,
                           kind = GraphView.Kind(entity),
                           title = GraphView.Title(entity),
                           found_in = field.Key,
                           value = GraphView.Shorten(field.Value)
                        });
                     }
                  }
               }
            }

            string message = total + " match(es) in " + views.Count + " graph(s)" + (total > results.Count ? ", showing " + results.Count : "") + ".";
            return new SuccessResponse(message, new { total, returned = results.Count, results, unreadable_graphs = errors });
         }
         catch (Exception e)
         {
            return new ErrorResponse("Search failed: " + e.Message);
         }
      }

      private static IEnumerable<KeyValuePair<string, string>> Fields(EntityNode entity)
      {
         yield return new KeyValuePair<string, string>("title", GraphView.Title(entity));
         yield return new KeyValuePair<string, string>("type", GraphView.TypeName(entity));
         yield return new KeyValuePair<string, string>("comment", entity.Comment.Default);

         if (entity is CommentNode)
         {
            yield return new KeyValuePair<string, string>("comment text", ((CommentNode)entity).BodyText.Default);
            yield break;
         }

         if (entity is LocalNode)
         {
            foreach (string value in GraphView.TextValues(((LocalNode)entity).Value))
               yield return new KeyValuePair<string, string>("variable value", value);
            yield break;
         }

         foreach (Parameter p in GraphView.SafeParameters(entity))
         {
            if (!p.Input) continue;
            string label = "value of " + (string.IsNullOrEmpty(p.FriendlyName) ? p.Name : p.FriendlyName);
            foreach (string value in GraphView.TextValues(p))
               yield return new KeyValuePair<string, string>(label, value);
         }
      }
   }

   // ------------------------------------------------------------------------------------------------

   [McpForUnityTool("uscript_signals", Description =
      "Maps uScript signals across all graphs: which graphs send each signal name with a Send Signal node, " +
      "and which graphs mention that exact name elsewhere (usually where it is received and compared). Read-only. " +
      "Also lists Send Signal nodes whose name is computed at runtime, and signals that no other graph mentions.")]
   public static class UScriptSignalsTool
   {
      public class Parameters
      {
         [ToolParameter("Only this signal name, or names containing this text (optional).", Required = false)]
         public string signal { get; set; }

         [ToolParameter("Most signals to return (default 200, max 1000).", Required = false)]
         public int? limit { get; set; }
      }

      private class Signal
      {
         public readonly List<string> SentBy = new List<string>();
         public readonly HashSet<string> SentByGraphs = new HashSet<string>();
      }

      public static object HandleCommand(JObject @params)
      {
         try
         {
            string filter = McpParams.Text(@params, "signal");
            int limit = McpParams.Int(@params, "limit", 200, 1, 1000);
            var errors = new List<string>();
            List<GraphView> views = McpParams.LoadGraphs(null, errors);

            var signals = new Dictionary<string, Signal>(StringComparer.Ordinal);
            var dynamicSenders = new List<string>();
            var receivingGraphs = new List<string>();
            var senderParams = new HashSet<string>(); // "graph/id" of signal-name sockets, skipped when looking for mentions

            foreach (GraphView view in views)
            {
               bool receives = false;

               for (int id = 0; id < view.Entities.Length; id++)
               {
                  EntityNode entity = view.Entities[id];
                  string type = GraphView.TypeName(entity) ?? "";

                  if (type.Contains("ReceiveSignal")) receives = true;
                  if (!(entity is LogicNode) || !type.StartsWith("uScriptAct_SendSignal", StringComparison.Ordinal)) continue;

                  foreach (Parameter p in GraphView.SafeParameters(entity))
                  {
                     if (!p.Input || p.Name != "signalName") continue;

                     senderParams.Add(view.Name + "/" + id);
                     var texts = new List<string>();
                     List<string> sources = view.DescribeInput(id, p, texts);
                     string where = view.Name + " " + view.Label(id);

                     if (texts.Count == 0)
                     {
                        dynamicSenders.Add(where + ": " + (sources.Count == 0 ? "signal name is empty" : string.Join("; ", sources)));
                        continue;
                     }

                     if (sources.Count > texts.Count) dynamicSenders.Add(where + ": partly computed at runtime: " + string.Join("; ", sources));

                     foreach (string name in texts.Where(t => !string.IsNullOrEmpty(t)))
                     {
                        Signal signal;
                        if (!signals.TryGetValue(name, out signal))
                        {
                           signal = new Signal();
                           signals[name] = signal;
                        }

                        signal.SentBy.Add(where);
                        signal.SentByGraphs.Add(view.Name);
                     }
                  }
               }

               if (receives) receivingGraphs.Add(view.Name);
            }

            // Where else does each exact signal name appear (variables, string lists, typed-in values)?
            var mentions = signals.Keys.ToDictionary(k => k, k => new List<string>(), StringComparer.Ordinal);

            foreach (GraphView view in views)
            {
               for (int id = 0; id < view.Entities.Length; id++)
               {
                  EntityNode entity = view.Entities[id];
                  if (entity is LinkNode || senderParams.Contains(view.Name + "/" + id)) continue;

                  IEnumerable<string> values = entity is LocalNode
                     ? GraphView.TextValues(((LocalNode)entity).Value)
                     : GraphView.SafeParameters(entity).Where(p => p.Input).SelectMany(p => GraphView.TextValues(p));

                  foreach (string value in values.Distinct())
                  {
                     List<string> list;
                     if (value != null && mentions.TryGetValue(value, out list))
                     {
                        string where = view.Name + " " + view.Label(id);
                        if (!list.Contains(where)) list.Add(where);
                     }
                  }
               }
            }

            var result = new List<object>();
            int notMentioned = 0;

            foreach (KeyValuePair<string, Signal> pair in signals.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
               if (filter != null && !McpParams.Contains(pair.Key, filter)) continue;

               List<string> mentionedIn = mentions[pair.Key];
               bool mentionedElsewhere = mentionedIn.Count > 0;
               if (!mentionedElsewhere) notMentioned++;

               if (result.Count >= limit) continue;

               result.Add(new
               {
                  signal = pair.Key,
                  sent_by = pair.Value.SentBy,
                  mentioned_in = mentionedIn,
                  note = mentionedElsewhere ? null : "Sent, but no graph mentions this name anywhere else. Nothing may react to it (or it is checked in C# code)."
               });
            }

            var data = new
            {
               signal_count = signals.Count,
               returned = result.Count,
               signals = result,
               not_mentioned_elsewhere = notMentioned,
               graphs_with_receive_signal = receivingGraphs,
               senders_with_runtime_names = dynamicSenders,
               unreadable_graphs = errors
            };

            return new SuccessResponse(signals.Count + " signal name(s) sent in " + views.Count + " graph(s).", data);
         }
         catch (Exception e)
         {
            return new ErrorResponse("Could not map signals: " + e.Message);
         }
      }
   }

   // ------------------------------------------------------------------------------------------------

   [McpForUnityTool("uscript_check_graph", Description =
      "Checks uScript graphs for likely mistakes: broken links, nodes that can never run, events connected to nothing, " +
      "unused variables, variables sharing a name with different types, empty Send Signal names, and out-of-date generated code. Read-only. " +
      "Without a graph name, checks every graph and summarises.")]
   public static class UScriptCheckGraphTool
   {
      public class Parameters
      {
         [ToolParameter("Graph name or path. Leave empty to check all graphs (optional).", Required = false)]
         public string graph { get; set; }

         [ToolParameter("Also list minor notes such as unused variables and nodes that never run (default true).", Required = false)]
         public bool? include_notes { get; set; }

         [ToolParameter("Most issues to return (default 200, max 1000).", Required = false)]
         public int? limit { get; set; }
      }

      public static object HandleCommand(JObject @params)
      {
         try
         {
            bool notes = McpParams.Bool(@params, "include_notes", true);
            int limit = McpParams.Int(@params, "limit", 200, 1, 1000);
            string graph = McpParams.Text(@params, "graph");
            var errors = new List<string>();
            List<GraphView> views;

            if (graph != null)
            {
               string error;
               string path = uScriptMcpGraphFiles.Resolve(graph, out error);
               if (path == null) return new ErrorResponse(error);

               GraphView view = uScriptMcpGraphFiles.Load(path, out error);
               if (view == null) return new ErrorResponse(error);
               views = new List<GraphView> { view };
            }
            else
            {
               views = McpParams.LoadGraphs(null, errors);
            }

            var issues = new List<object>();
            var summary = new List<object>();
            int total = 0;

            foreach (GraphView view in views)
            {
               List<KeyValuePair<string, string>> found = Check(view, notes);
               total += found.Count;

               int problems = found.Count(f => f.Key == "problem");
               if (found.Count > 0) summary.Add(new { graph = view.Name, problems, notes = found.Count - problems });

               foreach (KeyValuePair<string, string> f in found.OrderBy(f => f.Key == "problem" ? 0 : 1))
               {
                  if (issues.Count >= limit) break;
                  issues.Add(new { graph = view.Name, severity = f.Key, issue = f.Value });
               }
            }

            var data = new
            {
               graphs_checked = views.Count,
               total_issues = total,
               returned = issues.Count,
               per_graph = summary,
               issues,
               unreadable_graphs = errors
            };

            return new SuccessResponse(total + " issue(s) in " + views.Count + " graph(s).", data);
         }
         catch (Exception e)
         {
            return new ErrorResponse("Check failed: " + e.Message);
         }
      }

      private static List<KeyValuePair<string, string>> Check(GraphView view, bool notes)
      {
         var found = new List<KeyValuePair<string, string>>();
         Action<string> problem = text => found.Add(new KeyValuePair<string, string>("problem", text));
         Action<string> note = text => { if (notes) found.Add(new KeyValuePair<string, string>("note", text)); };

         if (view.Script.GeneratedCodeIsStale)
            problem("The generated C# code is out of date: the graph was changed after its code was generated. Save the graph again.");

         foreach (LinkNode link in view.BrokenLinks)
            problem("A link points to a node that no longer exists (link id " + view.Id(link.Guid) + ").");

         var variableTypes = new Dictionary<string, HashSet<string>>();

         for (int id = 0; id < view.Entities.Length; id++)
         {
            EntityNode entity = view.Entities[id];
            string label = view.Label(id);

            if (entity is LocalNode)
            {
               var local = (LocalNode)entity;
               string name = local.Name.Default;

               if (!view.IsLinked(id) && local.Externaled.Default != "true")
                  note(label + ": variable is not connected to anything.");

               if (!string.IsNullOrEmpty(name))
               {
                  HashSet<string> types;
                  if (!variableTypes.TryGetValue(name, out types))
                  {
                     types = new HashSet<string>();
                     variableTypes[name] = types;
                  }

                  types.Add(local.Value.Type);
               }

               continue;
            }

            if (entity is EntityEvent)
            {
               if (view.Out(id).Count == 0) note(label + ": event is connected to nothing, so it does nothing.");
               continue;
            }

            if (!(entity is LogicNode) && !(entity is EntityMethod)) continue;

            string[] signalInputs = GraphView.SignalInputs(entity);
            bool driven = entity is LogicNode && ((LogicNode)entity).Drivens != null && ((LogicNode)entity).Drivens.Length > 0;

            if (signalInputs.Length > 0 && !driven)
            {
               bool runs = view.In(id).Any(l => signalInputs.Contains(l.Destination.Anchor));
               if (!runs) note(label + ": nothing is connected to its input, so it never runs.");
            }
            else if (signalInputs.Length == 0 && entity is LogicNode && view.Out(id).Count == 0)
            {
               note(label + ": event node is connected to nothing, so it does nothing.");
            }

            string type = GraphView.TypeName(entity) ?? "";
            if (type.StartsWith("uScriptAct_SendSignal", StringComparison.Ordinal))
            {
               foreach (Parameter p in GraphView.SafeParameters(entity))
               {
                  if (p.Input && p.Name == "signalName" && view.DescribeInput(id, p, null).Count == 0)
                     problem(label + ": Send Signal has an empty signal name.");
               }
            }
         }

         foreach (KeyValuePair<string, HashSet<string>> pair in variableTypes.Where(p => p.Value.Count > 1))
            note("Variables named \"" + pair.Key + "\" have different types (" + string.Join(", ", pair.Value) + "), so they are separate variables.");

         return found;
      }
   }

   // ------------------------------------------------------------------------------------------------

   [McpForUnityTool("uscript_selection", Description =
      "Reads the nodes the user has SELECTED in the open uScript editor window, so you can check their logic. Read-only. " +
      "Use it when the user says things like \"check logic\", \"check this\" or \"what does this do\" about uScript. " +
      "It reads the editor's live graph, including unsaved changes. " +
      "By default it also follows the signal flow (the branch) forward from the selected nodes, and adds the variables and boxes they use. " +
      "Node ids match the graph's generated C# code only after the graph is saved.")]
   public static class UScriptSelectionTool
   {
      public class Parameters
      {
         [ToolParameter("Follow the signal flow forward from the selection, to include the whole branch (default: true).", Required = false)]
         public bool? follow_branch { get; set; }

         [ToolParameter("Also follow the signal flow backward, to show what leads into the selection (default: false).", Required = false)]
         public bool? include_callers { get; set; }

         [ToolParameter("Most nodes to return (default 200, max 500).", Required = false)]
         public int? limit { get; set; }
      }

      public static object HandleCommand(JObject @params)
      {
         try
         {
            uScript editor = uScript.WeakInstance;
            if (editor == null || editor.ScriptEditorCtrl == null || editor.ScriptEditorCtrl.ScriptEditor == null)
            {
               return new ErrorResponse("The uScript editor window is not open. Open it, open a graph and select some nodes.");
            }

            ScriptEditor script = editor.ScriptEditorCtrl.ScriptEditor;
            string name = string.IsNullOrEmpty(script.Name) ? "(unsaved graph)" : System.IO.Path.GetFileNameWithoutExtension(script.Name);
            var view = new GraphView(name, script);

            bool followBranch = McpParams.Bool(@params, "follow_branch", true);
            bool callers = McpParams.Bool(@params, "include_callers", false);
            int limit = McpParams.Int(@params, "limit", 200, 1, 500);

            var selected = new List<int>();
            foreach (var displayNode in editor.ScriptEditorCtrl.SelectedNodes)
            {
               if (displayNode == null) continue;
               int id = view.Id(displayNode.EntityNode.Guid);
               if (id >= 0 && !(view.Entities[id] is LinkNode)) selected.Add(id);
            }

            if (selected.Count == 0)
            {
               return new ErrorResponse("Nothing is selected in graph " + view.Name + ". Select one node (for its whole branch) or several nodes, then ask again.");
            }

            // the branch: follow signal links (not variable links) from the selection
            var included = new HashSet<int>(selected);
            var order = new List<int>(selected);
            var queue = new Queue<int>(selected);

            while ((followBranch || callers) && queue.Count > 0 && included.Count < limit)
            {
               int id = queue.Dequeue();

               if (followBranch)
               {
                  foreach (LinkNode link in view.Out(id))
                  {
                     int to = view.Id(link.Destination.Guid);
                     if (IsSignal(view, id, to) && included.Add(to)) { order.Add(to); queue.Enqueue(to); }
                  }
               }

               if (callers)
               {
                  foreach (LinkNode link in view.In(id))
                  {
                     int from = view.Id(link.Source.Guid);
                     if (IsSignal(view, from, id) && included.Add(from)) { order.Add(from); queue.Enqueue(from); }
                  }
               }
            }

            // the variables, property boxes and List Boxes those nodes read or write
            foreach (int id in order.ToList())
            {
               foreach (LinkNode link in view.In(id).Concat(view.Out(id)))
               {
                  int other = view.Id(link.Source.Guid) == id ? view.Id(link.Destination.Guid) : view.Id(link.Source.Guid);
                  if (other >= 0 && IsData(view.Entities[other]) && included.Add(other)) order.Add(other);
               }
            }

            List<int> returned = order.Take(limit).ToList();
            List<object> nodes = returned.Select(id => UScriptReadGraphTool.Describe(view, id)).ToList();

            var data = new
            {
               graph = new
               {
                  name = view.Name,
                  unsaved_changes = editor.ScriptEditorCtrl.IsDirty,
                  generated_code_out_of_date = script.GeneratedCodeIsStale
               },
               selected_ids = selected,
               followed_branch = followBranch,
               included_callers = callers,
               total = order.Count,
               returned = nodes.Count,
               truncated = order.Count > nodes.Count,
               hint = "Nodes are listed in flow order: the selection first, then the nodes its signals reach, then the variables and boxes they use. " +
                      "\"in\" and \"out\" are the links; \"values\" shows where each input gets its value.",
               nodes
            };

            string message = "Graph " + view.Name + ": " + selected.Count + " selected node(s), " + nodes.Count + " node(s) in the branch" +
                             (order.Count > nodes.Count ? " (more than the limit; raise limit to see all)." : ".");

            return new SuccessResponse(message, data);
         }
         catch (Exception e)
         {
            return new ErrorResponse("Could not read the selection: " + e.Message);
         }
      }

      private static bool IsData(EntityNode entity)
      {
         return entity is LocalNode || entity is EntityProperty || entity is OwnerConnection;
      }

      private static bool IsSignal(GraphView view, int from, int to)
      {
         if (from < 0 || to < 0) return false;
         return !IsData(view.Entities[from]) && !IsData(view.Entities[to]) && !(view.Entities[to] is CommentNode);
      }
   }
}
