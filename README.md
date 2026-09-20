# LoopGame

A dual-mouse first-person exosuit shooter built on WinForms + GDI+ with a
hand-rolled software 3D pipeline. No game engine, no external assets.

## Controls

The game starts by asking you to bind two physical mice through Raw Input.

| Action | Input |
| --- | --- |
| Bind left arm | Right-click the mouse you want as your LEFT arm |
| Bind right arm | Left-click the other mouse |
| Aim left weapon | Move left-arm mouse |
| Fire left weapon | Right-click, left-arm mouse |
| Aim right weapon | Move right-arm mouse |
| Fire right weapon | Left-click, right-arm mouse |
| Select move direction | Right-arm mouse wheel (forward / backward) |
| Move and look | Hold right-arm middle button, then move that mouse |
| Redeploy / restart | `R`, or click either mouse on the game over screen |

## Architecture

| File | Responsibility |
| --- | --- |
| `Program.cs` | Entry point |
| `RawMouseInput.cs` | WM_INPUT registration and per-device mouse packets |
| `GameForm.cs` | Game loop, camera, player state, projection, rendering |
| `GameObjects.cs` | Input records and the projectile tracer |
| `CameraView.cs` | Per-frame camera snapshot with near-plane clipping |
| `Geometry3D.cs` | World-space math shared by collision |
| `Enemy.cs` | Enemy exosuit state, pursuit and capsule hit detection |
| `EnemyRenderer.cs` | Procedural exosuit geometry and projected health bar |
| `ParticleField.cs` | World-space impact and destruction particles |

Everything in the battlefield lives at real XYZ world coordinates and is pushed
through one projection path, so distance alone determines on-screen size.

## Build and run

```
dotnet build
dotnet run
```

Requires the .NET 10 SDK on Windows.
