#uScript

Thank you for using uScript! Please use these links for helpful information:

** uScript Online Documentation **
A great resource of documentation, tutorials, and example projects you can use
to learn all about uScript!

http://docs.uscript.net/

For extending/customizing uScript, see our SDK Documentation:

http://docs.uscript.net/Development/


** uScript Community Website and Forum **

http://uscript.net


** Help & Support **
This code is provided as-is under the MIT license. Detox Studios has sunsetted uScript and no longer officially supports it. That being said, while it is feasible to do so, the uScript Community Forum will continue to reside at http://uscript.net/forum.


www.detoxstudios.com


** User Guide for this version **

See docs/USER_GUIDE.md (https://github.com/booman81/uscript/blob/develop/docs/USER_GUIDE.md)
for installing, Unity 6 notes and everything new: tabs, Quick Add, the Variables tab,
error links, reroute points, the minimap and faster saves.


** Installing uScript from GitHub (recommended) **

In Unity: Window > Package Manager > "+" > "Install package from git URL..." and enter:

    https://github.com/booman81/uscript.git?path=/uScript_Dev/Assets/uScript

To update later, select uScript in the Package Manager and click "Update" (or remove and re-add it).
Your graphs and generated code stay in Assets/uScriptProjectFiles and are not touched.

If you previously copied uScript into Assets/uScript, delete that folder first (keep
Assets/uScriptProjectFiles). Scenes keep their uScript components because the script GUIDs are unchanged.

Alternatively, copy the `uScript_Dev/Assets/uScript` folder into your project's `Assets` folder.

uScript compiles into its own assemblies (uScript.Runtime and uScript.Editor), so editing your own
scripts no longer recompiles uScript.


** Using uScript with Unity 6 (and 2020+) **

* Legacy input: uScript's input nodes use the classic `UnityEngine.Input` API. New Unity 6
  projects default to the Input System package only, so set
  Edit > Project Settings > Player > Other Settings > Active Input Handling to "Both"
  (or "Input Manager (Old)").
* Unity UI: the UI event/InputField nodes need the "Unity UI" (com.unity.ugui) package,
  which is installed by default in new projects.
* Legacy networking (Network/MasterServer) nodes were removed, as that API was removed
  from Unity in 2018.2.
* Rigidbody velocity, PhysicMaterial and the Hierarchy icon use the Unity 6 APIs
  (linearVelocity, PhysicsMaterial, EntityId) when compiled in Unity 6.


** New in this version **

* Quick Add: press Space on the canvas (or right-click > Search Nodes...), type part of a
  node's name or category, and press Enter to place it under the mouse. Arrow keys move
  the selection, Esc closes.
* TextMeshPro nodes (Actions > TextMeshPro): Set Text, Set Color, Set Font Size, Get Text.
  They appear automatically when TextMeshPro is available (built into Unity UI 2.0 in Unity 6).
* Input System nodes, available when the Input System package is installed:
  - Conditions > Input System > Input Action: Pressed / Released / Held / Not Held + value.
  - Events > Input Events > Input System Events: fires On Action Pressed / Released.
  Action names can be "Jump" or "Player/Jump" and work with Unity 6 project-wide actions
  and Player Input components.
* Variables tab (next to Toolbox / Contents, or Alt+V): one row per variable of the open
  graph with its type, value, Exposed marker and number of nodes. Click to select and jump
  to its nodes (click again for the next one), double-click or right-click to rename,
  edit the value and Exposed/Hidden settings, + to create a variable, drag a variable onto
  the canvas to place another node of it. Warns when two variables share a name but not a type.
* Console errors from graphs: an extra Console line names the graph and node that caused
  the error, with a "Show node in uScript" link that opens the graph and selects the node.
  (Graphs saved with this version include a small node map in their generated code.)
* Rename / Delete Graph from the graph list or File menu (other graphs that use a renamed
  graph are updated).
* Canvas navigation: F frames the selected nodes (all nodes if none are selected), A frames
  all nodes (also in the right-click and View menus). The mouse wheel and the -/= keys
  zoom toward the mouse.
* Sockets are easier to hit and highlight when the mouse is over them.
* Drag a link onto empty canvas: Quick Add opens with only the nodes that can connect to
  that socket, and the chosen node is placed and connected.
* Graph tabs: several graphs open at once. Unsaved changes stay in their tab when you
  switch; closing a changed tab asks to save. Middle-click or x closes, right-click for more.
* Minimap in the bottom-right corner: click or drag to move the view, M to show/hide.
* Reroute points: double-click a link to add a point (drag it), double-click a point to
  remove it. Saved in the graph; older graphs open unchanged.
* File > Compile Graphs Separately (Faster Saves): generated graph code is compiled as its
  own assembly (uScriptGraphs, plus uScriptNodes for your custom nodes), so saving a graph
  no longer recompiles all your scripts. uScript checks first that graphs and nodes don't
  use scripts from Assembly-CSharp (those would have to move into an asmdef), and keeps
  the assembly references up to date when you save. Turn it off in the same menu.
* Removed: the update check, license/promotion/welcome windows and other features that
  depended on the old Detox Studios servers. Help > User Guide opens the bundled PDF.
