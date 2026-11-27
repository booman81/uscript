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

      public struct Settings
      {
         public uScript_ListBoxFindBy FindBy;

         /// <summary>A position ("3", "-1"), a key ("Health"), or a variable's name when SelectorIsVariable is set.</summary>
         public string Selector;

         public bool SelectorIsVariable;
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

         return settings;
      }

      public static string Write(Settings settings)
      {
         return Prefix + "|" + settings.FindBy + "|" + Escape(settings.Selector ?? string.Empty) + "|" + (settings.SelectorIsVariable ? "1" : "0");
      }

      /// <summary>Second line of the box, such as: Key "Health" · float, or: Position = Selected_Index.</summary>
      public static string Describe(EntityProperty box)
      {
         Settings settings = Read(box);
         string selector = settings.Selector ?? string.Empty;
         string text;

         if (settings.SelectorIsVariable)
         {
            text = (settings.FindBy == uScript_ListBoxFindBy.Key ? "Key = " : "Position = ") + (selector.Length > 0 ? selector : "?");
         }
         else if (settings.FindBy == uScript_ListBoxFindBy.Key)
         {
            text = "Key \"" + selector + "\"";
         }
         else
         {
            text = "Position " + (selector.Length > 0 ? selector : "?");
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
            Field("ListBoxSelector", settings.FindBy == uScript_ListBoxFindBy.Key ? "Key" : "Position (1 = first, -1 = last)", "System.String", settings.Selector ?? string.Empty),
            Field("ListBoxSelectorIsVariable", "Use Variable (type its name)", "System.Boolean", settings.SelectorIsVariable ? "true" : "false"),
         });

         if (IsStringList(box))
         {
            parameters.Add(Field("ListBoxOutput", "Give Out As", typeof(uScript_ListBoxOutput).ToString(), OutputFor(box.Parameter.Type).ToString()));
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
