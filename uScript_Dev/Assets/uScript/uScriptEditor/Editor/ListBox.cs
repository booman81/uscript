// List Box: a canvas box that reads or writes one item of a list, picked by position or by key.
//
// A List Box is stored as a property box (EntityProperty), so saving, loading, copy/paste, undo, links and
// the code generator's "read before use / write after set" handling all work as they do for property boxes.
// What makes it a List Box is its ComponentType, which starts with "uScript_ListBox" and also holds its settings:
//
//    uScript_ListBox|<Position or Key>|<selector>|<1 if the selector is a variable name, else 0>
//
//    Instance  = the List socket (its Type is the list type, such as System.String[])
//    Parameter = the Value socket (its Type is what the box gives out)

using System;
using System.Collections.Generic;

namespace Detox.ScriptEditor
{
   public static class ListBox
   {
      public const string Prefix = "uScript_ListBox";

      public const string ValueName = "Value";

      /// <summary>The socket next to List that takes the position or key from a variable.</summary>
      public const string SelectorName = "Selector";

      /// <summary>The Position/Key socket. It accepts any variable: numbers, text, or others.</summary>
      public static Parameter SelectorParameter(EntityProperty box)
      {
         var selector = new Parameter();
         selector.Name = SelectorName;
         selector.FriendlyName = Read(box).FindBy == uScript_ListBoxFindBy.Key ? "Key" : "Position";
         selector.Type = "System.Object";
         selector.AutoLinkType = "System.Object";
         selector.Default = string.Empty;
         selector.ReferenceGuid = string.Empty;
         selector.Input = true;
         selector.Output = false;
         selector.State = Parameter.VisibleState.Visible;
         return selector;
      }

      /// <summary>The socket that takes the Default Value from a variable, when Show Default Socket is ticked.</summary>
      public const string DefaultName = "DefaultValue";

      public static Parameter DefaultParameter(EntityProperty box)
      {
         var missing = new Parameter();
         missing.Name = DefaultName;
         missing.FriendlyName = "Default";
         missing.Type = box.Parameter.Type;
         missing.AutoLinkType = box.Parameter.Type;
         missing.Default = Read(box).Default ?? string.Empty;
         missing.ReferenceGuid = string.Empty;
         missing.Input = true;
         missing.Output = false;
         missing.State = Parameter.VisibleState.Visible;
         return missing;
      }

      /// <summary>The extra bottom sockets after List: Position/Key, and Default when shown.</summary>
      public static Parameter[] ExtraParameters(EntityProperty box)
      {
         if (Read(box).ShowDefaultSocket && HasDefault(box))
         {
            return new[] { SelectorParameter(box), DefaultParameter(box) };
         }

         return new[] { SelectorParameter(box) };
      }

      /// <summary>One of the extra sockets by name, or Parameter.Empty.</summary>
      public static Parameter ExtraParameter(EntityNode node, string name)
      {
         if (!Is(node)) return Parameter.Empty;

         foreach (Parameter p in ExtraParameters((EntityProperty)node))
         {
            if (p.Name == name) return p;
         }

         return Parameter.Empty;
      }

      /// <summary>True when a link is connected to the box's Position/Key socket.</summary>
      public static bool SelectorLinked(ScriptEditor script, EntityProperty box)
      {
         return SocketLinked(script, box, SelectorName);
      }

      public static bool SocketLinked(ScriptEditor script, EntityProperty box, string socketName)
      {
         if (script == null) return false;

         foreach (LinkNode link in script.Links)
         {
            if (link.Destination.Guid == box.Guid && link.Destination.Anchor == socketName) return true;
         }

         return false;
      }

      public struct Settings
      {
         public uScript_ListBoxFindBy FindBy;

         /// <summary>A position ("3", "-1"), a key ("Health"), or a variable's name when SelectorIsVariable is set.</summary>
         public string Selector;

         public bool SelectorIsVariable;

         /// <summary>Key mode: writing to a missing key adds the key and its value to the end of the list.</summary>
         public bool AddIfMissing;

         /// <summary>What the box gives when the item is missing. Empty = 0, empty text or False.</summary>
         public string Default;

         /// <summary>Shows a Default socket after List and Position/Key, to take the default from a variable.</summary>
         public bool ShowDefaultSocket;
      }

      /// <summary>The list types offered in the Add menu, with their menu names.</summary>
      public static readonly KeyValuePair<string, string>[] ListTypes =
      {
         new KeyValuePair<string, string>("String List", "System.String[]"),
         new KeyValuePair<string, string>("Int List", "System.Int32[]"),
         new KeyValuePair<string, string>("Float List", "System.Single[]"),
         new KeyValuePair<string, string>("Bool List", "System.Boolean[]"),
         new KeyValuePair<string, string>("GameObject List", "UnityEngine.GameObject[]"),
         new KeyValuePair<string, string>("Vector3 List", "UnityEngine.Vector3[]"),
         new KeyValuePair<string, string>("Object List", "UnityEngine.Object[]"),
      };

