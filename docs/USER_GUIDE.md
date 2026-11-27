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
- [Themes: Classic, Studio and Modern](#themes-classic-studio-and-modern)
- [AI assistant tools (MCP)](#ai-assistant-tools-mcp)
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
| Stable, version 2.0.0 (everything in this guide, recommended) | `https://github.com/booman81/uscript.git?path=/uScript_Dev/Assets/uScript#stable` |
| Exactly 2.0.0 (never updates) | `https://github.com/booman81/uscript.git?path=/uScript_Dev/Assets/uScript#v2.0.0` |
| Develop (newest work, may be unfinished) | `https://github.com/booman81/uscript.git?path=/uScript_Dev/Assets/uScript#develop` |
| Original 1.1.0 (only fixed for Unity 6, no new features) | `https://github.com/booman81/uscript.git?path=/uScript_Dev/Assets/uScript#v1.1.0` |

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
- **Drag a tab** left or right to change the order. A blue line shows where it will go. The order is remembered.
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

Pressing Play with unsaved graphs no longer stops you with a popup. A short note appears in the uScript window and the
Console lists which graphs are unsaved. To get the old popup back, turn on **Unsaved Graph Popup on Play** in the
uScript Preferences.

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

### Wireless links

Long cables across the graph can be shown as two small labels instead:

- Select links, or the nodes they belong to, then right-click > **Wireless Links > Make Wireless**.
  The cable disappears. The output end gets a label like `→ Item_New : In`, and the input end a label like
  `String Switch : B Matched →`. The label shows the node's comment if it has one, otherwise its title.
- **Click a label** to select that link and move the view to the other end.
- While the link or one of its nodes is selected, the cable is drawn faintly so you can see where it goes.
- **Wireless Links > Show as Cables** turns them back into normal cables.
- This only changes how the link is drawn; the graph works exactly the same. It is saved with the graph, works
  with undo and copy/paste, and older uScript versions show these links as normal cables.

---

## Moving around the canvas

| Action | How |
|---|---|
| Frame the selected nodes (all nodes if none are selected) | **F**, or right-click / View menu > **Frame Selected** |
| Frame all nodes | **A**, or right-click / View menu > **Frame All** |
| Zoom toward the mouse | Mouse wheel, or **-** / **=** (**0** resets to 100%) |
| Pan | **Alt+drag** or middle-mouse drag |
| Pan while dragging a link | Keep the left button down and drag with the **middle** button |

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

**File > Compile Graphs Separately (Faster Saves)**, also a checkbox in the uScript Preferences under Code Generation Settings, is an option you turn on per project.

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

## List Boxes

A List Box reads or writes one item of a list, with no node in between. It works like a property box.

**Add one:** right-click the canvas → **Add → Variables → List Box →** pick the list type (String, Int, Float, Bool, GameObject, Vector3 or Object).

**Connect it:** link your list variable into the **List** socket at the bottom of the box. Link the box into any node input to read the item, or link a node output into it to write the item back to the list.

**Pick the item from a socket:** link any variable into the **Position** (or **Key**) socket, next to **List** at the bottom of the box. Its value picks the item, and it wins over what is typed in the panel.

**Or pick the item** in the properties panel:

| Setting | What it does |
|---|---|
| Find By | **Position** or **Key** (Key is for text lists only). |
| Position / Key | Position: 1 = first item, -1 = last. Key: for lists like `Health,3,Damage,6`, the key `Health` gives `3`. |
| Use Variable | On: type the **name** of a graph variable (for example `Selected_Index`) and its value picks the item each time. |
| Add Key If Missing | Key mode: writing to a key that isn't in the list adds it to the end, e.g. writing 50 to `Mana` adds `Mana,50`. |
| Default Value | What the box gives when the key or position is missing (e.g. `100`). Empty = 0, empty text or False. Reading only. |
| Show Default Socket | Adds a **Default** socket after Position/Key. Link a variable into it to set the default from the graph. |
| Give Out As | Text lists only: Text, Whole Number, Decimal or True/False. |

Good to know:
- The box always shows the current item. Writing to it changes the list itself.
- A position outside the list, or a key that is missing, reads as empty or 0 and writes nothing. To add new keys, use a list node.
- Numbers are read and written the same way on every PC: `12.5` works, and so does `12,5`.
- Graphs without List Boxes generate the same code as before.

## Themes: Classic, Studio and Modern

uScript has three canvas looks. Switch with **View > Theme** or in the Preferences under **Canvas Look**. The change is
immediate, and graphs are not touched: every node keeps exactly the same size and position in every theme.

- **Classic**: the original uScript look.
- **Studio**: Classic refreshed. The same blue-grey canvas, light title bars, orange events, cream selection and socket
  shapes (square for In/Out, circle for value inputs, triangle for value outputs), with rounded corners, soft shadows,
  crisper sockets, flatter variables and a red title bar on deprecated nodes.
- **Modern**: a dark canvas, rounded nodes with a title bar, round sockets colored by type (blue for In/Out, orange
  for event outputs, purple GameObject, red bool, green string, cyan int, blue float), lighter links and round
  variables that stretch into a pill when selected. Event nodes have an orange title bar.

**Node icons** (Studio and Modern themes, can be turned off in the Preferences): events get a lightning bolt, nodes about a Unity
object get Unity's own icon for it (Animator, Camera, Rigidbody, Transform, GameObject...), and other nodes get a
small icon chosen from their name (compare, set, math, list, string, signal, timer, loop, input, debug). An icon is only
drawn when it fits next to the title, so it never makes a node wider.

**Grid dots**: turn on **View > Grid Dots** (or Grid Style in the Preferences) to draw the grid as dots instead of lines.
It works with both themes. The grid itself is still switched on and off with **View > Grid**.

---

## AI assistant tools (MCP)

If the free **MCP for Unity** package (`com.coplaydev.unity-mcp`, version 9 or newer) is installed in your project, uScript adds
tools that let an AI assistant (Claude, ChatGPT, Gemini or any other app that supports MCP) **read** your graphs. Nothing
needs setting up in uScript: the tools appear automatically when the package is present, and without it this part of uScript
is simply not compiled.

**The tools only read.** They never change graphs, generated code or anything else in your project.

| Tool | What it does |
|---|---|
| `uscript_list_graphs` | Lists the graphs in the project. |
| `uscript_read_graph` | Describes one graph: its nodes, typed-in values, variables, and what is connected to what. Big graphs come in pages and can be searched. |
| `uscript_find` | Searches all graphs for a text: a signal name, an animation state, a node type, a comment. |
| `uscript_signals` | Lists every signal name sent with Send Signal, which graphs send it, and which graphs mention that name (usually where it is received). Flags signals nobody mentions. |
| `uscript_check_graph` | Looks for likely mistakes: broken links, nodes that never run, events connected to nothing, unused variables, empty signal names, out-of-date generated code. |

**Node ids match the generated code.** Every node has an id, and it is the same number uScript uses in the graph's C# code.
A Console error with `Zombie_Hero:Relay_In_865` in its stack trace comes from node 865 of Zombie_Hero, so you can ask the
assistant "what is node 865 in Zombie_Hero?". This holds as long as the graph was saved after its last change.

To check the tools are there, open **Window > MCP for Unity**, then the **Tools** tab. The uScript tools are listed with
the others, and can be switched off there like any other tool.

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
