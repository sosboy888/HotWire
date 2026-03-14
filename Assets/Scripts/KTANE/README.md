# KTANE VR – Script Wiring Guide

This document explains how to connect every script to the bomb model and the
Ubiq networking infrastructure in the Unity Inspector.

---

## Quick start – Build the scene automatically

The fastest way to get started is to let the included editor tool build
**KTANEGame.unity** for you:

1. Open the Unity project (if not already open).
2. In Unity's top menu click **Tools ▶ KTANE ▶ Build KTANEGame Scene**.
3. The scene is saved to `Assets/Scenes/KTANEGame.unity`.
4. Double-click it in the Project window to open it.

The tool creates:
- Correct render settings and a Directional Light
- A floor, ceiling, and four walls
- A wooden table in the centre of the room
- The bomb (or a grey placeholder cube) sitting on the table
- The Ubiq Network Scene (Demo) prefab for networking
- The XR Origin (XR Rig) prefab at the Defuser's position
- An AutoRoomJoiner set to room name `ktane-vr-game`
- KTANEGameManager, all five module scripts, and the Expert UI canvas

After running the tool you still need to do the three manual steps below
(add colliders/interactables to the bomb children, fill Inspector slots,
and wire up the Expert canvas labels) – the tool leaves placeholder
comments wherever something must be filled in manually.

---

## Prerequisites

| Package | Version |
|---|---|
| Ubiq | 1.0.0-pre.16 |
| XR Interaction Toolkit | 3.0.7 |
| TextMeshPro | (included with Unity) |
| Universal Render Pipeline | any |

---

## Scene hierarchy (recommended)

```
NetworkScene                  ← Ubiq NetworkScene component lives here
  RoomClient                  ← Ubiq RoomClient component lives here
  KTANEGameManager            ← KTANEGameManager.cs
  Bomb (KTANE_BOMB.fbx)       ← bomb root
    Timer_Display
    Wire_0 … Wire_5
    Button_Main
    Button_LED
    Keypad_Key_0 … Keypad_Key_3
    Simon_Red
    Simon_Blue
    Simon_Green
    Simon_Yellow
  ExpertUI (World-Space Canvas)
    ← ExpertUIManager.cs
XR Rig (Defuser)
XR Rig (Expert)
```

---

## 1 · KTANEGameManager

**Attach to:** the `KTANEGameManager` GameObject (child of NetworkScene root).

| Inspector field | Value |
|---|---|
| Modules To Solve | `5` (adjust to match the number of modules in your scene) |
| On Game Started | Wire to your start-countdown audio / visual FX |
| On Bomb Defused | Wire to win screen / fireworks |
| On Bomb Exploded | Wire to explosion VFX |
| On Strike Added | Wire to strike indicator animation |
| On Role Assigned | Wire to `ExpertUI.gameObject.SetActive` (see §8) |

**How roles are assigned:**
The peer with the lexicographically smallest UUID becomes the **Defuser**.
The other peer becomes the **Expert**.  This is computed locally on both
clients from the RoomClient peer list, so no manual configuration is needed
when two players are in the same Ubiq room.

> **Testing with two Editor instances:** Use the Ubiq _Starter Assets_ demo
> scene's `AutoRoomJoiner` (already in `Assets/Scripts/AutoRoomJoiner.cs`) on
> both instances.  Each instance will auto-join the same room UUID.

---

## 2 · TimerModule

**Attach to:** the Bomb root (or a dedicated child).

| Inspector field | Value |
|---|---|
| Timer Display | Drag `Timer_Display` child here |
| Start Time | `300` (5 minutes) |
| Sync Interval | `0.5` (seconds between network ticks) |

The `_EmissionColor` of the `Timer_Display` material is updated every frame
to blend from **green** → **yellow** → **red** as time runs low.
A per-instance material is created at runtime so the shared asset is not
dirtied.

---

## 3 · WiresModule

**Attach to:** the Bomb root (or a child called `WiresModule`).

| Inspector field | Value |
|---|---|
| Wire Objects [0] | `Wire_0` |
| Wire Objects [1] | `Wire_1` |
| Wire Objects [2] | `Wire_2` |
| Wire Objects [3] | `Wire_3` |
| Wire Objects [4] | `Wire_4` |
| Wire Objects [5] | `Wire_5` |
| Cut Distance | `0.08` (metres; how far to pull before wire is cut) |

**Per-wire setup:**
1. On each `Wire_N` GameObject add a **CapsuleCollider** (or use the mesh
   collider from the FBX – make sure _Convex_ is OFF for trigger, or use
   a simpler proxy collider).
2. Add **XRGrabInteractable** to each wire.
3. Set `Movement Type` to `Kinematic` on the XRGrabInteractable.

The correct wire is determined at runtime by:
- Counting the last digit of the timer.
- Counting red and blue wires.
- Applying the deterministic rule set (see `WiresModule.DetermineCorrectWire()`).

---

## 4 · ButtonModule

**Attach to:** the Bomb root (or a child called `ButtonModule`).

