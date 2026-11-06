#if USCRIPT_TEXTMESHPRO
// uScript Action Node
// TextMeshPro support (only compiled when TextMeshPro is installed)

using UnityEngine;
using TMPro;

[NodePath("Actions/TextMeshPro")]

[NodeCopyright("Copyright 2026 uScript contributors (MIT)")]
[NodeToolTip("Sets the text of TextMeshPro components (3D text or UI text).")]
[NodeAuthor("uScript contributors", "https://github.com/booman81/uscript")]
[NodeHelp("https://github.com/booman81/uscript")]

[FriendlyName("Set TextMeshPro Text", "Sets the text of the TextMeshPro component (TextMeshPro or TextMeshPro - Text (UI)) on the target GameObjects.")]
public class uScriptAct_SetTextMeshProText : uScriptLogic
{
   public bool Out { get { return true; } }

   public void In(
      [FriendlyName("Target", "The GameObject(s) with a TextMeshPro component."), AutoLinkType(typeof(GameObject))]
      GameObject[] Target,

      [FriendlyName("Text", "The new text.")]
      string Text
      )
   {
      foreach (GameObject currentTarget in Target)
      {
         if (currentTarget == null) continue;

         TMP_Text textComponent = currentTarget.GetComponent<TMP_Text>();
         if (textComponent != null)
         {
            textComponent.text = Text;
         }
      }
   }
}
#endif
