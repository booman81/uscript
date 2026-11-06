#if USCRIPT_TEXTMESHPRO
// uScript Action Node
// TextMeshPro support (only compiled when TextMeshPro is installed)

using UnityEngine;
using TMPro;

[NodePath("Actions/TextMeshPro")]

[NodeCopyright("Copyright 2026 uScript contributors (MIT)")]
[NodeToolTip("Sets the color of TextMeshPro components (3D text or UI text).")]
[NodeAuthor("uScript contributors", "https://github.com/booman81/uscript")]
[NodeHelp("https://github.com/booman81/uscript")]

[FriendlyName("Set TextMeshPro Color", "Sets the vertex color of the TextMeshPro component on the target GameObjects.")]
public class uScriptAct_SetTextMeshProColor : uScriptLogic
{
   public bool Out { get { return true; } }

   public void In(
      [FriendlyName("Target", "The GameObject(s) with a TextMeshPro component."), AutoLinkType(typeof(GameObject))]
      GameObject[] Target,

      [FriendlyName("Color", "The new text color.")]
      [DefaultValue(typeof(Color), new float[] { 1f, 1f, 1f, 1f })]
      Color Color
      )
   {
      foreach (GameObject currentTarget in Target)
      {
         if (currentTarget == null) continue;

         TMP_Text textComponent = currentTarget.GetComponent<TMP_Text>();
         if (textComponent != null)
         {
            textComponent.color = Color;
         }
      }
   }
}
#endif
