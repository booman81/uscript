# uScript for Unity 6: User Guide

This guide covers installing uScript and everything that is new or changed compared with the original uScript 1.x (2015).
For the basics of visual scripting in uScript (nodes, links, variables, events, nested graphs), the original
**uScript User Guide** PDF still applies. Open it from **Help > User Guide (PDF)** in the uScript window.

- [Installing](#installing)
- [Unity 6 notes](#unity-6-notes)
- [Working with graphs](#working-with-graphs)
- [Adding nodes](#adding-nodes)
- [Links](#links)
- [Moving around the canvas](#moving-around-the-canvas)
- [Variables tab](#variables-tab)
- [Finding errors](#finding-errors)
- [Faster saves](#faster-saves)
- [New nodes](#new-nodes)
- [Shortcuts](#shortcuts)
- [What was removed](#what-was-removed)

---

## Installing

uScript is installed as a package straight from GitHub.

1. In Unity, open **Window > Package Manager**.
2. Click **+ > Install package from git URL...**
3. Enter one of these:

| Version | URL |
|---|---|
| Stable (the original uScript, fixed for Unity 6) | `https://github.com/booman81/uscript.git?path=/uScript_Dev/Assets/uScript#stable` |
| Develop (all new features in this guide) | `https://github.com/booman81/uscript.git?path=/uScript_Dev/Assets/uScript#develop` |

Open uScript with **Ctrl+U** (Cmd+U on Mac) or from **Tools > uScript > uScript Editor**. The menu location can be changed in the uScript Preferences.

**Your work stays in `Assets/uScriptProjectFiles`**: graphs, generated code and custom nodes. Updating or reinstalling
the package never touches it.

### Updating

Select uScript in the Package Manager and click **Update**. If Unity keeps the old version, remove the package
and add it again, or delete the `com.detoxstudios.uscript` block in `Packages/packages-lock.json`.

### Moving an existing project to the package

1. Back up the project (or make sure it is committed).
2. Delete the old `Assets/uScript` folder. **Keep** `Assets/uScriptProjectFiles`.
3. Install the package as above.
4. Open uScript and use **File > Rebuild All Graphs** once.

Scenes keep their uScript components, because the script GUIDs are unchanged. The same package works in Unity 2021.3, 2022 and Unity 6.

---

## Unity 6 notes

- **Input:** the classic input nodes use `UnityEngine.Input`. New Unity 6 projects use only the Input System by
  default, so set **Edit > Project Settings > Player > Other Settings > Active Input Handling** to **Both**,
  or use the new [Input System nodes](#new-nodes).
- **Unity UI:** the UI event nodes need the Unity UI package (`com.unity.ugui`), which new projects have by default.
- **Removed Unity APIs:** the legacy networking nodes (Network / MasterServer) are gone because Unity removed that
  API. Physics nodes use the Unity 6 names (`linearVelocity`, `PhysicsMaterial`) automatically.
- **Linear color space:** the uScript editor looks the same in Linear and Gamma projects.

---

## Working with graphs

### Tabs

Every graph you open gets a tab under the toolbar.

- Click a tab to switch graphs. Unsaved changes stay in their tab, and a `*` marks changed graphs. You are only asked
  to save when you close a changed tab.
- Close a tab with its **×** or with a middle-click. Right-click a tab for **Close**, **Close Other Tabs** and
  **Show in Project**.
- **+** at the right creates a new graph in a new tab.
- Open tabs come back the next time you open uScript.
- Undo history is per session of a tab: switching tabs clears it.
- Entering Play mode warns you if a graph in another tab has unsaved changes.

### Rename and delete

Right-click a graph in the graph list, or use the **File** menu for the open graph:

- **Rename Graph...** renames the `.uscript` file and its generated code. Other graphs that use the renamed graph
  (as a nested graph) are updated automatically, so nothing breaks.
- **Delete Graph** moves the graph and its generated code to the trash. A graph that other graphs still use can't be
  deleted. uScript tells you which graphs use it.

### Save & Play

**Save & Play**, next to the Save button in the Current Graph panel (also **File > Save and Play**), saves the graph
with generated code and starts the game as soon as Unity has compiled it. One click instead of save, wait, Play.
It saves as Release, or as Debug when Debug is your chosen save method.

Pressing Play yourself right after saving also waits for the new code to compile first, so the game never runs the
previous version (this matters when **Enter Play Mode Options** has **Reload Domain** turned off).

### New custom nodes appear immediately

When you add or change a custom node in `uScriptProjectFiles/Nodes`, it shows up in the Toolbox after Unity
compiles. You don't need to close and reopen uScript.

---

## Adding nodes

### Quick Add

Press **Space** on the canvas, or right-click and choose **Search Nodes... (Space)**.

- Type any part of a node's name or category (for example `delay`, `set text`, `physics ray`).
- **Up/Down** chooses, **Enter** places the node under the mouse, **Esc** closes.
- Your last search is remembered.

### Drag a link onto empty canvas

This is off by default. Turn it on with **Quick Add on Link Drop** in the uScript Preferences (Node Settings).

Start a link from any socket and release it on an empty part of the canvas. Quick Add opens with **only the nodes
that can connect to that socket**. When the socket is a value, matching variables are listed first. The node you
choose is placed with its socket under the mouse and already connected. One **Ctrl+Z** removes both the node and the link.

---

## Links

- **Hover highlight:** sockets light up when the mouse is over them, and the area you can click is larger than before,
  so links are easier to start and finish.
- **Reroute points** shape a link around other nodes:
  - **Double-click a link** to add a point. Keep the mouse down to drag it straight away.
  - **Drag** a point to move it. It snaps to the grid when grid snapping is on.
  - **Double-click a point** to remove it.
  - To clear all points, select the links, then right-click > **Remove Reroute Points from Selected Links**.
  - The link becomes one smooth curve through its points. Points are saved in the graph, survive copy/paste and undo,
    and older graphs open unchanged. (Graphs saved with reroute points still open in older uScript versions;
    their links are just drawn straight.)

---

## Moving around the canvas

| Action | How |
|---|---|
| Frame the selected nodes (all nodes if none are selected) | **F**, or right-click / View menu > **Frame Selected** |
| Frame all nodes | **A**, or right-click / View menu > **Frame All** |
| Zoom toward the mouse | Mouse wheel, or **-** / **=** (**0** resets to 100%) |
| Pan | **Alt+drag** or middle-mouse drag |

### Minimap

The bottom-right corner shows the whole graph: events in orange, variables in purple, selected nodes in blue, and a
white frame for what you are looking at. **Click or drag** in it to move the view. Show or hide it with **M** or
**View > Show Panels > Minimap**.

---

## Variables tab

Open it with the **Variables** tab next to Toolbox and Contents, or with **Alt+V**. It lists every variable in the open graph.

Each row shows the name, type, value, an **Exposed** marker (public in the generated component) and how many nodes
the variable has. Variables with the same name and type are the same variable.

- **Click** a row to select the variable's nodes and jump to them. Click again to go to the next one.
- **Double-click** or right-click > **Rename** to rename every node of that variable at once. Renaming to the name of
  another variable of the same type merges them.
- In the details area below, edit the **value**, **Exposed** and **Hidden** for all of its nodes at once.
- **+** creates a new variable of any type.
- **Drag** a row onto the canvas to place another node of the same variable.
- A warning appears when two variables share a name but have different types.

---

## Finding errors

When a graph throws an error while the game runs, uScript adds a line under the error in the Console. It names the
**graph** and the **node** that caused it, with a **Show node in uScript** link. Clicking the link opens the graph
and selects and centers that node.

This works for graphs saved with this version. Use **File > Rebuild All Graphs** once on older projects. It works in
Unity 2021.2 and later, including Unity 6.

---

## Faster saves

**File > Compile Graphs Separately (Faster Saves)** is an option you turn on per project.

Normally, saving a graph makes Unity recompile **all** scripts in your project. With this option on, the generated
graph code compiles as its own assembly (`uScriptGraphs`), and your custom nodes as `uScriptNodes`. Saving a graph
then recompiles only the graph code. The bigger the project, the more this helps. Unity's reload after compiling
still happens.

**Before turning it on,** uScript checks whether your graphs or custom nodes use scripts from your own project code
(`Assembly-CSharp`). A separate assembly can't use those. If any are found, they are listed (the full list is in the
Console) and nothing changes. To go ahead, put those scripts in an Assembly Definition (asmdef) first.

**While it's on:**

- Your own scripts can still use graph components (for example `GetComponent<MyGraph_Component>()`).
- Assemblies a graph needs (for example TextMeshPro or the Input System) are added automatically when you save.
- If a graph starts using a script from `Assembly-CSharp`, uScript tells you and offers to turn the option off.
- Turn it off again from the same menu item.

---

## New nodes

### TextMeshPro (Actions > TextMeshPro)

**Set Text**, **Get Text**, **Set Color**, **Set Font Size**. These appear when TextMeshPro is available. It's built
into Unity UI 2.0 and later, so it's always there in Unity 6.

### Input System

These appear when the Input System package is installed.

- **Conditions > Input System > Input Action:** **Pressed**, **Released**, **Held** and **Not Held** outputs, plus
  the action's value.
- **Events > Input Events > Input System Events:** fires **On Action Pressed** and **On Action Released**.

Action names can be `Jump` or `Player/Jump`. They work with Unity 6 project-wide actions and with Player Input components.

### Web

**Web Text** and **Web Texture** use `UnityWebRequest`, can send form data, and report problems through their
**Error** output.

---

## Shortcuts

Only shortcuts that are new or changed are listed here. **Help > Quick Command Reference** in the uScript window shows them all.

| Shortcut | Action |
|---|---|
| **Space** | Quick Add |
| **F** | Frame selected nodes (all if none are selected) |
| **A** | Frame all nodes |
| **M** | Show / hide the minimap |
| **Alt+V** | Variables tab |
| **Double-click a link** | Add a reroute point |
| **Double-click a reroute point** | Remove it |
| **Middle-click a tab** | Close the tab |

---

## What was removed

- The update check, license, welcome and promotion windows, and everything else that depended on the old Detox
  Studios servers. **Help** now opens the bundled PDF and this project's GitHub page.
- The experimental XML save formats. Graphs are always saved in the normal `.uscript` format.
- The legacy networking nodes, because Unity removed that API.

---

uScript is open source under the MIT license. It was originally made by Detox Studios. This version is maintained at
[github.com/booman81/uscript](https://github.com/booman81/uscript).
