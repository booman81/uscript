#if USCRIPT_INPUTSYSTEM
// uScript Condition Node
// Input System support (only compiled when the Input System package is installed)

using UnityEngine;
using UnityEngine.InputSystem;

[NodePath("Conditions/Input System")]

[NodeCopyright("Copyright 2026 uScript contributors (MIT)")]
[NodeToolTip("Checks an Input System action: pressed this frame, released this frame, held, and its current value.")]
[NodeAuthor("uScript contributors", "https://github.com/booman81/uscript")]
[NodeHelp("https://github.com/booman81/uscript")]

[FriendlyName("Input Action", "Checks the state of an enabled Input System action (for example the project-wide actions in Unity 6, or the actions of a Player Input component)." +
   "\n\nCall this every frame, for example from an Update event, and use the output that matches what you need." +
   "\n\nAction Name can be just the action (\"Jump\") or include the action map (\"Player/Jump\").")]
public class uScriptCon_InputAction : uScriptLogic
{
   private bool m_Pressed;
   private bool m_Released;
   private bool m_Held;

   [FriendlyName("Pressed", "Fires on the frame the action was pressed.")]
   public bool Pressed { get { return m_Pressed; } }

   [FriendlyName("Released", "Fires on the frame the action was released.")]
   public bool Released { get { return m_Released; } }

   [FriendlyName("Held", "Fires every frame while the action is held down.")]
   public bool Held { get { return m_Held; } }

   [FriendlyName("Not Held", "Fires every frame while the action is not held down (or the action was not found).")]
   public bool NotHeld { get { return !m_Held; } }

   public void In(
      [FriendlyName("Action Name", "The name of the action, e.g. \"Jump\" or \"Player/Jump\".")]
      [DefaultValue("Jump")]
      string ActionName,

      [FriendlyName("Value", "The current value: 0-1 for buttons and triggers, the length of the vector for sticks.")]
      [SocketState(false, false)]
      out float Value,

      [FriendlyName("Vector2 Value", "The current value for sticks / 2D vectors (buttons and axes use X).")]
      [SocketState(false, false)]
      out Vector2 Vector2Value
      )
   {
      InputAction action = uScriptInputSystemUtility.FindAction(ActionName);

      m_Pressed = action != null && action.WasPressedThisFrame();
      m_Released = action != null && action.WasReleasedThisFrame();
      m_Held = action != null && action.IsPressed();

      uScriptInputSystemUtility.ReadValue(action, out Value, out Vector2Value);
   }
}
#endif
