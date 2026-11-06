#if USCRIPT_INPUTSYSTEM
// uScript Event Node
// Input System support (only compiled when the Input System package is installed)

using UnityEngine;
using UnityEngine.InputSystem;

[NodeAutoAssignMasterInstance(true)]

[NodePath("Events/Input Events")]

[NodeCopyright("Copyright 2026 uScript contributors (MIT)")]
[NodeToolTip("Fires when any enabled Input System action is pressed or released.")]
[NodeAuthor("uScript contributors", "https://github.com/booman81/uscript")]
[NodeHelp("https://github.com/booman81/uscript")]

[FriendlyName("Input System Events", "Fires when any enabled Input System action (for example the project-wide actions in Unity 6, or the actions of a Player Input component) is pressed or released." +
   "\n\nUse the Action Name output with a Compare String node to react to a specific action, or use the Input Action condition node.")]
public class uScript_InputSystemEvents : uScriptEvent
{
   public delegate void uScriptEventHandler(object sender, InputActionEventArgs args);

   public class InputActionEventArgs : System.EventArgs
   {
      private readonly string m_ActionName;
      private readonly string m_ActionMap;
      private readonly float m_Value;
      private readonly Vector2 m_Vector2Value;

      [FriendlyName("Action Name", "The name of the action, e.g. \"Jump\".")]
      public string ActionName { get { return m_ActionName; } }

      [FriendlyName("Action Map", "The name of the action map the action belongs to, e.g. \"Player\".")]
      [SocketState(false, false)]
      public string ActionMap { get { return m_ActionMap; } }

      [FriendlyName("Value", "The current value: 0-1 for buttons and triggers, the length of the vector for sticks.")]
      [SocketState(false, false)]
      public float Value { get { return m_Value; } }

      [FriendlyName("Vector2 Value", "The current value for sticks / 2D vectors (buttons and axes use X).")]
      [SocketState(false, false)]
      public Vector2 Vector2Value { get { return m_Vector2Value; } }

      public InputActionEventArgs(InputAction action)
      {
         m_ActionName = action.name;
         m_ActionMap = action.actionMap != null ? action.actionMap.name : string.Empty;
         uScriptInputSystemUtility.ReadValue(action, out m_Value, out m_Vector2Value);
      }
   }

   [FriendlyName("On Action Pressed")]
   public event uScriptEventHandler OnActionPressed;

   [FriendlyName("On Action Released")]
   public event uScriptEventHandler OnActionReleased;

   void Update()
   {
      if (OnActionPressed == null && OnActionReleased == null)
      {
         return;
      }

      foreach (InputAction action in uScriptInputSystemUtility.GetEnabledActions().ToArray())
      {
         if (OnActionPressed != null && action.WasPressedThisFrame())
         {
            OnActionPressed(this, new InputActionEventArgs(action));
         }

         if (OnActionReleased != null && action.WasReleasedThisFrame())
         {
            OnActionReleased(this, new InputActionEventArgs(action));
         }
      }
   }
}
#endif
