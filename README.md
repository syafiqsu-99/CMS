# Central Management System (CMS) — Technical Documentation

> Internal industrial monitoring platform for an injection-moulding factory. Tracks machine PLCs, production, rejects, utilities, staff attendance, and OEE reporting.

---

## 1. System Overview

CMS is a two-project solution deployed to IIS on a restricted internal network with a small user base. It reads live data from Omron PLCs on a continuous loop, persists it to SQL Server, and surfaces monitoring, reporting, and OEE dashboards through a Vue SPA.

| Concern       | Choice                                                                          |
| ------------- | ------------------------------------------------------------------------------- |
| Backend       | ASP.NET Core 8 Web API (`CMS.Server`), C# 12                                    |
| Frontend      | Vue 3 (Vite, Composition API), Vuetify, Pinia, Chart.js (`CMS.Client`)          |
| Data access   | Raw ADO.NET / `ExecuteSqlRawAsync` for live paths; EF Core for schema init only |
| Database      | SQL Server (**pre-2017** — no `STRING_AGG`, `TRIM`, `CONCAT_WS`)                |
| PLC protocol  | Omron FINS via `PLC_Omron_Standard`                                             |
| Hosting       | IIS, internal network, `SpaProxy` integration                                   |
| SPA dev proxy | Vite proxies `/api/*` to the backend                                            |

Design priorities are **stability and easy debugging over scalability** — no microservices, no distributed systems.

---

## 2. Solution Layout

```
CMS/
├── CMS.Server/                 # ASP.NET Core 8 Web API
│   ├── Controllers/            # Thin — placement follows where the fetch originates
│   ├── Services/               # BaseService + domain subclasses
│   ├── Data/                   # DbContext + SchemaInitializerService
│   ├── appsettings.json        # Connection strings LEFT EMPTY (see §7)
│   └── .config/dotnet-tools.json  # Pinned local dotnet-ef
└── CMS.Client/                 # Vue 3 SPA
    ├── src/components/          # ALL UI content lives here
    ├── src/views/              # Container skeletons ONLY
    ├── src/router/
    ├── src/store/              # Pinia
    └── src/utils/              # e.g. machineType.js
```

**UI separation of concerns (strict):** views are skeletons, components hold all cards/dialogs/charts/tables. Frontend-only mappings (e.g. machine-type by id) live in `/utils`, never in the backend.

---

## 3. Backend Architecture

### 3.1 Service layer

A `BaseService` base class with domain subclasses. Known subclasses: `MachinesService`, `OEEService`, `SupervisorService`, `DashboardService`, `SettingService`, plus `SchemaInitializerService` and `ExcelGenerationService`.

- `OEEService` (with `OEEController`) owns **all** OEE/report calculation paths. The former `MachineLogService`/`MachineLogController` are **deprecated and deleted**.
- `isDevelopment` is threaded as `bool isDevelopment` through the `BaseService` constructor into every subclass. A `TestMachineFilter()` helper applies the test-machine (`id_machine = 0`) filter at display query sites; write/polling helpers are left unfiltered. OEE keeps its own unconditional `<> 0` exclusion.

### 3.2 Dependency Injection lifetimes

| Service kind                                                                                                       | Lifetime      | Reason                                |
| ------------------------------------------------------------------------------------------------------------------ | ------------- | ------------------------------------- |
| PLC/background loop services (`PlcService`, `OEEService`, `DashboardService`, `MachinesService`, `SettingService`) | **Singleton** | Hold state across the continuous loop |
| `ExcelGenerationService`                                                                                           | **Scoped**    | Request-bound                         |
| DbContext                                                                                                          | Scoped        | Request-bound                         |

**Rule:** never inject a Scoped `DbContext` into a Singleton. Singletons that need DB access create a scope per unit of work via `IServiceScopeFactory` / `IDbContextFactory`.

Singleton background services are gated behind `!IsDevelopment()`. `ShiftReportBackgroundService` fires at **06:05** and **18:05**.

### 3.3 Controller placement convention

Endpoints live on the controller matching **where the fetch originates**, not where the data logically belongs. Examples:

- Material-group endpoints sit on `OeeController` (the OEE Wastage chart fetches from there).
- Machine-name setup sits on `SettingController` (a Setting-page concern).

