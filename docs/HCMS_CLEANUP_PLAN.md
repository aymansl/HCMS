# HCMS — Codebase Review, Cleanup & Restructuring Plan

---

## 1. Project context

**What this is.** HCMS (Health Centers Management System) is a graduation project at the Syrian Private University (Software Engineering track).
- **Graduation 1** produced the current ASP.NET Core MVC app (`HCMS4/`). It has 4 roles: Admin, Doctor, Patient and Pharmacist. Its modules are appointments, medical records, prescriptions, pharmacy and inventory, invoices, complaints and surveys. It also has a no-show ML service in Flask.
- **Graduation 2** extends it with three new departments (Laboratory, Radiology, Emergency), shared services and an assistive AI layer, following an **incremental plan (MS Project Gantt)**.

**Target architecture.** A Modular Monolith using the ASP.NET Core MVC pattern:
- .NET 10, EF Core and SQL Server.
- ASP.NET Core Identity with **policy-based authorization**.
- SignalR for real-time notifications and QuestPDF for reports.
- Razor and Bootstrap with an English UI.
- A **separate AI service** (Python FastAPI, called over REST with an API key). The system must keep working when the AI service is down.

**Roles after Graduation 2 (7):** Patient, Doctor, Admin, Pharmacist, LabTechnician, Radiologist, EmergencySupervisor.

**Requirement codes.** These codes are used in branches, commits and tests:

| Code | Area |
|---|---|
| L1–L11 | Laboratory |
| R1–R6 | Radiology |
| E1–E6 | Emergency |
| S1 | Unified invoice |
| S2 | Roles and permissions |
| S3 | Real-time notifications |
| S4 | Audit log and turnaround-time dashboard |
| A1–A4 | AI features |

**Gantt increments.** The cleanup in this plan is the **foundation work of Increment 1 (Foundation and Roles)**. It must be finished before Increment 2 (Lab requests) starts.

| Increment | Scope | Requirements |
|---|---|---|
| 1 | Foundation and roles | S2 |
| 2 | Lab: catalog, requests and samples | L1–L6 |
| 3 | Lab: results and approval | L7–L11 |
| 4 | Radiology: requests and scheduling | R1, R2, R4 |
| 5 | Radiology: reports | R3, R5, R6 |
| 6 | Cross-department integration and emergency | E1–E6, S1, S3, S4 |
| 7 | Assistive AI | A1–A4 |
| Final | Full testing and delivery | — |

**Non-functional requirements.** Every phase must respect these:
- Role-based access, with account lockout after 5 failed logins.
- A list of 500 requests renders in under 2 s.
- The system keeps working without the AI service.
- Test coverage of 70% or more, with CI.
- Data modelled with HL7 FHIR in mind.
- The audit log is kept for at least 1 year.
- English UI, with each role's main task done in 3 steps or fewer.

---

## 2. Standing rules (apply in every phase)

1. **Work one phase at a time.** At the end of each phase, stop and write a phase report (see §11). Do not start the next phase without my explicit approval.
2. **Verify before you act.** `CODEBASE_ANALYSIS.md` was written on 2026-10-21 and may be stale. Before fixing any listed issue, confirm it still exists in the code. Report issues that are already fixed or not reproducible.
3. **Keep behaviour unless told otherwise.** Refactoring must not change user-visible behaviour. The only exceptions are the explicit fixes listed in Phases 3–4. If a refactor would change a URL, a view, a DB column or a business rule, stop and ask.
4. **Keep everything green.** After every logical step, `dotnet build` must give 0 errors and `dotnet test` must pass. Never commit a red build. Never delete or skip a failing test to make the build green. Fix the cause, or stop and report.
5. **Test the fixes.** Every bug or security fix gets a regression test that fails before the fix and passes after it.
6. **Git workflow:**
   - Branches:
     - `main` is stable and is only merged at the end of an increment.
     - `dev` is for integration.
     - Work happens on `feature/<code-or-topic>`, for example `feature/cleanup-security`.
   - Commits are small and focused, in English, conventional style (`fix:`, `refactor:`, `chore:`, `test:`, `build:`, `docs:`). Reference the issue number from `CODEBASE_ANALYSIS.md` §19 or the requirement code, for example `fix(auth): restrict AdminController to Admin role (#1)`.
   - Never force-push and never rewrite published history.
   - At the end of each phase, merge the feature branch into `dev` and tell me. Do not push unless I say so.
