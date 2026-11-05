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
* Legacy networking (Network/MasterServer) nodes are excluded automatically, as that API
  was removed from Unity in 2018.2.
* Rigidbody velocity, PhysicMaterial and the Hierarchy icon use the Unity 6 APIs
  (linearVelocity, PhysicsMaterial, EntityId) when compiled in Unity 6.