      public static bool Is(EntityNode node)
      {
         return node is EntityProperty && Is(((EntityProperty)node).ComponentType);
      }

      public static bool Is(string componentType)
      {
         return componentType != null &&
                (componentType == Prefix || componentType.StartsWith(Prefix + "|", StringComparison.Ordinal));
      }

      public static bool IsStringList(EntityProperty box)
      {
         return box.Instance.Type == "System.String[]";
      }

      /// <summary>A new List Box for a list of the given type. String lists start in Key mode, others by position 1.</summary>
      public static EntityProperty Create(string listType)
      {
         bool strings = listType == "System.String[]";
         string valueType = listType.Replace("[]", string.Empty);

         var settings = new Settings
         {
            FindBy = strings ? uScript_ListBoxFindBy.Key : uScript_ListBoxFindBy.Position,
            Selector = strings ? string.Empty : "1",
            SelectorIsVariable = false
         };

         var box = new EntityProperty(ValueName, ValueName, Write(settings), valueType, true, true);

         Parameter list = box.Instance;
         list.FriendlyName = "List";
         list.Type = listType;
         list.AutoLinkType = listType;
         box.Instance = list;

         return box;
      }

      public static Settings Read(EntityProperty box)
      {
         return Read(box.ComponentType);
      }

      public static Settings Read(string componentType)
      {
         var settings = new Settings { FindBy = uScript_ListBoxFindBy.Position, Selector = "1", SelectorIsVariable = false };
         if (!Is(componentType))
         {
            return settings;
         }

         string[] parts = componentType.Split('|');
         if (parts.Length > 1 && parts[1] == "Key")
         {
            settings.FindBy = uScript_ListBoxFindBy.Key;
         }

         if (parts.Length > 2)
         {
            settings.Selector = Unescape(parts[2]);
         }

         if (parts.Length > 3)
         {
            settings.SelectorIsVariable = parts[3] == "1";
         }

         if (parts.Length > 4)
         {
            settings.AddIfMissing = parts[4] == "1";
         }

         if (parts.Length > 5)
         {
            settings.Default = Unescape(parts[5]);
         }

         if (parts.Length > 6)
         {
            settings.ShowDefaultSocket = parts[6] == "1";
         }

         return settings;
      }

      public static string Write(Settings settings)
      {
         return Prefix + "|" + settings.FindBy + "|" + Escape(settings.Selector ?? string.Empty) + "|" + (settings.SelectorIsVariable ? "1" : "0") + "|" + (settings.AddIfMissing ? "1" : "0") + "|" + Escape(settings.Default ?? string.Empty) + "|" + (settings.ShowDefaultSocket ? "1" : "0");
      }

      /// <summary>Second line of the box, such as: Key "Health" · float, or: Position = Selected_Index.</summary>
      public static string Describe(EntityProperty box)
      {
         return Describe(box, false);
      }

      public static string Describe(EntityProperty box, bool selectorLinked)
      {
         Settings settings = Read(box);
         string selector = settings.Selector ?? string.Empty;
         string text;

         if (selectorLinked)
         {
            text = settings.FindBy == uScript_ListBoxFindBy.Key ? "Key from socket" : "Position from socket";
         }
         else if (settings.SelectorIsVariable)
         {
            text = settings.FindBy == uScript_ListBoxFindBy.Key ? "Key from variable" : "Position from variable";
         }
         else if (selector.Length == 0)
         {
            text = settings.FindBy == uScript_ListBoxFindBy.Key ? "Key not set" : "Position not set";
         }
         else
         {
            text = settings.FindBy == uScript_ListBoxFindBy.Key ? "By Key" : "By Position";
         }

         if (IsStringList(box) && box.Parameter.Type != "System.String")
         {
            text += " · " + uScriptConfig.Variable.FriendlyName(box.Parameter.Type);
         }

         return text;
      }

      /// <summary>First line of the box: the kind of list, such as (String List).</summary>
      public static string Title(EntityProperty box)
      {
         foreach (KeyValuePair<string, string> pair in ListTypes)
         {
            if (pair.Value == box.Instance.Type)
            {
               return "(" + pair.Key + ")";
            }
         }

         return "(" + uScriptConfig.Variable.FriendlyName(box.Instance.Type) + ")";
      }

      // ------------------------------------------------------------ Reference panel help