### 3.4 Coding standards

- `async/await` for all DB and I/O. Parameterized queries only — pass `SqlParameter`/`DbParameter`, never interpolate.
- No `SELECT *`; project only needed columns.
- `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`; honour nullable annotations; use C# 12 features (primary constructors, collection expressions) where they improve readability.
- No new helper methods/private functions/utility classes unless explicitly needed — logic goes inline in existing methods.
- Minimal, targeted, inline changes over restructuring.
- Never manually dispose injected contexts/services.

---

## 4. PLC Network

_(Topology is rendered visually in the frontend; this section is the reference spec.)_

| Element               | Address / Rule                                              |
| --------------------- | ----------------------------------------------------------- |
| Central (main) PLC    | `172.17.86.80` — aggregates data from all machine PLCs      |
| Machine sub-PLCs      | `172.17.86.220` onward (`.220`, `.221`, …)                  |
| `id_machine` indexing | **0-based**                                                 |
| Test/trial machine    | `id_machine = 0` (development only; excluded from displays) |
| Protocol              | Omron **FINS** over UDP, via `PLC_Omron_Standard`           |

### 4.1 FINS read constraints

- Sub-PLC D-memory reads must use **≤250 words per chunk** to stay within FINS UDP payload limits.
- Pass **word count** (not bit/byte count) to `plc.Read()`.

### 4.2 Data flow

1. `PlcService` (Singleton) runs the continuous loop, reading the central PLC.
2. `insertMachineMaster` builds the per-machine `master` object and upserts `machine_master` (one row per machine, live snapshot).
3. State transitions are written to per-machine `machine_log_{id_machine}` tables.
4. Live inserts use **raw SQL** for performance/accuracy inside the loop.

### 4.3 Known PLC race condition (important)

The `stop_category` PLC register retains `"PRODUCTION RUNNING"` for ~9 seconds after a machine stops. **Fix:** normalize `master.stop_category` to an empty string immediately after `master` is built in `insertMachineMaster` when `status_start = 0`, **before** any downstream method consumes it.

---

## 5. Database Schema (SQL Server)

One row-per-machine live tables plus aggregated/historical tables. Machine-type grouping is **frontend-only** (see §8).

### 5.1 Core tables

| Table              | Purpose                                                                                                |
| ------------------ | ------------------------------------------------------------------------------------------------------ |
| `machine_master`   | 1 row per machine PLC — live data (current product, shot, cycle time, status)                          |
| `machine_log_{id}` | Dynamic per-machine state-change log (stop/run/etc.); one per machine                                  |
| `report`           | Aggregated reporting data derived from other tables                                                    |
| `reject`           | Daily reject data from PLC user input                                                                  |
| `sap`              | Product master (part weight, SAP cycle time, etc.)                                                     |
| `attendance`       | Daily staff attendance + machine assignments                                                           |
| `staff_list`       | All staff and their current machine assignments                                                        |
| `calendar`         | Working-day types (normal, overtime, offday, etc.)                                                     |
| `utilities`        | Daily per-machine utility data (chiller, dehumidifier, etc.)                                           |
| `material_group`   | SAP material → PP/PE/PET group mapping (source of truth; replaced a legacy JSON blob in `app_setting`) |
| `app_setting`      | Application settings                                                                                   |
| `db_log`           | Application/audit logging                                                                              |
| `plc_passwords`    | PLC credentials                                                                                        |

### 5.2 Dynamic `machine_log_{id}` tables

- One table per machine, named `machine_log_1` … `machine_log_N`.
- The range is derived dynamically from `MIN`/`MAX` `id_machine` in `machine_master`.

### 5.3 Computed column: `report.avail_hour`

`avail_hour` is a **computed column** and cannot be assigned directly in SQL:

```
avail_hour =
    production_running
  + change_full_set + change_half_set + change_parts
  + maintenance_dt
  + technician_dt
  + production_dt
  + buyoff_dt
```

### 5.4 Downtime bucket mapping

