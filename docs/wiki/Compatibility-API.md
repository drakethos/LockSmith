# Compatibility API

LockSmith can work with optional ward / cheat mods **without requiring them**. SoftDependency is load-order only — if the foreign mod is missing, LockSmith still loads.

Scaffolding (`ICompatModule`, `CompatPriority`, `CompatHost`) lives in **DrakeModsLibs**. LockSmith owns a **per-plugin** host plus ward domain types (`IWardCompatModule`, `WardCoverageKind`).

Built-in modules live under `Compat/<ModName>/` (e.g. `Compat/WardIsLove/`, `Compat/ProtectiveWards/`, `Compat/ArcaneWard/`). Third parties can register their own module at runtime so LockSmith does not need a new release for every ward stack.

Mutually exclusive ward mods are **not** LockSmith’s responsibility. Every registered module that is **present** is activated and run.

## SoftDependency (your plugin)

```csharp
[BepInDependency(LockSmith.API.LockSmithCompatApi.LockSmithGuid,
    BepInDependency.DependencyFlags.SoftDependency)]
```

GUID: `com.drakesworkshop.locksmith`

## Register a module

1. Reference LockSmith + DrakeModsLibs public types (`IWardCompatModule`, `WardCoverageKind`, `DrakeModsLibs.Compat.CompatPriority`, `LockSmithCompatApi`).
2. Implement the interface in your mod.
3. Call `LockSmithCompatApi.Register` from Awake (or when LockSmith is ready).

```csharp
using BepInEx;
using DrakeModsLibs.Compat;
using LockSmith.API;
using LockSmith.Compat;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("author.mywardmod.compat", "MyWard ↔ LockSmith", "1.0.0")]
[BepInDependency(LockSmithCompatApi.LockSmithGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        if (LockSmithCompatApi.IsReady)
            TryRegister();
        else
            LockSmithCompatApi.OnInitialized += TryRegister;
    }

    private void TryRegister()
    {
        LockSmithCompatApi.OnInitialized -= TryRegister;
        LockSmithCompatApi.Register(new MyWardCompatModule());
    }
}

internal sealed class MyWardCompatModule : IWardCompatModule
{
    public string Id => "MyWardMod";
    public string? SoftDependencyGuid => "author.MyWardMod";
    public int Priority => LockSmithCompatApi.DefaultThirdPartyPriority;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        IsActive = /* soft detect your types / Chainloader */;
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        // Soft-patch your Interact / hover blockers so LockSmith public/guest bypass works.
    }

    public bool IsInsideEnabledWard(Vector3 position) { /* ... */ return false; }

    public WardCoverageKind QueryLocalAccess(Vector3 position, bool flash)
        => WardCoverageKind.Unrelated;

    public WardCoverageKind QueryPlayerAccess(long playerId, Vector3 position, bool flash)
        => QueryLocalAccess(position, flash);
}
```

## Priority

| Value | Constant | Role |
| --- | --- | --- |
| `0` | `CompatPriority.Vanilla` | Vanilla `PrivateArea` — Harmony patches first |
| `100` | `CompatPriority.BuiltIn` | LockSmith built-ins (WardIsLove, ProtectiveWards, ArcaneWard, DevCommands, …) |
| `200` | `CompatPriority.ThirdParty` | Suggested default for external modules |

- **Patches:** ascending priority (vanilla, then built-ins, then third-party).
- **Access queries:** highest-priority **covering** module wins (so a ward mod can override vanilla when both still apply).

## Built-in layout

```
Compat/
  CompatibilityManager.cs      # LockSmith facade over Libs CompatHost
  IWardCompatModule.cs
  WardCoverageKind.cs
  Vanilla/VanillaPrivateAreaModule.cs
  WardIsLove/WardIsLoveModule.cs
  ProtectiveWards/ProtectiveWardsModule.cs
  ArcaneWard/ArcaneWardModule.cs
  DevCommands/DevCommandsModule.cs
API/
  LockSmithCompatApi.cs
```

Public helpers for soft dependents (RenameIt, etc.): `LockSmithCompatApi.HasLocalWardAccess` / `IsInsideEnabledWard`.

Adding another built-in ward mod: new folder under `Compat/`, implement `IWardCompatModule`, `Host.Register` in `CompatibilityManager.Initialize`.

## Upstream vs external

External registration is supported so you are not blocked on a LockSmith release.

Built-in SoftDependency bridges (WardIsLove, ProtectiveWards, Arcane Ward, …) are a **courtesy** for common packs. They are not actively regression-tested against every upstream ward update. If a bridge breaks, please [open an issue](https://github.com/drakethos/LockSmith/issues) with logs — I may look into it when I can. Contributions are welcome: open a pull request under `Compat/<YourMod>/`, or keep the bridge in your own mod via SoftDependency + `LockSmithCompatApi.Register`.

If you maintain a popular ward mod and want first-party support, a PR or issue is the right path. Built-in modules are easier for players (no extra bridge plugin) and stay in the soft-compat scan automatically.
