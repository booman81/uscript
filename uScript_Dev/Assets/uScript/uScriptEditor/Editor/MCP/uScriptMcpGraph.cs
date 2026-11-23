// uScript graph reading for the MCP tools (read-only).
// This assembly only compiles when the "MCP for Unity" package (com.coplaydev.unity-mcp) is installed.

using System;
using System.Collections.Generic;
using System.IO;
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
using UnityEngine;

namespace uScriptMcp
{
   /// <summary>Finds and loads .uscript graph files without touching the open uScript window or its cache.</summary>
   internal static class uScriptMcpGraphFiles
   {
      private class CacheEntry
      {
         public DateTime Modified;
         public GraphView View;
      }

      private static readonly Dictionary<string, CacheEntry> s_Cache = new Dictionary<string, CacheEntry>();

      /// <summary>All graphs in the project as "Assets/..." paths, sorted.</summary>
      public static List<string> AllGraphPaths()
      {
         string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace("\\", "/") + "/";

         return Directory.GetFiles(Application.dataPath, "*.uscript", SearchOption.AllDirectories)
            .Select(p => p.Replace("\\", "/"))
            .Select(p => p.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) ? p.Substring(projectRoot.Length) : p)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
      }

      public static string GraphName(string path)
      {
         return Path.GetFileNameWithoutExtension(path);
      }

