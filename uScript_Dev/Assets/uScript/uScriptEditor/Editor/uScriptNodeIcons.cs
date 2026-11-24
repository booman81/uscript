// Icons for node title bars in the Modern theme.
// Order of preference: lightning for events, Unity's own icon for the Unity object a node works on
// (Animator, Camera, Rigidbody...), then one of uScript's drawn icons chosen from the node's name.

using System;
using System.Collections.Generic;
using System.Linq;

using Detox.ScriptEditor;

using UnityEditor;

using UnityEngine;

public static class uScriptNodeIcons
{
   /// <summary>Unity types whose built-in icon is used, matched against words in the node's name and type.</summary>
   private static readonly KeyValuePair<string[], string>[] UnityTypeRules =
   {
      Rule("UnityEngine.Animator", "animator"),
      Rule("UnityEngine.AnimationClip", "animation", "anim"),
      Rule("UnityEngine.AudioSource", "audio", "sound", "music", "mixer", "volume"),
      Rule("UnityEngine.Camera", "camera", "screen", "viewport"),
      Rule("UnityEngine.Light", "light", "lightmap"),
      Rule("UnityEngine.AI.NavMeshAgent", "navmesh", "navmeshagent", "nav"),
      Rule("UnityEngine.ParticleSystem", "particle", "particles", "emit", "emitting"),
      Rule("UnityEngine.Rigidbody", "rigidbody", "force", "velocity", "physics", "torque", "gravity"),
      Rule("UnityEngine.BoxCollider", "collision", "collider", "trigger", "raycast", "overlap"),
      Rule("UnityEngine.Material", "material", "shader"),
      Rule("UnityEngine.Texture2D", "texture"),
      Rule("UnityEngine.Sprite", "sprite", "image"),
      Rule("UnityEngine.Font", "text", "tmp", "textmeshpro", "font", "label"),
      Rule("UnityEditor.SceneAsset", "scene", "level"),
      Rule("UnityEngine.RectTransform", "ui", "canvas", "slider", "scroll"),
      Rule("UnityEngine.Transform", "transform", "position", "rotation", "rotate", "scale", "parent", "lookat", "translate", "quaternion", "move"),
   };

   /// <summary>Checked after the drawn icons, because "(GameObject)" appears in many names that are really about lists, loops or comparisons.</summary>
   private static readonly KeyValuePair<string[], string>[] FallbackUnityTypeRules =
   {
      Rule("UnityEngine.GameObject", "gameobject", "gameobjects", "spawn", "instantiate", "destroy", "tag", "prefab", "active", "child", "children"),
   };

   /// <summary>uScript's own drawn icons, matched against words in the node's name.</summary>
   private static readonly KeyValuePair<string[], string>[] DrawnRules =
   {
      Rule("signal", "signal", "signals", "broadcast", "message"),
      Rule("set", "set", "assign", "store"),
      Rule("timer", "delay", "timer", "wait", "time", "cooldown", "seconds"),
      Rule("loop", "loop", "foreach", "each", "repeat", "while", "iterate"),
      Rule("list", "list", "lists", "array", "index", "append", "insert", "shuffle", "inlist"),
      Rule("compare", "compare", "equal", "equals", "greater", "less"),
      Rule("math", "add", "subtract", "multiply", "divide", "int", "float", "round", "exponent", "logarithmic",
         "clamp", "random", "rng", "min", "max", "abs", "lerp", "math", "modulo", "sqrt", "sin", "cos", "power", "increment", "decrement"),
      Rule("string", "string", "strings", "concatenate", "split", "replace", "substring", "format"),
      Rule("debug", "log", "debug", "print", "console", "breakpoint"),
      Rule("input", "input", "key", "mouse", "touch", "axis", "button", "joystick"),
      Rule("logic", "switch", "gate", "branch", "manual", "pass", "toggle"),
   };

   private static readonly Dictionary<string, Texture> Cache = new Dictionary<string, Texture>();

   private static readonly Dictionary<string, Texture2D> Drawn = new Dictionary<string, Texture2D>();

   private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>();

   /// <summary>The icon for a node, or null when it gets none. drawnIcon is true for white icons that should be tinted.</summary>
   public static Texture For(EntityNode entity, bool isEvent, out bool drawnIcon)
   {
      drawnIcon = false;

      if (isEvent)
      {
         drawnIcon = true;
         return GetDrawn("event");
      }

      string title;
      string typeName;
      if (!Describe(entity, out title, out typeName))
      {
         return null;
      }

      var key = typeName + "|" + title;
      Texture icon;
      if (Cache.TryGetValue(key, out icon))
      {
         drawnIcon = icon != null && Drawn.ContainsValue(icon as Texture2D);
         return icon;
      }

      icon = Find(entity, title, typeName);
      Cache[key] = icon;
      drawnIcon = icon != null && Drawn.ContainsValue(icon as Texture2D);
      return icon;
   }