| Column(s)                                            | PLC stop categories                          |
| ---------------------------------------------------- | -------------------------------------------- |
| `change_full_set`, `change_half_set`, `change_parts` | MOULD CHANGE                                 |
| `maintenance_dt`                                     | MACHINE BREAKDOWN + OTHERS MAIN              |
| `technician_dt`                                      | QUALITY ISSUE + SAMPLE RUNNING + OTHERS TECH |
| `production_dt`                                      | NO OPERATOR + MATERIAL DRYING + OTHERS PROD  |
| `buyoff_dt`                                          | PRODUCT BUYOFF                               |
| `planned_dt`                                         | NO SCHEDULE + SCHEDULED MAINTENANCE          |

> **Open reconciliation gap:** uncategorised `machine_log` rows are silently excluded from `avail_hour` since the `unallocated` catch-all bucket was removed. Decide whether to reintroduce a catch-all or surface the discrepancy.

---

## 6. OEE Calculation

| Metric         | Formula                       |
| -------------- | ----------------------------- |
| Availability   | `Run Time / Operating Time`   |
| Operating Time | `Run Time + Unplanned DT`     |
| Available Time | `Operating Time + Planned DT` |

**Plant-level aggregation is ratio-of-sums, not the mean of per-machine values:**

```
Plant OEE Availability = ΣRun / ΣOperating
```

This applies to both the frontend `summaryMetrics` and the Excel TOTAL row.

**Live vs historical branching:** current-shift data is sourced live from `machine_master` + PLC; historical shifts read from the persisted `report` table. Mirrors the `isToday` / `isEditing` pattern in `OEEService` and `SupervisorService`.

---

## 7. Configuration & Secrets

- **Never** hardcode connection strings or secrets in `appsettings.json`.
- `ConnectionStrings:DefaultConnection` (and `CmsConnection`, if used) are left **empty** in the file.
- Real values come from **machine-level system environment variables** using the double-underscore convention:
  - `ConnectionStrings__DefaultConnection`
  - `ConnectionStrings__CmsConnection`
- Route all config changes through environment variables so updates skip file edits and the app-pool recycle that appsettings changes force.

---

## 8. Frontend Architecture

- **Composition API** (`setup`, `ref`, `reactive`); Pinia for state; Chart.js (`chart.js/auto`) for charts.
- **API calls use relative paths** (`/api/...`) relying on the Vite dev proxy.
- **Views are skeletons; components hold all UI.** Keep components small and reusable; avoid complex inline template logic.

### 8.1 Machine-type mapping (`/utils/machineType.js`, frontend-only)

| Type       | `id_machine` values |
| ---------- | ------------------- |
| ISBM       | 1–16                |
| EBM        | 20–23, 29           |
| IM         | 17–19, 24–25, 28    |
| 2 Stages   | 26–27               |
| Test/trial | 0                   |

### 8.2 Live polling

- **5-second interval**, current shift only.
- **Paused** during field edits via an `isEditing` flag.
- **Stopped** on `onUnmounted`.

### 8.3 Navigation

Supervisor and Setting nav items are expandable `v-list-group` components with child route links. Vue Router uses named child routes (`/supervisor/*`, `/setting/*`) with `meta.tab`; `Supervisor.vue` and `Setting.vue` bind the active tab to `route.meta.tab`.

### 8.4 Notable Setting tabs

