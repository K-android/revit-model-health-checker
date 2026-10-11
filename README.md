# BIMHealthCheck

A Revit add-in that checks a model for common health problems, gives it a 0–100 score, and exports the results as an HTML report or CSV.

**Supported versions:** Revit 2024 (.NET Framework 4.8), Revit 2025 and 2026 (.NET 8). All three are built from the same code.

## Health checks

| Checker | Weight | What it looks at |
|---|---|---|
| Warnings & Classifications | 15% | All Revit warnings classified (Duplicate Elements, Rooms & Areas, Overlaps, Joins, Stairs, MEP, Analytical, Annotations) with failing Element IDs |
| Model Scale & Metrics | 10% | Element, view, sheet, family counts and file size evaluated against industry scale benchmarks |
| Unplaced & Unused Elements | 15% | Unplaced / unenclosed rooms (high priority), excessive unplaced working views, and unused templates |
| CAD Imports & Links | 15% | Distinguishes dangerous unlinked CAD imports from managed/pinned CAD links |
| In-Place Families | 10% | In-place families audit and instance counts across categories |
| Workset Organization | 10% | Worksharing audit: "Workset1" renaming, empty worksets, and distribution |
| Naming Standards & Cleanup | 15% | Pragmatic cleanup: flags copy artifacts (`Copy of`, `... - Copy`), abandoned `{3D - username}` views, and generic `Family1` names without penalizing standard Revit defaults |
| Revit Links & Coordination | 10% | Unpinned link instances (displacement risk), unloaded links, and link types |

## Scoring

Each checker scores 0–100; the overall score is the weighted average.

- 🟢 **80–100** Healthy
- 🟡 **60–79** Needs attention
- 🔴 **0–59** Critical

## Building

You need the **.NET SDK 8 or later**. Revit does **not** need to be installed to build: the Revit API comes from the `Nice3point.Revit.Api` NuGet packages.

Build all versions:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Build one version and install it for the current Windows user:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Versions 2026 -Deploy
```

Or with `dotnet` directly:

```powershell
dotnet build BIMHealthCheck\BIMHealthCheck.csproj -c Release -p:RevitVersion=2024
```

In **VS Code**, press `Ctrl+Shift+B` to pick one of the build tasks.

Output goes to `dist\<version>\`:

```
dist\2026\
├── BIMHealthCheck.addin
└── BIMHealthCheck\
    └── BIMHealthCheck.dll
```

## Installing

Copy the **contents** of `dist\<version>\` into:

```
%APPDATA%\Autodesk\Revit\Addins\<version>\
```

Example for Revit 2024: copy `dist\2024\*` to `%APPDATA%\Autodesk\Revit\Addins\2024\`. Then start Revit and click **Always Load** when asked.

To uninstall, delete `BIMHealthCheck.addin` and the `BIMHealthCheck` folder from that same folder.

## Using it

1. Open a model in Revit.
2. Go to the **BIM Health** tab → **Analysis** panel → **Health Check**.
3. Review the overall health score and expandable category cards.
4. **Locate problem elements in Revit:** Click the **🔍 Show** button or **double-click** any row in the findings table to immediately select and zoom to those elements in your active Revit view!
5. Use **Export HTML Report** or **Export CSV** to save and share the full audit.

## Project layout

```
BIMHealthCheck/
├── App.cs                    Ribbon tab and button
├── Commands/                 Command that runs the check
├── Engine/                   Result models, scoring, orchestrator
├── Checkers/                 One class per health check (implements IHealthChecker)
├── Reporting/                HTML and CSV export
├── UI/                       WPF results dialog
└── Utilities/                Revit API helpers
```

To add a new check, create a class in `Checkers/` that implements `IHealthChecker` and register it in `Engine/HealthCheckEngine.cs`.

## Version differences

Code that only works on newer .NET (for example `string.Contains(value, StringComparison)`) won't compile for Revit 2024. Use the `REVIT2024`, `REVIT2025`, `REVIT2026` or `REVIT2025_OR_GREATER` compile symbols when you need version-specific code:

```csharp
#if REVIT2025_OR_GREATER
    bool hasCopy = name.Contains("copy", StringComparison.OrdinalIgnoreCase);
#else
    bool hasCopy = name.IndexOf("copy", StringComparison.OrdinalIgnoreCase) >= 0;
#endif
```

## License

MIT
