#if USCRIPT_TEXTMESHPRO
// uScript Action Node
// TextMeshPro support (only compiled when TextMeshPro is installed)

using UnityEngine;
using TMPro;

[NodePath("Actions/TextMeshPro")]

[NodeCopyright("Copyright 2026 uScript contributors (MIT)")]
[NodeToolTip("Sets the font size of TextMeshPro components (3D text or UI text).")]
[NodeAuthor("uScript contributors", "https://github.com/booman81/uscript")]
[NodeHelp("https://github.com/booman81/uscript")]

[FriendlyName("Set TextMeshPro Font Size", "Sets the font size of the TextMeshPro component on the target GameObjects. Turns off Auto Size so the new size is used.")]
public class uScriptAct_SetTextMeshProFontSize : uScriptLogic
{
   public bool Out { get { return true; } }

   public void In(
      [FriendlyName("Target", "The GameObject(s) with a TextMeshPro component."), AutoLinkType(typeof(GameObject))]
      GameObject[] Target,

      [FriendlyName("Font Size", "The new font size.")]
      [DefaultValue(36f)]
      float FontSize
      )
   {
      foreach (GameObject currentTarget in Target)
      {
         if (currentTarget == null) continue;

         TMP_Text textComponent = currentTarget.GetComponent<TMP_Text>();
         if (textComponent != null)
         {
            textComponent.enableAutoSizing = false;
            textComponent.fontSize = FontSize;
         }
      }
   }
}
#endif
