# WorkSchedulerApp

A cross-platform desktop application for building weekly work schedules and **automatically assigning employees to tasks** based on their skills, contracted/requested hours, and weekly workload balance.

Built with **.NET 9** and **Avalonia UI**, following the **MVVM** pattern with a normalized **SQLite** backend.

> University project. The focus is on data modelling, a layered desktop architecture, and a non-trivial scheduling algorithm — all covered by automated tests.

<!-- Add a screenshot of the running app here — it's the single most effective thing for a portfolio README:
![WorkSchedulerApp screenshot](docs/screenshot.png)
-->

---

## What it does

Managers spend a lot of time turning a repeating weekly plan ("on Mondays we need the deep-clean done, deliveries unpacked, and two people on the floor") into a concrete rota of *who* does *what*. This app models that workflow end to end:

1. **Define work** as reusable **workload templates** of different kinds:
   - **Fixed** – a set number of hours.
   - **Per-item** – `minutes per item × number of items`.
   - **Per-employee** – `minutes per employee × number of employees`.
   - **Group** – a template that bundles other templates; its estimated hours are computed **recursively**.
2. **Define employees** with a role, availability, contracted vs. requested hours, and the set of templates they are **skilled** to perform.
3. **Build a weekly template** — seven days, each populated with the workloads required that day.
4. **Instantiate a week** (with real start/end dates), which clones the template into concrete day/workload **instances**.
5. **Auto-assign** employees to that week's workloads, then **export the result to CSV**.

### The scheduling algorithm

[`ScheduleAutoAssigner`](Scheduling/ScheduleAutoAssigner.cs) decides how many people a task needs (from its estimated hours) and ranks qualified employees by a priority score:

| Priority | Condition | Score |
|----------|-----------|-------|
| 1 (highest) | Below **contracted** hours — *must* get hours | `1000 + hours remaining to contracted` |
| 2 | Between contracted and **requested** hours | `500 + hours remaining to requested` |
| 3 (lowest) | Already above requested hours | `10 − overload` |

Hours already assigned earlier in the same week are taken into account, so work is spread fairly across the team.

---

## Tech stack

| Area | Choice |
|------|--------|
| Runtime | .NET 9 |
| UI | [Avalonia 11.3](https://avaloniaui.net/) (Fluent theme, compiled bindings) |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) source generators |
| Data | SQLite via `Microsoft.Data.Sqlite` (raw SQL, foreign keys + cascade deletes) |
| DI | `Microsoft.Extensions.DependencyInjection` |
| Tests | NUnit |

---

## Architecture

```
Program.cs ─► App.axaml.cs ─► DI container ─► MainViewModel ─► PageFactory ─► page ViewModels
                                                                                      │
                              Views (.axaml)  ◄── data binding (MVVM) ────────────────┘
                                                                                      │
                              ScheduleAutoAssigner  ──┐                                │
                                                      ▼                                ▼
                                              DatabaseHandler (SQLite)  ◄──────────────┘
```

- **Views** (`Views/*.axaml`) are declarative XAML, bound to **ViewModels** (`ViewModels/`) using compiled bindings.
- **Navigation** is handled by a `PageFactory` resolving page ViewModels from the DI container, driven by `MainViewModel`.
- **`DatabaseHandler`** is the data-access layer: schema creation and all CRUD/query logic for the SQLite database.
- **`ScheduleAutoAssigner`** holds the pure scheduling logic, kept separate from the UI so it can be unit-tested directly.

### Database schema

The schema (created on first run in [`DatabaseHandler.EnsureSchemaAsync`](Database/DatabaseHandler.cs)) is fully normalized with foreign keys and `ON DELETE CASCADE` throughout:

- Template inheritance via subtype tables (`Fixed` / `PerItem` / `PerEmployee`).
- Many-to-many links via junction tables (`EmployeeSkill`, `WorkGroupWorkLoadTemplate`, `DayWorkloadTemplateWorkLoadTemplate`).
- A clear template → instance split (`WeeklyWorkloadTemplate` → `WeeklyWorkloadInstance` → `DayWorkloadInstance` → `WorkLoadInstance`).

UML diagrams (schema, sequence, backend) live in [`UMLS/`](UMLS/) as PlantUML (`.puml`) files.

---

## Getting started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### Run the app
```bash
git clone <repo-url>
cd WorkSchedulerApp
dotnet run --project WorkSchedulerApp.csproj
```
The SQLite database is created automatically on first launch under the build output's `Database/` folder.

### Run the tests
```bash
dotnet test
```
The suite (`TestProject1/`) has 26 NUnit tests covering employee CRUD, foreign-key cascade integrity, template linking, and the auto-assignment algorithm.

---

## Project structure

```
WorkSchedulerApp/
├── App.axaml(.cs)         App entry, DI container wiring
├── Program.cs             Avalonia bootstrap
├── Models/                Domain models (Employee)
├── Database/              DatabaseHandler — schema + data access
├── Scheduling/            ScheduleAutoAssigner — assignment algorithm
├── ViewModels/            One ViewModel per page (MVVM)
├── Views/                 Avalonia XAML views
├── Factories/             PageFactory for navigation
├── Data/                  Shared types (page enum)
├── Styles/ · Assets/      Theme, fonts, icons
├── UMLS/                  PlantUML diagrams
└── TestProject1/          NUnit test suite
```

---

## Possible future work

Known areas I would refactor with more time:

- **Split `DatabaseHandler`** (currently a single large class) into per-entity repositories behind an interface, and inject it rather than using a singleton — this would also make the ViewModels unit-testable.
- **Introduce domain model classes** to replace the tuples currently returned by the data layer, so the database shape doesn't leak into the UI.
- **Strongly type `WorkLoadType`** as an enum and store `ContractedHours` numerically instead of as text.
- Replace ad-hoc diagnostics with a proper logging abstraction.