7. **Never lose data:**
   - Every EF migration must be reviewed for data loss before it is applied. Show me any migration that drops or renames a column or table, and wait for approval.
   - Never run `Database.EnsureDeleted`.
   - Never drop or reset my local database.
8. **Ask before deleting.** Do not delete a file or folder without listing it first with the reason. Group deletions in one commit.
9. **Secrets.** Never commit passwords, connection strings or API keys. Use User Secrets locally and environment variables elsewhere. Commit an `appsettings.Development.example.json` instead.
10. **UI language.** All user-facing text is in English.
11. **Do not touch these files** unless the phase says so:
    - `medical-appointment-no-shows-main/**/models/*.bin`
    - `data/*.csv`
    - Existing migrations under `Data/Migrations`, which must never be edited.
12. **Language.** Talk to me in Arabic. Code, identifiers, comments, commit messages and Markdown reports in the repository are in English.
13. **Platform.** I work on Windows with Visual Studio and SQL Server Express. Commands you give me must work in PowerShell.

---

## 3. Phase 0 — Baseline and verification (no code changes)

**Goal:** know exactly where we start.

1. Run `git status`.
   - If the folder is not a Git repository, stop and tell me. Do not initialise it yourself.
   - If it is one, create `dev` from the current branch, then `feature/cleanup-baseline`.
2. Run the baseline and record the results:
   - `dotnet --info` (SDK versions).
   - `dotnet build HCMS4.sln` (errors and warnings, with the count per warning code).
   - `dotnet test HCMS4.Tests/HCMS4.Tests.csproj --collect:"XPlat Code Coverage"` (pass/fail counts and line coverage %).
   - `dotnet list package --vulnerable --include-transitive` and `dotnet list package --outdated` for both projects.
3. Verify every item in `CODEBASE_ANALYSIS.md` §19 (issues 1–38). Mark each one **Confirmed**, **Already fixed** or **Not reproducible**, with file:line evidence.
4. Look for anything the analysis missed:
   - Dead code: unused classes, actions, views and ViewModels.
   - Duplicated logic.
   - Very large files (above 500 lines).
   - Controllers that use `ApplicationDbContext` directly instead of services.
   - Any `async void`, `.Result` or `.Wait()`.
   - Missing `[ValidateAntiForgeryToken]` on POST actions.
   - Missing `[Authorize]` on controllers.
5. Write `docs/cleanup/00-baseline.md` with:
   - The metrics above.
   - The verified issue table.
   - New findings, each with a severity (Critical, High, Medium, Low).
   - A list of the 10 largest files.
6. Commit only the report. **Stop.**

**Definition of Done:** the baseline report exists, nothing else changed, and the build and tests behave exactly as before.

---

## 4. Phase 1 — Repository hygiene

Work on branch `feature/cleanup-repo`.

1. **Ignore and remove build output.** Add a standard .NET + Python `.gitignore`: `bin/`, `obj/`, `.vs/`, `*.user`, `TestResults/`, `__pycache__/`, `.venv/` and `appsettings.*.local.json`. Remove any tracked `bin/` and `obj/` from the index (`git rm -r --cached`).
2. **Add an `.editorconfig`** with these settings:
   - UTF-8, CRLF and 4-space indentation for C#.
   - File-scoped namespaces preferred.
   - `var` when the type is apparent.
   - Naming rules: `_camelCase` for private fields and `Async` suffix on async methods.
