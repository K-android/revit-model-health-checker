# BIMHealthCheck

A Revit add-in that checks a model for common health problems, gives it a 0–100 score, and exports the results as an HTML report or CSV.

**Supported versions:** Revit 2024 (.NET Framework 4.8), Revit 2025 and 2026 (.NET 8). All three are built from the same code.

## One-Click Project Scorecard & Features

- **Discipline Health Scores:** Instant health breakdown across **Architecture**, **Structure**, **MEP Systems**, and **BIM Coordination**.
- **Historical Trends:** Tracks progress over time (e.g. `📈 +6.5 pts improvement vs last week`), saving run history to `%APPDATA%\BIMHealthCheck\history\`.
- **Severity Levels & Filtering:** Findings are tagged by severity (**🔴 Critical**, **🟠 Error**, **🟡 Warning**, **🔵 Info**) with interactive filter pills in the UI.
- **Click-to-Zoom Navigation:** Click the **🔍 Show** button or double-click any row to highlight and zoom to that element in your active Revit view.

## Health checks (14 Specialized Audits)

| Checker | What it looks at |
|---|---|
| **Warnings & Classifications** | All Revit warnings classified (Duplicate Elements, Rooms & Areas, Overlaps, Joins, Stairs, MEP, Analytical, Annotations) with failing Element IDs |
| **Rooms & Tag Integrity** | Duplicate room numbers, unenclosed rooms (`Area == 0`), unplaced rooms, and orphaned/unhosted tags |
| **Duplicate Mark Values** | Identical Mark parameter values across doors, windows, mechanical/electrical equipment, and fixtures |
| **CAD Imports & DWG Layer Audit** | Detects unlinked CAD imports, model-wide imports, and CAD layer pollution in Object Styles |
| **Model Groups & Performance** | Single-instance group types (database bloat), unused group types, and high-count groups |
| **In-Place Families Audit** | Detects in-place families and counts their placed instances |
| **Generic Model Categorization** | Flags excessive Generic Models abuse bypassing proper BIM scheduling and IFC categories |
| **Family Health & Geometry** | Families with bloated type counts (>35 types), DirectShape elements, and heavy imported meshes |
| **Workset Organization** | Unrenamed default "Workset1", empty worksets, and element distribution across worksets |
| **Naming Standards & Cleanup** | Copy artifacts (`Copy of`, `... - Copy`), abandoned `{3D - username}` user views, and generic `Family1` names |
| **Revit Links & Coordination** | Unpinned link instances (risk of accidental shift), unloaded/missing links, and nested links |
| **Parameters & Compliance** | Scans project & shared parameter bindings for duplicate/conflicting parameter names |
| **Unreferenced Views & Templates** | Unreferenced views not placed on sheets and unused view templates |
| **Model Scale & Metrics** | Overall element counts, view counts, family counts, and file size against industry scale benchmarks |

## Scoring & Grades

Each checker scores 0–100; overall health is calculated via weighted average:

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
