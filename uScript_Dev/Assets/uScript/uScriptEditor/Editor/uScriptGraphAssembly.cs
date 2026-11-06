// --------------------------------------------------------------------------------------------------------------------
// <copyright file="uScriptGraphAssembly.cs" company="Detox Studios, LLC">
//   Copyright 2010-2015 Detox Studios, LLC. All rights reserved.
// </copyright>
// <summary>
//   Optional: compile the generated graph code (and the project's custom nodes) in their own assemblies, so saving
//   a graph recompiles only the graph code instead of every script in Assembly-CSharp.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

using Detox.Editor;
using Detox.ScriptEditor;

using UnityEditor;
using UnityEditor.Compilation;

using UnityEngine;

public static class uScriptGraphAssembly
{
   public const string GraphsAssembly = "uScriptGraphs";
   public const string NodesAssembly = "uScriptNodes";
   private const string RuntimeAssembly = "uScript.Runtime";

   private static readonly string[] ProjectAssemblies = { "Assembly-CSharp", "Assembly-CSharp-firstpass" };

   public static string GraphsAsmdefPath
   {
      get { return Normalize(Preferences.GeneratedScripts) + "/" + GraphsAssembly + ".asmdef"; }
   }

   public static string NodesAsmdefPath
   {
      get { return Normalize(Preferences.UserNodes) + "/" + NodesAssembly + ".asmdef"; }
   }