      public const string ReferenceText =
         "Reads or writes one item of a list, with no node in between. It works like a property box: " +
         "link it into a node input to read the item, or link a node output into it to write the item back to the list.\n\n" +
         "HOW TO USE\n" +
         "1. Link your list variable into the List socket at the bottom of the box.\n" +
         "2. Select the box and choose the item in the Properties panel.\n" +
         "3. Link the box into the nodes that need the value.\n\n" +
         "CHOOSING THE ITEM\n" +
         "\u2022 Find By Position: 1 = first item, 2 = second, -1 = last, -2 = second to last.\n" +
         "\u2022 Find By Key (text lists only): for a list like \"Health, 3, Damage, 6\", the key Health gives 3. " +
         "Keys ignore upper/lower case and spaces around them.\n" +
         "\u2022 Position/Key socket: link a variable into the second socket at the bottom of the box (next to List). " +
         "Its value picks the item, instead of what is typed in the Properties panel.\n" +
         "\u2022 Use Variable: turn it on and type the name of a graph variable (for example Selected_Index) " +
         "in the Position or Key field. The variable's current value picks the item every time the box is used.\n" +
         "\u2022 Give Out As (text lists only): Text, Whole Number, Decimal or True/False. " +
         "\"12.5\" and \"12,5\" both read as 12.5; \"true\", \"1\", \"yes\" and \"on\" read as True.\n\n" +
         "EXAMPLES\n" +
         "\u2022 Stats list \"Health, 100, Damage, 6\": a box with Key Health, Give Out As Whole Number, gives 100. " +
         "Link it into Add Int, then link the result back into the box to save the new health.\n" +
         "\u2022 Int list \"1, 12, 56\": Position 3 gives 56. Position -1 also gives 56.\n" +
         "\u2022 GameObject list of spawn points: Use Variable on, Position = Spawn_Index, gives the current spawn point.\n\n" +
         "GOOD TO KNOW\n" +
         "\u2022 The value is read fresh each time a node uses it, so it always matches the list.\n" +
         "\u2022 A position outside the list, or a missing key, reads as empty, 0 or False, and writing to it does nothing. " +
         "Add new items with a list node.\n" +
         "\u2022 Default Value: what the box gives when the key or position is missing (for example 100). " +
         "Empty means 0, empty text or False. It is only for reading; it never changes the list. " +
         "Tick Show Default Socket to add a Default socket after Position/Key and link a variable into it instead.\n" +
         "\u2022 Add Key If Missing (Key mode): writing to a key that is not in the list adds the key and its value to the end, " +
         "for example writing 50 to Mana adds \"Mana, 50\".\n" +
         "\u2022 The box title shows the list type, and the line under it shows what it picks. " +
         "Key not set or Position not set means nothing is chosen yet.\n" +
         "\u2022 After changing Give Out As, re-link the box if a link no longer fits the new type.";

      public static string ParameterHelp(string parameterName)
      {
         switch (parameterName)
         {
            case ValueName:
               return "The chosen list item. Link it into a node input to read it; link a node output into it to write the item back to the list.";
            case "Instance":
               return "The list to read from and write to. Link a list variable here (one link only).";
            case DefaultName:
               return "Optional. Link a variable here to set the Default Value: what the box gives when the item is missing. " +
                      "When linked, it is used instead of the Default Value typed in the Properties panel.";
            case SelectorName:
               return "Optional. Link a variable here to choose the item: a number for Position, or text for Key. " +
                      "When linked, it is used instead of the Position or Key typed in the Properties panel.";
            default:
               return parameterName;
         }
      }

      // ------------------------------------------------------------ properties panel

      public const string PanelKey = "List Box";

      /// <summary>The settings shown in the properties panel, as editable fields.</summary>
      public static Parameter[] PanelParameters(EntityProperty box)
      {
         Settings settings = Read(box);
         var parameters = new List<Parameter>();

         if (IsStringList(box))
         {
            parameters.Add(Field("ListBoxFindBy", "Find By", typeof(uScript_ListBoxFindBy).ToString(), settings.FindBy.ToString()));
         }

         parameters.AddRange(new[]
         {
            Field("ListBoxSelector", settings.FindBy == uScript_ListBoxFindBy.Key ? "Key" : "Position", "System.String", settings.Selector ?? string.Empty),
            Field("ListBoxSelectorIsVariable", "Use Variable", "System.Boolean", settings.SelectorIsVariable ? "true" : "false"),
         });

         if (IsStringList(box))
         {
            parameters.Add(Field("ListBoxAddIfMissing", "Add Key If Missing", "System.Boolean", settings.AddIfMissing ? "true" : "false"));
            parameters.Add(Field("ListBoxOutput", "Give Out As", typeof(uScript_ListBoxOutput).ToString(), OutputFor(box.Parameter.Type).ToString()));
         }

         if (HasDefault(box))
         {
            parameters.Add(Field("ListBoxDefault", "Default Value", "System.String", settings.Default ?? string.Empty));
            parameters.Add(Field("ListBoxShowDefaultSocket", "Show Default Socket", "System.Boolean", settings.ShowDefaultSocket ? "true" : "false"));
         }

         return parameters.ToArray();
      }

