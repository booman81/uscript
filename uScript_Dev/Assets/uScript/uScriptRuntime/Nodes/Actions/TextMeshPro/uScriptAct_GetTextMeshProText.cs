#if USCRIPT_TEXTMESHPRO
// uScript Action Node
// TextMeshPro support (only compiled when TextMeshPro is installed)

using UnityEngine;
using TMPro;

[NodePath("Actions/TextMeshPro")]

[NodeCopyright("Copyright 2026 uScript contributors (MIT)")]
[NodeToolTip("Gets the text, color and font size of a TextMeshPro component.")]
[NodeAuthor("uScript contributors", "https://github.com/booman81/uscript")]
[NodeHelp("https://github.com/booman81/uscript")]

[FriendlyName("Get TextMeshPro Text", "Gets the text, color and font size of the TextMeshPro component (TextMeshPro or TextMeshPro - Text (UI)) on the target GameObject.")]
public class uScriptAct_GetTextMeshProText : uScriptLogic
{
   private bool m_Found;

   public bool Out { get { return true; } }

   [FriendlyName("Found", "Fires when the target has a TextMeshPro component.")]
   public bool Found { get { return m_Found; } }

   [FriendlyName("Not Found", "Fires when the target has no TextMeshPro component.")]
   public bool NotFound { get { return !m_Found; } }

   public void In(
      [FriendlyName("Target", "The GameObject with a TextMeshPro component."), AutoLinkType(typeof(GameObject))]
      GameObject Target,

      [FriendlyName("Text", "The current text.")]
      out string Text,

      [FriendlyName("Color", "The current text color.")]
      [SocketState(false, false)]
      out Color Color,

      [FriendlyName("Font Size", "The current font size.")]
      [SocketState(false, false)]
      out float FontSize
      )
   {
      Text = string.Empty;
      Color = UnityEngine.Color.white;
      FontSize = 0f;

      TMP_Text textComponent = Target != null ? Target.GetComponent<TMP_Text>() : null;
      m_Found = textComponent != null;

      if (m_Found)
      {
         Text = textComponent.text;
         Color = textComponent.color;
         FontSize = textComponent.fontSize;
      }
   }
}
#endif