| Inspector field | Value |
|---|---|
| Button Interactable | `Button_Main` (must have **XRSimpleInteractable**) |
| Button LED | `Button_LED` Renderer |

**Per-button setup:**
1. On `Button_Main` add a **BoxCollider** sized to the button face.
2. Add **XRSimpleInteractable** and enable _Select Mode: Single_.

Solve rules:
- **Blue** → hold; release when the timer contains a **4**.
- **Red** → hold; release when the timer contains a **1**.
- **Yellow / White** → quick tap (release within 0.5 s).

The `Button_LED` emission colour mirrors the hold-feedback colour.

---

## 5 · KeypadModule

**Attach to:** the Bomb root (or a child called `KeypadModule`).

| Inspector field | Value |
|---|---|
| Key Objects [0] | `Keypad_Key_0` |
| Key Objects [1] | `Keypad_Key_1` |
| Key Objects [2] | `Keypad_Key_2` |
| Key Objects [3] | `Keypad_Key_3` |

**Per-key setup:**
1. Add a **BoxCollider** to each key.
2. Add **XRSimpleInteractable** to each key.

On start a random symbol column is selected and each key is assigned one
symbol.  The Expert UI shows the symbols and correct press order.  The Defuser
must press keys in the correct order; wrong press → strike and reset.

---

## 6 · SimonModule

**Attach to:** the Bomb root (or a child called `SimonModule`).

| Inspector field | Array slot | Value |
|---|---|---|
| Pad Renderers | [0] | `Simon_Red` Renderer |
| Pad Renderers | [1] | `Simon_Blue` Renderer |
| Pad Renderers | [2] | `Simon_Green` Renderer |
| Pad Renderers | [3] | `Simon_Yellow` Renderer |
| Pad Interactables | [0] | `Simon_Red` XRSimpleInteractable |
| Pad Interactables | [1] | `Simon_Blue` XRSimpleInteractable |
| Pad Interactables | [2] | `Simon_Green` XRSimpleInteractable |
| Pad Interactables | [3] | `Simon_Yellow` XRSimpleInteractable |
| Total Rounds | | `5` |
| Flash On Time | | `0.4` |
| Flash Off Time | | `0.2` |

**Per-pad setup:**
1. Add a **MeshCollider** or **BoxCollider** to each Simon pad.
2. Add **XRSimpleInteractable** to each pad.

The colour mapping table (see `SimonModule.ColourMap`) handles the
strike-based remapping automatically.

---

## 7 · ExpertUIManager

**Attach to:** the root of a **World Space Canvas** that is visible only to the
Expert (e.g. floating in front of the Expert's head-tracked camera).

| Inspector field | Value |
|---|---|
| Label Role | TMP text component for "Role" label |
| Label Game State | TMP text component for game state |
| Label Strikes | TMP text component for strike display |
| Label Timer | TMP text component for timer readout |
| Label Wires | TMP text component for wire status |
| Label Button | TMP text component for button hints |
| Label Keypad | TMP text component for keypad symbols |
| Label Simon | TMP text component for Simon sequence hints |
| Timer Module | Drag the TimerModule component here |
| Wires Module | Drag the WiresModule component here |
| Button Module | Drag the ButtonModule component here |
| Keypad Module | Drag the KeypadModule component here |
| Simon Module | Drag the SimonModule component here |

The canvas starts **inactive** and is enabled automatically when the local
peer is assigned the Expert role.

To show/hide via KTANEGameManager events (alternative to built-in logic):

```csharp
// On any MonoBehaviour in the Expert's XR Rig:
void Start()
{
    KTANEGameManager.Instance.OnRoleAssigned.AddListener(role =>
        expertCanvasRoot.SetActive(role == PlayerRole.Expert));
}
```

---

## 8 · Networking rules summary

| Who | Does what |
|---|---|
| **Defuser** | Runs XR interaction, solve/strike logic, calls `GameManager.SolveModule()` / `AddStrike()` |
| **Expert** | Receives all network messages, reads module state via ExpertUIManager |
| **Both** | Have identical scene graphs; role is derived from lowest peer UUID |

Every module registers with `NetworkScene.Register(this)` to get a
`NetworkContext`.  Messages are sent with `context.SendJson(msg)` and received
by implementing `public void ProcessMessage(ReferenceCountedSceneGraphMessage)`.
This is the standard Ubiq messaging pattern as shown in the Ubiq Examples
(`Assets/Samples/Ubiq/1.0.0-pre.16/Examples/`).

---

## 9 · Quick-start checklist

- [ ] NetworkScene and RoomClient are in the scene
- [ ] Both players auto-join the same room (use `AutoRoomJoiner.cs`)
- [ ] KTANEGameManager attached and `Modules To Solve` set
- [ ] Bomb model imported and child names match exactly
- [ ] Each module script attached with all Inspector references filled
- [ ] Each interactive child has a Collider + XR Interactable component
- [ ] Expert World Space Canvas created with all TMP labels linked
- [ ] URP materials have `_EmissionColor` property (use URP/Lit shader)
- [ ] `Dynamic GI` / `Emission` is enabled on the render settings if you want
  emission to show without baked lightmaps