   public static bool IsEnabled
   {
      get { return File.Exists(GraphsAsmdefPath); }
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Turning it on and off
   // ---------------------------------------------------------------------------------------------------------------

   public static void Toggle()
   {
      if (IsEnabled)
      {
         Disable(true);
      }
      else
      {
         Enable();
      }
   }

   public static void Enable()
   {
      const string Title = "Compile Graphs Separately";

      var generatedFolder = Normalize(Preferences.GeneratedScripts);
      var nodesFolder = Normalize(Preferences.UserNodes);

      if (!string.Equals(generatedFolder, Normalize(Preferences.NestedScripts), StringComparison.OrdinalIgnoreCase))
      {
         EditorUtility.DisplayDialog(Title, "The generated graph code and the nested graph code are set to different folders in the uScript Preferences. Put them in the same folder first.", "OK");
         return;
      }

      // Every generated graph file must be inside the folder that gets the assembly definition
      var outside = FindGeneratedScripts().Where(path => !IsInside(path, generatedFolder)).ToList();
      if (outside.Count > 0)
      {
         EditorUtility.DisplayDialog(Title, "These generated graph scripts are not in " + RelativeAsset(generatedFolder) + ":\n\n" + FormatList(outside.Select(RelativeAsset)) + "\n\nMove them there (or rebuild the graphs) first.", "OK");
         return;
      }

      // "Editor" folders lose their special meaning inside an assembly definition
      var editorFolders = FindEditorFolders(generatedFolder).Concat(FindEditorFolders(nodesFolder)).ToList();
      if (editorFolders.Count > 0)
      {
         EditorUtility.DisplayDialog(Title, "These Editor folders are inside the graph or node folders, which would break them:\n\n" + FormatList(editorFolders.Select(RelativeAsset)) + "\n\nMove them out first.", "OK");
         return;
      }

      var graphTypes = TypesDeclaredIn(generatedFolder);
      var nodeTypes = Directory.Exists(nodesFolder) ? TypesDeclaredIn(nodesFolder) : new HashSet<Type>();

      var problems = new SortedSet<string>();
      var nodeReferences = new SortedSet<string> { RuntimeAssembly };
      var graphReferences = new SortedSet<string> { RuntimeAssembly };

      if (Directory.Exists(nodesFolder))
      {
         graphReferences.Add(NodesAssembly);
      }

      foreach (var type in nodeTypes)
      {
         foreach (var used in TypeDependencies.Of(type))
         {
            Classify(used, nodeTypes, null, graphTypes, nodeReferences, problems, "custom node " + type.Name);
         }
      }

      foreach (var type in graphTypes)
      {
         foreach (var used in TypeDependencies.Of(type))
         {
            Classify(used, graphTypes, nodeTypes, null, graphReferences, problems, "graph " + type.Name);
         }
      }

      if (problems.Count > 0)
      {
         foreach (var problem in problems)
         {
            Debug.LogWarning("uScript - Compile Graphs Separately: " + problem);
         }

         EditorUtility.DisplayDialog(
            Title,
            "Graphs or custom nodes use these scripts from your project, which can't be used from a separate assembly:\n\n"
            + FormatList(problems) + "\n\nTo use this option, put those scripts in an Assembly Definition (asmdef) first. The full list is in the Console.",
            "OK");
         return;
      }

      var message = string.Format(
         "Graph code in {0} will be compiled as \"{1}\"{2}.\n\nSaving a graph then recompiles only that code, not your other scripts. Your scripts can still use the graph components as before.\n\nYou can turn this off again in the File menu.",
         RelativeAsset(generatedFolder),
         GraphsAssembly,
         Directory.Exists(nodesFolder) ? string.Format(", and your custom nodes in {0} as \"{1}\"", RelativeAsset(nodesFolder), NodesAssembly) : string.Empty);

      if (!EditorUtility.DisplayDialog(Title, message, "Turn On", "Cancel"))
      {
         return;
      }

      if (Directory.Exists(nodesFolder))
      {
         WriteAsmdef(NodesAsmdefPath, NodesAssembly, nodeReferences);
      }

      WriteAsmdef(GraphsAsmdefPath, GraphsAssembly, graphReferences);
      AssetDatabase.Refresh();

      Debug.Log("uScript: graphs are now compiled separately (" + GraphsAssembly + ").");
   }

   public static void Disable(bool ask)
   {
      if (ask && !EditorUtility.DisplayDialog(
             "Compile Graphs Separately",
             "Compile the graph code (and custom nodes) together with your other scripts again?",
             "Turn Off",
             "Cancel"))
      {
         return;
      }

      DeleteAsset(GraphsAsmdefPath);
      DeleteAsset(NodesAsmdefPath);
      AssetDatabase.Refresh();

      Debug.Log("uScript: graphs are compiled with your other scripts again.");
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Keeping the references up to date when a graph is saved
   // ---------------------------------------------------------------------------------------------------------------

   /// <summary>
   /// Called before a graph's code is generated: adds the assemblies the graph uses to the graph assembly.
   /// Returns the types it uses that the graph assembly can't reach (empty when everything is fine).
   /// </summary>
   public static List<string> UpdateForGraph(ScriptEditor script)
   {
      var problems = new SortedSet<string>();
      if (!IsEnabled || script == null)
      {
         return problems.ToList();
      }

      var references = new SortedSet<string>(ReadReferences(GraphsAsmdefPath));
      var before = references.Count;

      foreach (var typeName in TypeNamesUsedBy(script))
      {
         if (string.IsNullOrEmpty(typeName))
         {
            continue;
         }

         var type = uScript.Instance != null ? uScript.Instance.GetType(typeName) : uScript.GetAssemblyQualifiedType(typeName);
         if (type == null)
         {
            continue;
         }

         foreach (var used in TypeDependencies.Expand(type))
         {
            Classify(used, null, null, null, references, problems, "graph " + Path.GetFileNameWithoutExtension(script.Name));
         }
      }

      if (references.Count != before)
      {
         WriteAsmdef(GraphsAsmdefPath, GraphsAssembly, references);
      }

      return problems.ToList();
   }

   private static IEnumerable<string> TypeNamesUsedBy(ScriptEditor script)
   {
      foreach (var node in script.EntityNodes)
      {
         if (node is LinkNode)
         {
            continue;
         }

         if (node is LogicNode)
         {
            yield return ((LogicNode)node).Type;
         }

         if (!string.IsNullOrEmpty(node.Instance.Type))
         {
            yield return CleanTypeName(node.Instance.Type);
         }

         foreach (var parameter in node.Parameters)
         {
            if (!string.IsNullOrEmpty(parameter.Type))
            {
               yield return CleanTypeName(parameter.Type);
            }
         }
      }
   }

   private static string CleanTypeName(string typeName)
   {
      return typeName.Replace("[]", string.Empty).Replace("&", string.Empty);
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Dependency rules
   // ---------------------------------------------------------------------------------------------------------------

   /// <summary>
   /// Sorts one used type: part of the assembly being built (ignored), in an assembly definition (added to
   /// <paramref name="references"/>), engine/system/plugin code (ignored), or project code that a separate
   /// assembly can't reach (added to <paramref name="problems"/>).
   /// </summary>
   private static void Classify(
      Type used,
      HashSet<Type> ownTypes,
      HashSet<Type> nodeTypes,
      HashSet<Type> forbiddenTypes,
      SortedSet<string> references,
      SortedSet<string> problems,
      string usedBy)
   {
      if (used == null || used.IsGenericParameter)
      {
         return;
      }

      if (ownTypes != null && ownTypes.Contains(used))
      {
         return;
      }

      if (nodeTypes != null && nodeTypes.Contains(used))
      {
         references.Add(NodesAssembly);
         return;
      }

      var assemblyName = used.Assembly.GetName().Name;

      if (assemblyName == GraphsAssembly)
      {
         return;   // another graph, in the same assembly
      }

      if (assemblyName == NodesAssembly)
      {
         references.Add(NodesAssembly);
         return;
      }

      if ((forbiddenTypes != null && forbiddenTypes.Contains(used)) || ProjectAssemblies.Contains(assemblyName))
      {
         problems.Add(used.FullName + " (used by " + usedBy + ")");
         return;
      }

      if (!string.IsNullOrEmpty(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName)))
      {
         references.Add(assemblyName);
      }
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Files
   // ---------------------------------------------------------------------------------------------------------------

   [Serializable]
   private class AsmdefData
   {
      public string name = string.Empty;
      public string rootNamespace = string.Empty;
      public string[] references = new string[0];
      public string[] includePlatforms = new string[0];
      public string[] excludePlatforms = new string[0];
      public bool allowUnsafeCode;
      public bool overrideReferences;
      public string[] precompiledReferences = new string[0];
      public bool autoReferenced = true;
      public string[] defineConstraints = new string[0];
      public bool noEngineReferences;
   }

   private static void WriteAsmdef(string path, string name, IEnumerable<string> references)
   {
      var data = new AsmdefData
      {
         name = name,
         references = references.Where(r => r != name).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToArray()
      };

      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.WriteAllText(path, JsonUtility.ToJson(data, true));
      AssetDatabase.ImportAsset(RelativeAsset(path));
   }

   private static IEnumerable<string> ReadReferences(string path)
   {
      try
      {
         var data = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(path));
         return data != null && data.references != null ? data.references : new string[0];
      }
      catch (Exception)
      {
         return new string[0];
      }
   }

   private static void DeleteAsset(string path)
   {
      if (File.Exists(path))
      {
         AssetDatabase.DeleteAsset(RelativeAsset(path));
      }
   }

   private static IEnumerable<string> FindGeneratedScripts()
   {
      return AssetDatabase.FindAssets("l:uScriptCode")
         .Select(AssetDatabase.GUIDToAssetPath)
         .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
         .Select(path => Normalize(Path.GetFullPath(path)));
   }

   private static IEnumerable<string> FindEditorFolders(string folder)
   {
      if (!Directory.Exists(folder))
      {
         return new string[0];
      }

      return Directory.GetDirectories(folder, "*", SearchOption.AllDirectories)
         .Where(dir => string.Equals(Path.GetFileName(dir), "Editor", StringComparison.OrdinalIgnoreCase))
         .Select(Normalize);
   }

   /// <summary>Types in the project assemblies declared in .cs files under the folder.</summary>
   private static HashSet<Type> TypesDeclaredIn(string folder)
   {
      var result = new HashSet<Type>();
      if (!Directory.Exists(folder))
      {
         return result;
      }

      var declaration = new Regex(@"\b(?:class|struct|enum|interface)\s+([A-Za-z_]\w*)|\bdelegate\s+[\w<>\[\],.\s]+?\s+([A-Za-z_]\w*)\s*\(");
      var names = new HashSet<string>();

      foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
      {
         string text;
         try
         {
            text = File.ReadAllText(file);
         }
         catch (Exception)
         {
            continue;
         }

         foreach (Match match in declaration.Matches(text))
         {
            names.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
         }
      }

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
         if (!ProjectAssemblies.Contains(assembly.GetName().Name))
         {
            continue;
         }

         Type[] types;
         try
         {
            types = assembly.GetTypes();
         }
         catch (ReflectionTypeLoadException e)
         {
            types = e.Types.Where(t => t != null).ToArray();
         }

         foreach (var type in types)
         {
            var outer = type;
            while (outer.DeclaringType != null)
            {
               outer = outer.DeclaringType;
            }

            if (names.Contains(outer.Name))
            {
               result.Add(type);
            }
         }
      }

      return result;
   }

   private static string Normalize(string path)
   {
      return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
   }

   private static bool IsInside(string path, string folder)
   {
      return Normalize(path).StartsWith(Normalize(folder) + "/", StringComparison.OrdinalIgnoreCase);
   }

   private static string RelativeAsset(string path)
   {
      path = Normalize(Path.GetFullPath(path));
      var project = Normalize(Path.GetFullPath(Application.dataPath + "/.."));
      return path.StartsWith(project + "/", StringComparison.OrdinalIgnoreCase) ? path.Substring(project.Length + 1) : path;
   }

   private static string FormatList(IEnumerable<string> items)
   {
      var all = items.ToList();
      var shown = all.Take(12).ToList();
      if (all.Count > shown.Count)
      {
         shown.Add(string.Format("... and {0} more", all.Count - shown.Count));
      }

      return string.Join("\n", shown.ToArray());
   }

   // ---------------------------------------------------------------------------------------------------------------
   // Which types a compiled class uses (signatures and method bodies)
   // ---------------------------------------------------------------------------------------------------------------

   private static class TypeDependencies
   {
      private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

      private static readonly OpCode[] OneByte = new OpCode[0x100];
      private static readonly OpCode[] TwoByte = new OpCode[0x100];

      static TypeDependencies()
      {
         foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
         {
            var code = (OpCode)field.GetValue(null);
            var value = (ushort)code.Value;
            if (value < 0x100)
            {
               OneByte[value] = code;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
               TwoByte[value & 0xff] = code;
            }
         }
      }

      public static HashSet<Type> Of(Type type)
      {
         var found = new HashSet<Type>();
         Collect(type, found);
         return found;
      }

      /// <summary>The type itself plus element and generic argument types.</summary>
      public static HashSet<Type> Expand(Type type)
      {
         var found = new HashSet<Type>();
         Add(type, found);
         return found;
      }

      private static void Collect(Type type, HashSet<Type> found)
      {
         Add(type.BaseType, found);
         foreach (var i in type.GetInterfaces()) Add(i, found);

         foreach (var field in type.GetFields(All)) Add(field.FieldType, found);
         foreach (var property in type.GetProperties(All)) Add(property.PropertyType, found);
         foreach (var e in type.GetEvents(All)) Add(e.EventHandlerType, found);

         foreach (var method in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All).Cast<MethodBase>()))
         {
            var info = method as MethodInfo;
            if (info != null) Add(info.ReturnType, found);
            foreach (var parameter in method.GetParameters()) Add(parameter.ParameterType, found);
            ScanBody(method, found);
         }

         foreach (var nested in type.GetNestedTypes(All))
         {
            Collect(nested, found);
         }
      }