   private static bool Describe(EntityNode entity, out string title, out string typeName)
   {
      title = string.Empty;
      typeName = string.Empty;

      if (entity is LogicNode)
      {
         var logic = (LogicNode)entity;
         title = logic.FriendlyName ?? string.Empty;
         typeName = logic.Type ?? string.Empty;
         return true;
      }

      if (entity is EntityEvent)
      {
         var e = (EntityEvent)entity;
         title = e.FriendlyType ?? string.Empty;
         typeName = e.ComponentType ?? string.Empty;
         return true;
      }

      if (entity is EntityMethod)
      {
         var method = (EntityMethod)entity;
         title = method.Input.FriendlyName ?? string.Empty;
         typeName = method.ComponentType ?? string.Empty;
         return true;
      }

      return false;
   }

   private static Texture Find(EntityNode entity, string title, string typeName)
   {
      // Nodes made from another graph
      if (entity is LogicNode && IsGraphType(typeName))
      {
         return GetDrawn("nested");
      }

      // Reflection nodes on a Unity type ("UnityEngine.Animator"): that type's own icon
      if (entity is EntityMethod)
      {
         var componentIcon = UnityIcon(typeName);
         if (componentIcon != null)
         {
            return componentIcon;
         }
      }

      var words = Words(title + " " + typeName);

      foreach (var rule in UnityTypeRules)
      {
         if (rule.Key.Any(words.Contains))
         {
            var unityIcon = UnityIcon(rule.Value);
            if (unityIcon != null)
            {
               return unityIcon;
            }
         }
      }

      foreach (var rule in DrawnRules)
      {
         if (rule.Key.Any(words.Contains))
         {
            return GetDrawn(rule.Value);
         }
      }

      foreach (var rule in FallbackUnityTypeRules)
      {
         if (rule.Key.Any(words.Contains))
         {
            var unityIcon = UnityIcon(rule.Value);
            if (unityIcon != null)
            {
               return unityIcon;
            }
         }
      }

      if (typeName.StartsWith("uScriptCon_", StringComparison.Ordinal))
      {
         return GetDrawn("compare");
      }

      return GetDrawn("action");
   }

   /// <summary>
   /// Lower-case words of a name, splitting "AnimatorCrossFade", "Set_Bool" and "uScriptAct_SetBool" alike.
   /// Neighbouring words are also added joined, so "Game Object" and "Nav Mesh" match "gameobject" and "navmesh".
   /// </summary>
   private static HashSet<string> Words(string text)
   {
      var ordered = new List<string>();
      var current = new System.Text.StringBuilder();

      for (int i = 0; i < text.Length; i++)
      {
         char c = text[i];
         bool letter = char.IsLetterOrDigit(c);
         bool camel = letter && i > 0 && char.IsUpper(c) && char.IsLower(text[i - 1]);

         if ((!letter || camel) && current.Length > 0)
         {
            ordered.Add(current.ToString().ToLowerInvariant());
            current.Length = 0;
         }

         if (letter)
         {
            current.Append(c);
         }
      }

      if (current.Length > 0)
      {
         ordered.Add(current.ToString().ToLowerInvariant());
      }

      var words = new HashSet<string>(ordered);
      for (int i = 0; i + 1 < ordered.Count; i++)
      {
         words.Add(ordered[i] + ordered[i + 1]);
      }

      return words;
   }

   /// <summary>Graphs used as nodes are generated classes without uScript's NodePath attribute.</summary>
   private static bool IsGraphType(string typeName)
   {
      if (typeName.StartsWith("uScript", StringComparison.Ordinal))
      {
         return false;
      }

      var type = ResolveType(typeName);
      if (type == null)
      {
         return false;
      }

      return !type.GetCustomAttributes(true).Any(a => a.GetType().Name == "NodePath");
   }

   private static Texture UnityIcon(string typeName)
   {
      var type = ResolveType(typeName);
      if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type))
      {
         return null;
      }

      var image = EditorGUIUtility.ObjectContent(null, type).image;
      return image;
   }

   private static Type ResolveType(string typeName)
   {
      if (string.IsNullOrEmpty(typeName))
      {
         return null;
      }

      Type type;
      if (Types.TryGetValue(typeName, out type))
      {
         return type;
      }

      type = null;
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
         try
         {
            type = assembly.GetType(typeName, false);
         }
         catch (Exception)
         {
            type = null;
         }

         if (type != null)
         {
            break;
         }
      }

      Types[typeName] = type;
      return type;
   }

   private static Texture2D GetDrawn(string name)
   {
      Texture2D texture;
      if (Drawn.TryGetValue(name, out texture) && texture != null)
      {
         return texture;
      }

      var path = uScriptConfig.ConstantPaths.RelativePath(uScriptConfig.ConstantPaths.SkinModern) + "/icons/uscript_icon_" + name + ".png";
      texture = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
      Drawn[name] = texture;
      return texture;
   }

   private static KeyValuePair<string[], string> Rule(string value, params string[] words)
   {
      return new KeyValuePair<string[], string>(words, value);
   }
}