      /// <summary>Finds a graph by name ("Zombie_Hero"), file name or path. Returns null and an error when not found or ambiguous.</summary>
      public static string Resolve(string nameOrPath, out string error)
      {
         error = null;
         if (string.IsNullOrEmpty(nameOrPath))
         {
            error = "Give a graph name, for example \"Zombie_Hero\".";
            return null;
         }

         string wanted = nameOrPath.Replace("\\", "/").Trim();
         List<string> all = AllGraphPaths();

         string exact = all.FirstOrDefault(p => string.Equals(p, wanted, StringComparison.OrdinalIgnoreCase));
         if (exact != null) return exact;

         string wantedName = wanted.EndsWith(".uscript", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(wanted)
            : Path.GetFileName(wanted);

         List<string> byName = all.Where(p => string.Equals(GraphName(p), wantedName, StringComparison.OrdinalIgnoreCase)).ToList();
         if (byName.Count == 1) return byName[0];
         if (byName.Count > 1)
         {
            error = "More than one graph is called \"" + wantedName + "\". Use the full path: " + string.Join(", ", byName);
            return null;
         }

         List<string> similar = all.Where(p => GraphName(p).IndexOf(wantedName, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(10).Select(GraphName).ToList();
         error = "No graph called \"" + wantedName + "\"." + (similar.Count > 0 ? " Similar names: " + string.Join(", ", similar) : "");
         return null;
      }

      /// <summary>Loads a graph. Results are cached until the file changes on disk.</summary>
      public static GraphView Load(string assetPath, out string error)
      {
         error = null;
         string fullPath = Path.GetFullPath(assetPath);

         if (!File.Exists(fullPath))
         {
            error = "File not found: " + assetPath;
            return null;
         }

         DateTime modified = File.GetLastWriteTimeUtc(fullPath);
         CacheEntry entry;
         if (s_Cache.TryGetValue(assetPath, out entry) && entry.Modified == modified) return entry.View;

         string contents = File.ReadAllText(fullPath);
         string name = Path.GetFileName(assetPath);
         var script = new ScriptEditor(string.Empty, null, null);
         bool ok;

         // Same format detection as ScriptEditor.Open, but with no cache name so uScript's own graph cache is untouched.
         string base64 = ExtractBase64(contents);
         if (base64 != null)
         {
            ok = script.OpenFromBase64(null, name, base64, false);
         }
         else
         {
            ok = script.OpenFromXml(null, name, contents, false);
         }

         if (!ok)
         {
            error = "uScript could not read " + assetPath;
            return null;
         }

         var view = new GraphView(assetPath, script);
         s_Cache[assetPath] = new CacheEntry { Modified = modified, View = view };
         return view;
      }

      private static string ExtractBase64(string contents)
      {
         const string Start = "/*[[BEGIN BASE64";
         const string End = "END BASE64]]*/";

         int start = contents.IndexOf(Start, StringComparison.Ordinal);
         if (start < 0) return null;
         start += Start.Length;

         int end = contents.IndexOf(End, start, StringComparison.Ordinal);
         if (end < 0) return null;

         return contents.Substring(start, end - start).Trim();
      }
   }

   /// <summary>A read-only, indexed view of one graph.</summary>
   internal class GraphView
   {
      public readonly string Path;
      public readonly string Name;
      public readonly ScriptEditor Script;

      /// <summary>All entities in file order. The index is the number uScript uses in generated code (Relay_In_865 = id 865).</summary>
      public readonly EntityNode[] Entities;

      public readonly Dictionary<Guid, int> IdOf = new Dictionary<Guid, int>();
      public readonly List<LinkNode> Links = new List<LinkNode>();
      public readonly Dictionary<int, List<LinkNode>> LinksIn = new Dictionary<int, List<LinkNode>>();
      public readonly Dictionary<int, List<LinkNode>> LinksOut = new Dictionary<int, List<LinkNode>>();
      public readonly List<LinkNode> BrokenLinks = new List<LinkNode>();

      public GraphView(string path, ScriptEditor script)
      {
         Path = path;
         Name = System.IO.Path.GetFileNameWithoutExtension(path);
         Script = script;
         Entities = script.DirectEntityNodes;

         for (int i = 0; i < Entities.Length; i++)
         {
            IdOf[Entities[i].Guid] = i;
         }

         foreach (EntityNode entity in Entities)
         {
            if (!(entity is LinkNode)) continue;

            var link = (LinkNode)entity;
            int from, to;
            if (!IdOf.TryGetValue(link.Source.Guid, out from) || !IdOf.TryGetValue(link.Destination.Guid, out to))
            {
               BrokenLinks.Add(link);
               continue;
            }

            Links.Add(link);
            Add(LinksOut, from, link);
            Add(LinksIn, to, link);
         }
      }

      private static void Add(Dictionary<int, List<LinkNode>> map, int id, LinkNode link)
      {
         List<LinkNode> list;
         if (!map.TryGetValue(id, out list))
         {
            list = new List<LinkNode>();
            map[id] = list;
         }

         list.Add(link);
      }

      public List<LinkNode> In(int id)
      {
         List<LinkNode> list;
         return LinksIn.TryGetValue(id, out list) ? list : new List<LinkNode>();
      }

      public List<LinkNode> Out(int id)
      {
         List<LinkNode> list;
         return LinksOut.TryGetValue(id, out list) ? list : new List<LinkNode>();
      }

      public bool IsLinked(int id)
      {
         return LinksIn.ContainsKey(id) || LinksOut.ContainsKey(id);
      }

      public int Id(Guid guid)
      {
         int id;
         return IdOf.TryGetValue(guid, out id) ? id : -1;
      }

      // ----- Describing entities -----

      public static string Kind(EntityNode entity)
      {
         if (entity is LogicNode) return "node";
         if (entity is EntityEvent) return "event";
         if (entity is EntityMethod) return "method";
         if (entity is EntityProperty) return "property";
         if (entity is LocalNode) return "variable";
         if (entity is ExternalConnection) return "external";
         if (entity is OwnerConnection) return "owner";
         if (entity is CommentNode) return "comment";
         if (entity is LinkNode) return "link";
         return "other";
      }

      public static string Title(EntityNode entity)
      {
         if (entity is LogicNode) return ((LogicNode)entity).FriendlyName;
         if (entity is EntityEvent) return ((EntityEvent)entity).FriendlyType;
         if (entity is EntityMethod) return ((EntityMethod)entity).Input.FriendlyName;
         if (entity is EntityProperty) return ((EntityProperty)entity).Parameter.FriendlyName;
         if (entity is LocalNode)
         {
            string name = ((LocalNode)entity).Name.Default;
            return string.IsNullOrEmpty(name) ? "(unnamed variable)" : name;
         }
         if (entity is ExternalConnection) return ((ExternalConnection)entity).Name.Default;
         if (entity is OwnerConnection) return "Owner GameObject";
         if (entity is CommentNode) return ((CommentNode)entity).TitleText.Default;
         return "";
      }

      public static string TypeName(EntityNode entity)
      {
         if (entity is LogicNode) return ((LogicNode)entity).Type;
         if (entity is EntityEvent) return ((EntityEvent)entity).ComponentType;
         if (entity is EntityMethod) return ((EntityMethod)entity).ComponentType;
         if (entity is EntityProperty) return ((EntityProperty)entity).ComponentType;
         if (entity is LocalNode) return ((LocalNode)entity).Value.Type;
         return "";
      }

      /// <summary>Short label used inside link descriptions, for example: 865 "Animator CrossFade".</summary>
      public string Label(int id)
      {
         if (id < 0 || id >= Entities.Length) return "?";
         EntityNode entity = Entities[id];
         string title = Title(entity);
         if (entity is LocalNode) return id + " (variable " + title + ")";
         if (entity is ExternalConnection) return id + " (graph connection " + title + ")";
         if (entity is OwnerConnection) return id + " (Owner GameObject)";
         return id + " (" + title + ")";
      }

      /// <summary>Friendly name for a socket of an entity, falling back to the raw anchor name.</summary>
      public static string SocketName(EntityNode entity, string anchor)
      {
         if (string.IsNullOrEmpty(anchor)) return "";

         foreach (Parameter p in SafeParameters(entity))
         {
            if (p.Name == anchor && !string.IsNullOrEmpty(p.FriendlyName)) return p.FriendlyName;
         }

         IEnumerable<Plug> plugs = Enumerable.Empty<Plug>();
         if (entity is LogicNode)
         {
            var l = (LogicNode)entity;
            plugs = (l.Inputs ?? new Plug[0]).Concat(l.Outputs ?? new Plug[0]).Concat(l.Events ?? new Plug[0]);
         }
         else if (entity is EntityEvent)
         {
            plugs = ((EntityEvent)entity).Outputs ?? new Plug[0];
         }
         else if (entity is EntityMethod)
         {
            var m = (EntityMethod)entity;
            plugs = new[] { m.Input, m.Output };
         }

         foreach (Plug plug in plugs)
         {
            if (plug.Name == anchor && !string.IsNullOrEmpty(plug.FriendlyName)) return plug.FriendlyName;
         }

         return anchor;
      }

      public static Parameter[] SafeParameters(EntityNode entity)
      {
         try
         {
            return entity.Parameters ?? new Parameter[0];
         }
         catch (Exception)
         {
            return new Parameter[0];
         }
      }

      /// <summary>Names of the signal inputs (In, Reset...) of a node that runs when signalled.</summary>
      public static string[] SignalInputs(EntityNode entity)
      {
         if (entity is LogicNode) return (((LogicNode)entity).Inputs ?? new Plug[0]).Select(p => p.Name).ToArray();
         if (entity is EntityMethod) return new[] { ((EntityMethod)entity).Input.Name };
         return new string[0];
      }

      public static string Shorten(string value, int max = 200)
      {
         if (value == null) return "";
         return value.Length <= max ? value : value.Substring(0, max) + "... (" + value.Length + " characters)";
      }

      /// <summary>The value written in a parameter: arrays become lists.</summary>
      public static object ValueOf(Parameter p)
      {
         if (!string.IsNullOrEmpty(p.Type) && p.Type.EndsWith("[]"))
         {
            try
            {
               return Parameter.StringToArray(p.Default ?? "").Select(v => Shorten(v)).ToList();
            }
            catch (Exception)
            {
               // fall back to the raw text
            }
         }

         return Shorten(p.Default);
      }

      /// <summary>Text values of a parameter, used for searching. Array parameters give one entry per element.</summary>
      public static IEnumerable<string> TextValues(Parameter p)
      {
         if (string.IsNullOrEmpty(p.Default)) yield break;

         if (!string.IsNullOrEmpty(p.Type) && p.Type.EndsWith("[]"))
         {
            string[] parts;
            try
            {
               parts = Parameter.StringToArray(p.Default);
            }
            catch (Exception)
            {
               parts = new[] { p.Default };
            }

            foreach (string part in parts) yield return part;
         }
         else
         {
            yield return p.Default;
         }
      }

      /// <summary>
      /// What feeds a node's value socket: typed-in text, variables, graph connections or other nodes' outputs.
      /// Several things can be linked to one socket (for example to fill a list). Empty when nothing is set.
      /// resolvedTexts gets the plain text values that are known (typed in, or held by a linked variable).
      /// </summary>
      public List<string> DescribeInput(int id, Parameter p, List<string> resolvedTexts)
      {
         var sources = new List<string>();

         foreach (LinkNode link in In(id))
         {
            if (link.Destination.Anchor != p.Name) continue;

            int from = Id(link.Source.Guid);
            EntityNode source = Entities[from];

            if (source is LocalNode)
            {
               var local = (LocalNode)source;
               if (resolvedTexts != null) resolvedTexts.AddRange(TextValues(local.Value));
               sources.Add("variable \"" + Title(source) + "\" = " + Shorten(local.Value.Default) + " (id " + from + ")");
            }
            else if (source is ExternalConnection)
            {
               sources.Add("graph connection \"" + Title(source) + "\" (set by whoever uses this graph)");
            }
            else if (source is OwnerConnection)
            {
               sources.Add("Owner GameObject");
            }
            else
            {
               sources.Add("output \"" + SocketName(source, link.Source.Anchor) + "\" of " + Label(from));
            }
         }

         if (sources.Count == 0 && !string.IsNullOrEmpty(p.Default))
         {
            if (resolvedTexts != null) resolvedTexts.AddRange(TextValues(p));
            sources.Add(Shorten(p.Default));
         }

         return sources;
      }
   }
}