3. **Add `Directory.Build.props`** at the root with shared settings: `Nullable` enable, `ImplicitUsings` enable, `LangVersion` latest, `AnalysisLevel` latest-recommended, and `EnforceCodeStyleInBuild` true. Do **not** turn on `TreatWarningsAsErrors` yet.
4. **Solution.** Add `HCMS4.Tests` to `HCMS4.sln` (issue #11).
5. **Target layout:**
   ```
   HCMS4_FINAL/
   ├── HCMS4.sln
   ├── src/HCMS4/              (web app, moved from ./HCMS4)
   ├── tests/HCMS4.Tests/      (moved from ./HCMS4.Tests)
   ├── ai-service/             (moved from medical-appointment-no-shows-main/medical-appointment-no-shows-main)
   ├── docs/                   (IMPLEMENTATION_STATUS.md, IMPLEMENTATION_SUMMARY.md, TESTING_GUIDE.md, cleanup reports, CODEBASE_ANALYSIS.md)
   ├── .github/workflows/      (added in Phase 6)
   ├── Directory.Build.props
   ├── .editorconfig
   ├── .gitignore
   └── README.md
   ```
   - Use `git mv` so history is kept.
   - Update the `.sln` paths, the `ProjectReference` paths, `launchSettings.json` and any relative path in code (for example the ML model path).
   - Build and test after the move.
6. **Move the EF tool manifest** to `.config/dotnet-tools.json` at the root, so `dotnet tool restore` works from anywhere.
7. **Secrets** (issues #3 and #2):
   - Move the connection string out of `appsettings.json` into User Secrets.
   - Commit `appsettings.Development.example.json` with placeholders.
   - In `README.md`, give the exact PowerShell commands to set `ConnectionStrings:DefaultConnection`, `AdminSettings:*` and `AiService:ApiKey`.
8. **Docs encoding.** Save the Arabic Markdown docs as UTF-8 with BOM (issue #32). Mark `TESTING_GUIDE.md` credentials as outdated, or fix them (issue #33).
9. **README.md** covers prerequisites, setup (secrets, DB, running the web app and the AI service), how to run the tests, the branch strategy and the project structure.

**Definition of Done:** a fresh clone, plus the README steps, builds, runs and passes the tests. No secrets remain in tracked files; verify with `git grep -i "password\|Integrated Security\|ApiKey"`.

---

## 5. Phase 2 — Upgrade to .NET 10

Work on branch `feature/cleanup-net10`. This fixes issues #12 and #13.

1. Set `net10.0` as the target framework for the web app and the tests.
2. Upgrade all `Microsoft.*` packages, EF Core (SqlServer, Design, Tools, InMemory) and Identity to the matching 10.x versions. Upgrade `dotnet-ef` in the tool manifest to 10.x.
3. Remove `Microsoft.VisualStudio.Web.CodeGeneration.Design` if it is not needed. Upgrade or remove packages with known vulnerabilities, until `dotnet list package --vulnerable --include-transitive` reports none.
4. Fix all breaking changes and obsolete-API warnings.
5. **Migrations:**
   - Run `dotnet ef migrations list` to confirm the existing migrations still load.
   - Run `dotnet ef migrations add Net10Upgrade` **only if** the model snapshot changes. Show me the generated migration, and delete it if it is empty.
   - Do not edit old migrations.
6. Run the full test suite, then start the app and smoke-test login for each role. Give me a manual checklist for this smoke test.

**Definition of Done:**
- 0 errors on `net10.0`.
- 0 vulnerable packages.
- Tests pass.
- The app starts and migrates the existing database without data loss.

---

## 6. Phase 3 — Critical and high security fixes

Work on branch `feature/cleanup-security`. Each item gets its own commit and a regression test.

| # | Fix |
|---|---|
| 1 | Change the `AdminController` class attribute to Admin only. Move `DailyVisitingPatientsReport` to `DoctorController`. Add a test proving a Doctor gets 403 on `/Admin/*`. |
| 2 | `AdminSeeder` must **fail fast**, with a clear log message and an exception, when `AdminSettings:*` are missing. No hard-coded fallback password. |
| 6 | Set `lockoutOnFailure: true` in every `PasswordSignInAsync` call. Lockout is 5 attempts and 5 minutes. Add a test. |
| 9 | Add `AccountController.AccessDenied` and its view, with Arabic text, and point `AccessDeniedPath` to it. |
| 10 | **Keep a single login UI.** Keep the MVC `/Account/Login` and remove or disable the scaffolded Identity Razor Pages that are not used. List them first. |
| — | Add a **global fallback policy** (`RequireAuthenticatedUser`). Explicitly mark public endpoints with `[AllowAnonymous]`: Home, Login, Register and static pages. |
| — | Add `[ValidateAntiForgeryToken]` to every state-changing POST action that lacks it (or use `AutoValidateAntiforgeryTokenAttribute` globally). |
| 29 | Store complaint attachments **outside `wwwroot`**, for example `App_Data/uploads`, and serve them through an authorized controller action. Validate extension, size and content type. |
| 4, 5 | **AI service:** disable `debug`, bind to `127.0.0.1`, and require an `X-API-Key` header that matches an environment variable. On the .NET side, read `AiService:ApiKey` and send it. The .NET client must treat timeouts and errors as "service unavailable" and continue without the AI feature (graceful degradation). |

**Definition of Done:**
- All items done, each with a test.
- Re-run the Phase 0 security checks and update the issue table.
- A short `docs/cleanup/03-security.md` report.

---

## 7. Phase 4 — Bug fixes and consistency

Work on branch `feature/cleanup-bugs`. For any item that needs a **DB schema change** (marked ⚠), show me the migration and wait for my approval.

| # | Fix |
|---|---|
| 7, 8 | Repair `Views/Patient/BookAppointment.cshtml`: remove the orphaned JS fragment, so that `node --check` on the extracted script passes. Either implement `GetAppointmentSuggestions` or remove the assistant UI. **Ask me which.** |
| 14 | Use **UTC everywhere for stored timestamps**. Inject `TimeProvider` (built into .NET 8+) instead of using `DateTime.Now` or `DateTime.UtcNow` directly, so time-based logic becomes testable. Convert to local time only in views. |
| 15 | Call `UpdatePastScheduledToCompletedAsync()` from the background service, not only from the admin page. |
| 16, 17 ⚠ | Implement `LeaveService.NotifyManagerAsync` using `NotificationService`. Persist the leave rejection reason (needs a new column). |
| 18 | Validate future dates in `AppointmentService.CreateAsync`, not only in the model attribute. |
| 19 | Remove the `ProcessPrescriptionAsync` alias and update its callers. |
| 20 | Delete `ConcurrencyEntity` and the dead catch blocks that depend on it. Optimistic concurrency will be added later where it is needed. |
| 21, 22 | Remove the duplicate jQuery and validation includes from `_LayoutAuth.cshtml` (keep one local copy of each). Delete the orphaned `dashboard.css`. |
| 23 | Use one set of drug expiry thresholds from `BusinessRules`, shared by `Drug` and `DrugService`. |
| 24 | Document the `DoctorLeave` versus `LeaveRequest` duplication and **propose** a merge, but do not implement it without my approval. |
| 25 | `GetFilteredReportAsync` must report the requested date range. |
| 26 | Leave as is. It is part of Increment 7 (A1). Add a `// TODO(A1)` comment only. |
| 27 | Make `NoShowRiskService` stateless: return the availability inside a result object instead of mutable instance fields. |
| 30 | Guard payments against the remaining balance (`TotalAmount - AmountPaid`). Add tests for overpayment and for a second payment. |
| 31 ⚠ | Add a `PaymentMethod` enum column to `Invoice`. |
| 34–38 | Fix the `PrescriptionItem.DrugId` nullability mismatch. Merge the duplicate `GetDoctorConsultationFee` endpoints. Make `AccountController` and `PharmacistController` derive from `BaseController`, or remove `BaseController` in favour of services (see Phase 5). |

**Definition of Done:**
- All confirmed items fixed or explicitly deferred, with the reason.
- Tests added.
- `docs/cleanup/04-bugs.md` written.

---

## 8. Phase 5 — Restructure into a Modular Monolith

Work on branch `feature/cleanup-modules`. This is the biggest phase. **Before writing any code, send me a short design proposal** (folder tree, namespaces, routing decision and migration order) and wait for approval.

**Target structure.** It stays in one web project. The aim is clear module boundaries, not separate assemblies:
```
src/HCMS4/
├── Modules/
│   ├── Appointments/   Controllers/ Services/ Models/ ViewModels/ Views/
│   ├── Records/        (patients, clinical notes)
│   ├── Pharmacy/       (drugs, prescriptions, dispensing, suppliers, purchase requests)
│   ├── Billing/        (invoices, payments → later S1 unified invoice)
│   ├── Feedback/       (complaints, surveys, ratings, articles)
│   ├── Staff/          (doctors, pharmacists, leaves)
│   ├── Lab/            (empty skeleton, Increment 2)
│   ├── Radiology/      (empty skeleton, Increment 4)
│   └── Emergency/      (empty skeleton, Increment 6)
├── Shared/
│   ├── Identity/       (ApplicationUser, roles, permissions, policies)
│   ├── Notifications/  (UserNotification, NotificationService → later SignalR hub, S3)
│   ├── Audit/          (SystemActivityLog, ActivityLogService → later S4)
│   ├── Reporting/      (DailyReport, PDF generation)
│   └── Common/         (ServiceResult, Pagination, BusinessRules, TimeProvider usage)
├── Data/               (ApplicationDbContext + per-module IEntityTypeConfiguration classes)
└── Areas/ or feature folders for views (choose one approach and justify it)
```

**Module rules:**
- A module may depend on `Shared`, but **not on another module's internals**. Cross-module calls go through an interface exposed by the module.
- Controllers are thin: no `ApplicationDbContext` in controllers. All data access goes through module services.
- Move the fluent configuration out of the 360-line `ApplicationDbContext` into `IEntityTypeConfiguration<T>` classes per module, applied with `ApplyConfigurationsFromAssembly`. The model snapshot must stay **identical**, meaning `migrations add` produces an empty migration. Prove it.
- Register module services through one extension method per module (`services.AddPharmacyModule()`, …), so `Program.cs` stays short.
- Split `AdminController` (103 KB, 63 actions) into feature controllers per module. **Keep existing URLs working**, using `[Route]` attributes or redirects, and give me a table of old URL → new URL.
- Namespaces follow the folders (`HCMS4.Modules.Pharmacy.Services`, …).

**Authorization redesign (S2, Increment 1):**
- Define **permission-based policies**, for example `Permissions.Lab.ReceiveRequest` and `Permissions.Radiology.ScheduleSession`, mapped to roles in one place.
- Use `[Authorize(Policy = ...)]` instead of role strings.
- Remove the unused policies `AdminOnly`, `DoctorOnly` and the others.
- Seed the **3 new roles**: `LabTechnician`, `Radiologist`, `EmergencySupervisor`.
- Tests prove that each role can reach only its own endpoints.

**Foundation for the next increments.** Add skeletons only, no features:
- `Shared/Workflow`: a small reusable **state-machine helper** for orders. It holds allowed transitions per status, rejects invalid transitions, and records `StatusChangedAt` and `ChangedBy` for every transition. It will be used by the lab orders, radiology orders and emergency cases (needed for turnaround-time / S4). Add unit tests.
- Empty `Lab`, `Radiology` and `Emergency` module folders, each with a README stating its requirement codes and increment.

**Definition of Done:**
- Same behaviour as before: all tests pass, plus a manual smoke checklist per role.
- The EF snapshot is unchanged.
- No controller uses `DbContext`.
- `docs/cleanup/05-modules.md` contains the final tree, a module dependency diagram (Mermaid) and the URL mapping table.

---

## 9. Phase 6 — Quality gates and CI

Work on branch `feature/cleanup-ci`.

1. **Tests:**
   - Add `Microsoft.AspNetCore.Mvc.Testing` integration tests (`WebApplicationFactory`) for authorization and the main flows.
   - Use SQLite in-memory, or EF InMemory where SQL behaviour does not matter.
   - Raise **line coverage to ≥ 70%** for the service layer. Report coverage per module.
2. **Analyzers.** Fix all analyzer warnings, then set `TreatWarningsAsErrors` to true in `Directory.Build.props`, keeping a short documented `NoWarn` list if needed.
3. **GitHub Actions** — `.github/workflows/ci.yml`:
   - Triggers: push and pull request on `dev` and `main`.
   - Steps: setup .NET 10, restore, build in Release, test with coverage, and fail if coverage is below 70%.
   - A separate job for the AI service: install with pip, run lint (ruff) and pytest smoke tests.
4. **Repository templates.** Add `.github/pull_request_template.md` with:
   - The requirement code.
   - A Definition of Done checklist: tests added, build green, docs updated and no secrets.
5. **Branch protection.** Write `docs/cleanup/06-ci.md` with instructions for me to enable it on GitHub: PR required into `main` and `dev`, and CI must pass.

**Definition of Done:** CI is green on `dev`, coverage is at least 70%, and the build has 0 warnings.

---

## 10. Phase 7 — Final cleanup report

1. Re-run all the Phase 0 measurements.
2. Write `docs/cleanup/CLEANUP_REPORT.md` with:
   - A before/after table: warnings, tests, coverage, vulnerable packages, largest files, number of controllers using `DbContext`.
   - The status of every issue from `CODEBASE_ANALYSIS.md` §19: fixed, deferred (with the reason) or not reproducible.
   - The architecture decisions taken, as short ADR-style entries: context, decision, consequence.
   - Known remaining risks.
3. Update `CODEBASE_ANALYSIS.md` so it reflects the new structure.
4. Merge into `dev`, then tell me it is ready to merge into `main` and to tag it as `v2.1` (end of Increment 1).

---

## 11. Phase report format (end of every phase)

Report in Arabic, in this order:
1. **What was done:** commits with short descriptions.
2. **Verification:** build result, test counts and coverage.
3. **Decisions you took and why.**
4. **Deviations from this plan,** and anything skipped.
5. **Questions or approvals I need to give** before the next phase.
6. **Manual checks I should do** in the browser, as a short checklist.

---

## 12. Out of scope for this cleanup

These belong to later increments, so do not implement them now:
- Lab, radiology and emergency features.
- The unified invoice.
- The SignalR notifications UI.
- QuestPDF reports.
- The FastAPI rewrite of the AI service.
- The new AI features.

Only the **foundations** listed in Phase 5 are allowed.