      private static void Add(Type type, HashSet<Type> found)
      {
         while (type != null && (type.IsArray || type.IsByRef || type.IsPointer))
         {
            type = type.GetElementType();
         }

         if (type == null || type.IsGenericParameter)
         {
            return;
         }

         if (type.IsGenericType && !type.IsGenericTypeDefinition)
         {
            foreach (var argument in type.GetGenericArguments())
            {
               Add(argument, found);
            }

            type = type.GetGenericTypeDefinition();
         }

         found.Add(type);
      }

      private static void ScanBody(MethodBase method, HashSet<Type> found)
      {
         MethodBody body;
         try
         {
            body = method.GetMethodBody();
         }
         catch (Exception)
         {
            return;
         }

         if (body == null)
         {
            return;
         }

         foreach (var local in body.LocalVariables)
         {
            Add(local.LocalType, found);
         }

         var il = body.GetILAsByteArray();
         if (il == null)
         {
            return;
         }

         var typeArguments = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
         var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

         var position = 0;
         while (position < il.Length)
         {
            OpCode code;
            var first = il[position++];
            if (first == 0xFE)
            {
               if (position >= il.Length) break;
               code = TwoByte[il[position++]];
            }
            else
            {
               code = OneByte[first];
            }

            var operandSize = OperandSize(code.OperandType, il, position);

            if (code.OperandType == OperandType.InlineType || code.OperandType == OperandType.InlineTok ||
                code.OperandType == OperandType.InlineMethod || code.OperandType == OperandType.InlineField)
            {
               if (position + 4 <= il.Length)
               {
                  try
                  {
                     var member = method.Module.ResolveMember(BitConverter.ToInt32(il, position), typeArguments, methodArguments);
                     AddMember(member, found);
                  }
                  catch (Exception)
                  {
                     // Unresolvable token (e.g. generic context): skip it
                  }
               }
            }

            position += operandSize;
         }
      }

      private static void AddMember(MemberInfo member, HashSet<Type> found)
      {
         if (member == null) return;

         var type = member as Type;
         if (type != null)
         {
            Add(type, found);
            return;
         }

         Add(member.DeclaringType, found);

         var field = member as FieldInfo;
         if (field != null)
         {
            Add(field.FieldType, found);
            return;
         }

         var method = member as MethodBase;
         if (method != null)
         {
            var info = method as MethodInfo;
            if (info != null)
            {
               Add(info.ReturnType, found);
               if (info.IsGenericMethod)
               {
                  foreach (var argument in info.GetGenericArguments()) Add(argument, found);
               }
            }

            foreach (var parameter in method.GetParameters()) Add(parameter.ParameterType, found);
         }
      }

      private static int OperandSize(OperandType operandType, byte[] il, int position)
      {
         switch (operandType)
         {
            case OperandType.InlineNone:
               return 0;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
               return 1;
            case OperandType.InlineVar:
               return 2;
            case OperandType.InlineI8:
            case OperandType.InlineR:
               return 8;
            case OperandType.InlineSwitch:
               return position + 4 <= il.Length ? 4 + (4 * BitConverter.ToInt32(il, position)) : 4;
            default:
               return 4;
         }
      }
   }
}
