# Clients

Wave 0 scaffolding for the client runtime seam. Nothing here is wired up yet; no existing
code references these types.

## Layers

| Folder | Holds | Must not hold |
|---|---|---|
| `Contracts/` | `IRobloxClient`, `IClientLayout`, `IClientSource`, `IClientLauncher`, `ILogLocator`, `IProcessService` | Client-specific paths or OS checks |
| `Model/` | Enums, `ClientInstall`, `ClientResolveContext`, requests/results, capabilities | Settings reads or mutable state |
| `Layouts/` | Static packaging, path, log, and process metadata | Install, launch, or dialogs |
| `Registry/` | Descriptors, resolution, `ClientRegistry` | Global active-client state |

## Rules

- A layout is pure. `Resolve(context)` takes host values as an argument and returns absolute
  paths; it never reads `App.Settings` and never calls `OperatingSystem.Is*`.
- A layout describes the client, not the host. `MacBundlePlayerLayout` must be resolvable on
  Linux by passing Linux host roots.
- Behavior is chosen by `ClientCapabilities`, never by `ClientId`.
- Log discovery and process identity belong to `ILogLocator` and `IProcessService`. The
  watcher keeps the parsing.

## Not in this wave

Runtimes, sources, launchers, providers, state store, and any consumer migration. Those arrive
with Steps 2–7 of the plan.