      /// <summary>Applies edited panel fields to the box. Returns the changed box.</summary>
      public static EntityProperty ApplyPanel(EntityProperty box, Parameter[] fields)
      {
         Settings settings = Read(box);
         string valueType = box.Parameter.Type;

         foreach (Parameter field in fields)
         {
            switch (field.Name)
            {
               case "ListBoxFindBy":
                  settings.FindBy = field.Default == "Key" ? uScript_ListBoxFindBy.Key : uScript_ListBoxFindBy.Position;
                  break;
               case "ListBoxSelector":
                  settings.Selector = (field.Default ?? string.Empty).Trim();
                  break;
               case "ListBoxSelectorIsVariable":
                  settings.SelectorIsVariable = field.Default == "true";
                  break;
               case "ListBoxDefault":
                  settings.Default = (field.Default ?? string.Empty).Trim();
                  break;
               case "ListBoxShowDefaultSocket":
                  settings.ShowDefaultSocket = field.Default == "true";
                  break;
               case "ListBoxAddIfMissing":
                  settings.AddIfMissing = field.Default == "true";
                  break;
               case "ListBoxOutput":
                  if (IsStringList(box))
                  {
                     valueType = TypeFor(ParseOutput(field.Default));
                  }

                  break;
            }
         }

         //only text lists have keys
         if (!IsStringList(box))
         {
            settings.FindBy = uScript_ListBoxFindBy.Position;
            settings.AddIfMissing = false;
         }

         box.ComponentType = Write(settings);

         if (valueType != box.Parameter.Type)
         {
            Parameter value = box.Parameter;
            value.Type = valueType;
            value.AutoLinkType = valueType;
            value.Default = string.Empty;
            box.Parameter = value;
         }

         return box;
      }

      /// <summary>Text, number and true/false lists can have a default value for missing items.</summary>
      public static bool HasDefault(EntityProperty box)
      {
         switch (box.Parameter.Type)
         {
            case "System.String":
            case "System.Int32":
            case "System.Single":
            case "System.Boolean":
               return true;
            default:
               return false;
         }
      }

      /// <summary>The default value as C# code of the box's value type, or null when none is set.</summary>
      public static string DefaultCode(EntityProperty box)
      {
         string text = Read(box).Default;
         if (string.IsNullOrEmpty(text) || !HasDefault(box)) return null;

         switch (box.Parameter.Type)
         {
            case "System.Int32":
               return uScript_ListBox.ToInt(text).ToString(System.Globalization.CultureInfo.InvariantCulture);
            case "System.Single":
               return uScript_ListBox.ToFloat(text).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f";
            case "System.Boolean":
               return uScript_ListBox.ToBool(text) ? "true" : "false";
            default:
               return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
         }
      }

      public static uScript_ListBoxOutput OutputFor(string valueType)
      {
         switch (valueType)
         {
            case "System.Int32": return uScript_ListBoxOutput.WholeNumber;
            case "System.Single": return uScript_ListBoxOutput.Decimal;
            case "System.Boolean": return uScript_ListBoxOutput.TrueFalse;
            default: return uScript_ListBoxOutput.Text;
         }
      }

      public static string TypeFor(uScript_ListBoxOutput output)
      {
         switch (output)
         {
            case uScript_ListBoxOutput.WholeNumber: return "System.Int32";
            case uScript_ListBoxOutput.Decimal: return "System.Single";
            case uScript_ListBoxOutput.TrueFalse: return "System.Boolean";
            default: return "System.String";
         }
      }

      private static uScript_ListBoxOutput ParseOutput(string text)
      {
         try
         {
            return (uScript_ListBoxOutput)Enum.Parse(typeof(uScript_ListBoxOutput), text);
         }
         catch (Exception)
         {
            return uScript_ListBoxOutput.Text;
         }
      }

      private static Parameter Field(string name, string friendlyName, string type, string value)
      {
         var field = new Parameter();
         field.Name = name;
         field.FriendlyName = friendlyName;
         field.Type = type;
         field.AutoLinkType = type;
         field.Default = value;
         field.ReferenceGuid = string.Empty;
         field.Input = true;
         field.Output = false;
         field.State = Parameter.VisibleState.Hidden | Parameter.VisibleState.Locked;
         return field;
      }

      private static string Escape(string text)
      {
         return text.Replace("%", "%25").Replace("|", "%7C");
      }

      private static string Unescape(string text)
      {
         return text.Replace("%7C", "|").Replace("%25", "%");
      }
   }
}