| Component              | Function                                                                                                                                                  |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MaterialGroupTab.vue` | Edit `material_group` mappings                                                                                                                            |
| `MachineNameTab.vue`   | Edit `machine_name` per `id_machine` in `machine_master` **only** (historical tables intentionally not updated — machines relocate while PLCs stay fixed) |
| `ProdImportTab.vue`    | CSV import; accepts `machine_name` text, validates case-insensitively against `machine_master`, resolves to `id_machine` server-side before insert        |

---

## 9. Deployment (IIS)

- Publish via `dotnet publish`; workflow is IDE-agnostic and CLI-first.
- Target share: `\\68d-mmslpm\cms`.
- `tasks.json` task `publish-iis-sb`: write maintenance flag → 60-second live countdown banner → drop `app_offline.htm` → poll for DLL lock release → publish → cleanup.

### 9.1 `app_offline.htm` constraints

- Must **not** use a blind `<meta http-equiv="refresh">` — it causes redirect loops with `MapFallbackToFile("/index.html")`.
- Health-poll must validate `content-type: application/json` before redirecting.
- IIS URL Rewrite module is **not available** on the host.

---

## 10. Key Learnings & Gotchas

- **SQL Server pre-2017:** no `STRING_AGG`, `TRIM`, `CONCAT_WS` — use `FOR XML PATH` + `STUFF` throughout C# services.
- **`avail_hour` is computed** — never assign directly (see §5.3).
- **CSV date parsing:** use `DateOnly.TryParseExact` with formats `{ "d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" }`. A silent fallback to `DateOnly.MinValue` causes SQL Server out-of-range errors.
- **`SchemaInitializerService`:** add/alter only, **never** `DROP COLUMN`. Schema removals require manual SSMS intervention.
- **`db_log` logging is fire-and-forget** via an independent `SqlConnection` outside parent transactions, so rollbacks don't erase log entries.
- **PLC `stop_category` race** — normalize immediately (see §4.3).
- **FINS chunk size** — ≤250 words, pass word count (see §4.1).
- **Trigger caution:** `[dbo].[updateReport]` previously referenced a dropped `unallocated` column and tried to write the computed `avail_hour` — both fixed. Watch triggers when altering `report`.

### EF Core CLI tooling

`dotnet-ef` is a **local tool** pinned in `.config/dotnet-tools.json`. Run `dotnet tool restore` before first use, then `dotnet ef`. Keep the pinned `dotnet-ef` version aligned with the `Microsoft.EntityFrameworkCore.*` runtime packages — a mismatch causes migration command failures.

### VS Code tooling

MIT-licensed "C# for Visual Studio Code" extension + coreclr debugger only — **no** C# Dev Kit, **no** Visual Studio. Debugging via `.vscode/launch.json` (coreclr launch/attach); tasks via `.vscode/tasks.json` (build, publish, watch). CLI-first: `dotnet build`, `dotnet publish`, `dotnet watch`.

---

## Appendix — Additional information worth documenting

To make handover to another developer smooth, the following are worth extracting from the system and adding to this doc as you go:

1. **Full column dictionary per table** — every column with type, nullability, computed/identity flags, and PLC-register or SAP-field source. (Extractable from `INFORMATION_SCHEMA.COLUMNS` + `sys.computed_columns`.)
2. **Complete `id_machine` → machine registry** — id, `machine_name`, sub-PLC IP, machine type, ISBM/EBM/IM/2-Stages grouping, active/decommissioned. Reconcile against `machineType.js`.
3. **API endpoint catalogue** — route, HTTP verb, controller, request/response DTOs, and which frontend component/store consumes it. (Swagger/OpenAPI export if enabled.)
4. **PLC register / D-memory map** — every address read/written, its meaning, data type, word offset, and target column. This is the single most valuable artifact to recover if PLC firmware or wiring changes.
5. **`stop_category` enumeration** — the full canonical list of PLC stop categories and their exact string values (drives the §5.4 bucket mapping).
6. **All SQL triggers, computed columns, and constraints** — text of each trigger (`sys.sql_modules`), what fires it, and known pitfalls. `report` triggers especially.
7. **Background service schedule** — every timed/background job, its cron/time, what it writes, and its dev-mode gate. (`ShiftReportBackgroundService` = 06:05/18:05; list any others.)
8. **Shift definitions** — shift boundaries, `isToday`/current-shift logic, and the day-type rules in `calendar`.
9. **Environment variable inventory** — every `__`-convention variable the app reads, per environment, and who sets it.
10. **Deployment runbook** — step-by-step `publish-iis-sb` walkthrough, rollback (git-restore) procedure, app-pool identity, and the `app_offline.htm` health-poll contract.
11. **Excel/report output spec** — `ExcelGenerationService` sheet layout, the TOTAL-row ratio-of-sums rule, and column ordering.
12. **State machine for `machine_log`** — the valid status transitions (run → stop → …) and how each maps to a downtime bucket.
13. **Pinia store map** — each store, its state shape, and which components read/write it.
14. **Seed/migration history** — what `SchemaInitializerService` seeds on first run (e.g. `material_group` from legacy JSON) and any manual SSMS steps performed to date.
15. **Known open items** — the `unallocated` reconciliation gap (§5.4) and the pending PLC Signals tab frontend work (column grouping, frozen Machine column, offline/online status fix, manual column resize/reorder).
