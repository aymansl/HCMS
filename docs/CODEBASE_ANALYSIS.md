# HCMS4 — Hospital Care Management System: Comprehensive Codebase Analysis

> **Purpose of this document.** It is a complete, AI-readable map of the HCMS4 repository: architecture, startup pipeline, data model, business rules, services and controllers, the Razor UI layer, the no-show ML microservice contract, the test suite, and a catalogue of known defects and risks. It is meant to let another engineer or AI agent (or a fresh chat session) understand the implementation without re-reading the source.
>
> **Updated on 2026-11-19 at the end of Increment 1 (`v2.1`).** `dotnet build HCMS4.sln` → 0 errors, 0 warnings (warnings are errors). `dotnet test HCMS4.sln` → **371 passed, 0 failed**. Service line coverage 82.8 %.
>
> The first version of this analysis was written on 2026-10-21, before Increment 1. §19 keeps that original defect list and adds the current status of each item.

---

## Table of Contents

0. [Status After Increment 1 (`v2.1`)](#0-status-after-increment-1-v21)
1. [Repository Layout](#1-repository-layout)
2. [Technology Stack](#2-technology-stack)
3. [Build, Run, and Test](#3-build-run-and-test)
4. [Configuration](#4-configuration)
5. [Startup Pipeline (`Program.cs`)](#5-startup-pipeline-programcs)
6. [Dependency Injection Registry](#6-dependency-injection-registry)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [Data Model](#8-data-model)
9. [Database Schema, Migrations &amp; Seed Data](#9-database-schema-migrations--seed-data)
10. [Business Rules Catalogue](#10-business-rules-catalogue)
11. [Service Layer](#11-service-layer)
12. [Controller Layer](#12-controller-layer)
13. [ViewModels](#13-viewmodels)
14. [Razor Views &amp; UI](#14-razor-views--ui)
15. [Front-end Assets](#15-front-end-assets)
16. [ML No-Show Prediction Microservice](#16-ml-no-show-prediction-microservice)
17. [Test Suite](#17-test-suite)
18. [Key End-to-End Workflows](#18-key-end-to-end-workflows)
19. [Known Issues, Defects &amp; Risks](#19-known-issues-defects--risks)
20. [Recommended Next Steps](#20-recommended-next-steps)
21. [Appendix A — File Map](#appendix-a--file-map)

---

## 0. Status After Increment 1 (`v2.1`)

Increment 1 kept every feature and URL, and changed the structure underneath them.

| Area | Before (2026-10-21) | After (2026-11-19) |
|---|---|---|
| Framework | `net6.0` (out of support) | `net10.0`, EF Core 10.0.15 |
| Layout | `HCMS4/` with 7 role controllers | `src/HCMS4/` modular monolith: `Modules/*` + `Shared/*`; `tests/HCMS4.Tests` |
| Controllers | `AdminController` 2 527 lines; all controllers use `ApplicationDbContext` | 23 module controllers; **none** uses the DbContext; data access in 39 services |
| Authorization | Role strings; Doctor could reach `/Admin/*` | 25 permission policies (`Permissions.*`) mapped to 7 roles in `RolePermissions`; fallback policy requires sign-in |
| Roles | Admin, Doctor, Patient, Pharmacist | + LabTechnician, Radiologist, EmergencySupervisor (no pages yet) |
| Time | UTC / local mixed | Damascus time (UTC+3) stored and shown, through `IClinicClock` / `ClinicTime` |
| Login UIs | MVC + scaffolded Identity pages | MVC only; lockout armed (5 attempts / 5 min) |
| Pharmacy | Stock taken at prescribing and at dispensing; partial dispensing | Stock taken **only when dispensing**; the whole prescription at once; one invoice |
| Billing | No admin invoice list | `/Admin/ManageInvoices` (filter, totals, pages); cash payments only |
| UI language | English with some Arabic RTL pages | English (LTR) everywhere |
| Secrets | Connection string committed | User Secrets / environment variables only |
| Tests | 44 (none run through the solution) | 371 unit and integration tests, run through `HCMS4.sln` |
| CI | none | GitHub Actions on push to `main` (`.github/workflows/ci.yml`) |

**URLs did not change.** Module controllers keep the old controller names through `[ControllerName("Admin")]` and similar attributes; the full list is in `tests/HCMS4.Tests/Integration/RouteInventory.txt`. The only route changes since Increment 1 started are the following.
- Removed:
  - the dead pharmacist profile pages;
  - the two "mark prescription completed" shortcuts.
- Added: `GET /Admin/ManageInvoices`.

---

## 1. Repository Layout

```
HCMS4_FINAL/
├── HCMS4.sln                       # Web app + test project
├── Directory.Build.props           # net-wide settings: nullable, analyzers, TreatWarningsAsErrors
├── .editorconfig                   # Code style + documented analyzer exceptions
├── .config/dotnet-tools.json       # Local tool: dotnet-ef 10.0.15
├── .github/workflows/ci.yml        # Build + test on push to main
├── src/HCMS4/                      # ASP.NET Core MVC web application (net10.0)
│   ├── Program.cs                  # Startup: shared services, one Add<Module>Module() per module
│   ├── appsettings.json            # No secrets (connection string, admin, API key come from User Secrets)
│   ├── appsettings.Development.example.json
│   ├── Data/
│   │   ├── ApplicationDbContext.cs # DbSets + ApplyConfigurationsFromAssembly
│   │   └── Migrations/             # 29 migrations + model snapshot (never edited)
│   ├── Modules/
│   │   ├── Appointments/  Controllers Data Models Services Views
│   │   ├── Billing/       Controllers Data Models Services Views
│   │   ├── Feedback/      Controllers Data Models Services Views   (complaints, surveys, ratings, articles)
│   │   ├── Pharmacy/      Controllers Data Models Services Views   (drugs, prescriptions, dispensing, suppliers, purchasing)
│   │   ├── Portal/        Controllers Services Views               (role dashboards, public home page)
│   │   ├── Records/       Controllers Data Models Services Views   (patients, clinical notes, medical record)
│   │   ├── Staff/         Controllers Data Models Services Views   (doctors, pharmacists, specializations, leave)
│   │   ├── Lab/           README.md   (Increments 2–3)
│   │   ├── Radiology/     README.md   (Increments 4–5)
│   │   └── Emergency/     README.md   (Increment 6)
│   ├── Shared/
│   │   ├── Audit/          SystemActivityLog, IActivityLogService
│   │   ├── Common/         ServiceResult, Pagination, BusinessRules, ClinicTime/IClinicClock, HcmsControllerBase,
│   │   │                   ControllerNameAttribute, ModuleViewLocationExpander
│   │   ├── Identity/       ApplicationUser, AccountController, AdminSeeder, ICurrentUserService, lookups,
│   │   │                   Authorization/ (Roles, Permissions, RolePermissions, AddHcmsAuthorization)
│   │   ├── Notifications/  UserNotification, INotificationService
│   │   ├── Reporting/      DailyReport, IDailyReportService, DailyReportBackgroundService
│   │   └── Workflow/       StateMachine<TStatus> for orders and cases
│   ├── ViewModels/         Shared view models (HCMS4.ViewModels)
│   ├── Views/              Account, Home, Shared (layouts, partials)
│   └── wwwroot/            CSS / JS / vendor libraries / images
├── tests/HCMS4.Tests/      xUnit unit + integration tests (net10.0)
├── ai-service/             Flask no-show ML service (predict.py, train.py, models/*.bin, data/*.csv)
└── docs/                   REQUIREMENTS.md, this file, diagrams/*.puml
```

Each module registers its services in `<Module>Module.Add<Module>Module()` and keeps one `IEntityTypeConfiguration<T>` per entity in its `Data/` folder. Views are found in `Modules/<Module>/Views/<Controller>/` first (`ModuleViewLocationExpander`), then in `Views/`.

---

## 2. Technology Stack

### Web application (`src/HCMS4/HCMS4.csproj`)

| Item | Value |
|---|---|
| SDK | `Microsoft.NET.Sdk.Web` |
| Target framework | `net10.0` (LTS) |
| Analysis | `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors` (Directory.Build.props) |
| Nullable / implicit usings | enabled |
| UserSecretsId | `aspnet-HCMS4-A260DD2B-EF83-4531-8B2B-7ADF582829DA` |
| ORM | EF Core 10.0.15 + SQL Server provider |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.15 (MVC login only, no Identity UI) |
| Time | `TimeProvider` through `IClinicClock` / `ClinicTime` (Asia/Damascus) |
| Front-end | Razor + Bootstrap 5, Bootstrap Icons, jQuery (one local copy), English LTR UI |

### Test project (`tests/HCMS4.Tests/HCMS4.Tests.csproj`)

| Item | Value |
|---|---|
| Target framework | `net10.0` |
| Test framework | xUnit 2.9.3 + `xunit.runner.visualstudio` 3.1.4 |
| Integration tests | `Microsoft.AspNetCore.Mvc.Testing` 10.0.15 (`HcmsWebApplicationFactory`, header-based test login) |
| DB provider | `Microsoft.EntityFrameworkCore.InMemory` 10.0.15 |
| Time | `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) |
| Mocking / coverage | Moq 4.18.4, coverlet.collector 6.0.4 |

### ML microservice (`ai-service/`)

| Item | Value |
|---|---|
| Language / framework | Python, Flask (`scripts/predict.py`) |
| Endpoint | `POST /predict` on `127.0.0.1:9696`, requires `X-API-Key` (`HCMS_AI_API_KEY`), `debug=False` |
| Model | LightGBM classifier (`LGBMClassifier_tranformers_final.bin`) + `StandardScaler` + `OneHotEncoder` |
| Data | Kaggle "Medical Appointment No Shows" dataset |

---

## 3. Build, Run, and Test

```powershell
# Build and test (the solution contains the web app and the tests)
dotnet build HCMS4.sln
dotnet test HCMS4.sln

# Run the web app (needs the User Secrets below)
dotnet run --project src\HCMS4\HCMS4.csproj

# EF tooling (local tool manifest in .config/)
dotnet tool restore
dotnet tool run dotnet-ef migrations add Inc<N>_<Code>_<Name> --project src\HCMS4\HCMS4.csproj --output-dir Data\Migrations

# AI service
$env:HCMS_AI_API_KEY = "<key>"; python ai-service\scripts\predict.py
```

**First-run behaviour:** on startup the app applies pending migrations (`Database.Migrate()`, relational providers only) and runs `AdminSeeder`. The seeder creates the 7 roles and the admin account, and **refuses to start** when `AdminSettings:Email` / `AdminSettings:Password` are missing.

**CI:** `.github/workflows/ci.yml` runs restore → `build -c Release` → `test` with XPlat coverage on `windows-latest` for every push to `main`. It needs no secrets because the tests use EF InMemory.

---

## 4. Configuration

`src/HCMS4/appsettings.json` contains **no secrets**:

```jsonc
{
  "ConnectionStrings": { "DefaultConnection": "" },
  "AiService": { "BaseUrl": "http://localhost:9696", "ApiKey": "" },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

Development values come from **User Secrets**. Other environments use environment variables. `appsettings.Development.example.json` shows the expected keys:

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `AdminSettings:Email`, `AdminSettings:Password`, `AdminSettings:UserName` | Seeded admin account (required) |
| `AiService:ApiKey` | Must equal the AI service's `HCMS_AI_API_KEY`. Without it the app uses the local no-show estimate |

---

## 5. Startup Pipeline (`Program.cs`)

1. `WebApplication.CreateBuilder(args)`; `AddDbContext<ApplicationDbContext>` with SQL Server.
2. `AddControllersWithViews` with the global `AutoValidateAntiforgeryTokenAttribute`. The `ModuleViewLocationExpander` and the `[ControllerName]` convention are also registered.
3. Session (30 min, `HttpOnly`, `SameSite=Strict`).
4. `AddHcmsIdentity()`:
   - Identity password rules.
   - Lockout: 5 attempts / 5 min, armed on login.
   - Unique email.
   - Cookie: `/Account/Login`, `/Account/AccessDenied`, 60 min sliding.
   - `ICurrentUserService`.
5. `AddHcmsAuthorization()`: fallback policy `RequireAuthenticatedUser` plus one policy per permission (§7).
6. Shared services: `AddSharedCommon()` (clock), `AddNotifications()`, `AddAudit()`, `AddReporting()` (daily report plus the nightly `DailyReportBackgroundService`).
7. Modules: `AddAppointmentsModule()`, `AddRecordsModule()`, `AddPharmacyModule()`, `AddBillingModule()`, `AddFeedbackModule()`, `AddStaffModule()`, `AddPortalModule()`.
8. Culture fixed to `en-US`.
9. Middleware: developer exception page (Development) or exception handler + HSTS → HTTPS redirection → static files → routing → session → authentication → authorization.
10. Default route `{controller=Home}/{action=Index}/{id?}`.
11. Startup scope: `Migrate()` (or `EnsureCreated()` for InMemory), then `AdminSeeder.SeedAdminUser`.

---

## 6. Dependency Injection Registry

All services are scoped unless noted. Each line is registered by the extension method in its module or shared folder.

| Module / shared | Services |
|---|---|
| Shared/Common | `IClinicClock` (singleton) |
| Shared/Identity | `ICurrentUserService` |
| Shared/Notifications | `INotificationService` |
| Shared/Audit | `IActivityLogService` |
| Shared/Reporting | `IDailyReportService`; `DailyReportBackgroundService` (hosted) |
| Appointments | `IAppointmentService`, `IDailyVisitReportService`, `INoShowRiskService` (+ named `HttpClient`) |
| Records | `IPatientService`, `IMedicalRecordService`, `IPatientLookup` |
| Staff | `IStaffService`, `ILeaveService`, `IDoctorLookup` |
| Pharmacy | `IDrugService`, `IPrescriptionService`, `IPharmacistService`, `ISupplierService`, `IPurchaseRequestService`, `IDispensingService`, `IPrescriptionRequestService` |
| Billing | `IInvoiceService`, `IInvoicePaymentService` |
| Feedback | `IComplaintService`, `IComplaintAttachmentService`, `ISurveyService`, `IVisitRatingService`, `IMedicalArticleService` |
| Portal | `IDashboardService` |

`ModuleRegistrationTests` checks that every service resolves.

---

## 7. Authentication & Authorization

- **One login UI:** `/Account/Login`, with lockout armed and local `returnUrl` only. `/Account/AccessDenied` shows a 403 page.
- **Fallback policy:** every endpoint requires a signed-in user unless it is `[AllowAnonymous]` (Home, Login, Register, AccessDenied).
- **Antiforgery:** validated globally on every POST.
- **Roles (7):** Admin, Doctor, Patient, Pharmacist, LabTechnician, Radiologist, EmergencySupervisor. All are created by `AdminSeeder`.
- **Permissions:** `Shared/Identity/Authorization/Permissions.cs` defines 25 permission names. `RolePermissions.cs` is the **only** place that maps roles to permissions. `AddHcmsAuthorization()` registers each permission as a policy that the roles holding it satisfy.
  - Every controller has `[Authorize(Policy = Permissions.<Area>.<Action>)]` (§12).
  - The permissions for Lab, Radiology and Emergency are defined and mapped, but no endpoint uses them yet.
- **Tests:**
  - `RoleAccessMatrixTests` requests every GET route as each of the 7 roles; the response is 403 exactly where the role lacks the controller's permission.
  - `PermissionPolicyTests` checks that no role strings remain.
- **`HcmsControllerBase`:** the controller base class. It gives the current user id, patient id and doctor id through `IPatientLookup`, `IDoctorLookup` and `ICurrentUserService`; it has no data access of its own.

---

## 8. Data Model

Entities live in their module: `src/HCMS4/Modules/<Module>/Models`, namespace `HCMS4.Modules.<Module>.Models`. Identity, audit, notification and report entities live in `Shared/<Area>/Models`. The shared primitives (`ServiceResult`, `Pagination`, `BusinessRules`) moved to `HCMS4.Shared.Common`. All dates are Damascus time. The property tables below describe the domain and are still valid. Since Increment 1, money columns have an explicit `decimal(18,2)` precision, and check constraints guard amounts, quantities and ratings (see §9).

### 8.1 Identity

**`ApplicationUser : IdentityUser`**
| Property | Type | Notes |
|---|---|---|
| `FirstName` | `string` | Required, max 50 |
| `LastName` | `string` | Required, max 50 |
| `FullName` | `string` | Computed → `$"{FirstName} {LastName}"` |
| `Doctor` / `Patient` / `Pharmacist` | nav? | 1:1 back-references to the profile tables |

### 8.2 Core entities

**`Patient`** — `Id`, `UserId` (FK → `Users`, required), `User`, `DateOfBirth` (nullable), `Address` (≤200), `EmergencyContact` (≤20), `ChronicConditions` (≤500), `Status` (`PatientStatus`: Active / Disabled, default Active), `NoShowCount` (int, default 0), `DisableReason`, `DisabledAt`.
Collections: `Appointments`, `Prescriptions`, `Invoices`.

**`Doctor`** — `Id`, `UserId` (FK → `Users`, required), `User`, `SpecializationId?` (FK → `Specialization`, `SetNull` on delete), `Specialization`, `SpecializationName` (computed → "Not specified"), `Qualifications` (≤500), `ContactInfo` (≤50, phone), `IsAvailable` (bool, default `true`), `AverageRating` (`decimal(3,2)`, 0–5), `RatingCount` (int), `CanPublishArticles` (bool, default `true`).
Collections: `Appointments`, `Prescriptions`, `DoctorLeaves`, `MedicalArticles`. Computed `FullName`.

**`Pharmacist`** — `Id`, `UserId` (FK → `Users`), `User`, `Qualifications` (≤500), `ContactInfo` (≤20), `CreatedAt`, `LastLoginAt`, `IsActive` (default `true`), `Shift` (string). Computed `FullName`.

**`Appointment`**
| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `PatientId` | int | Required FK |
| `DoctorId` | int | Required FK |
| `AppointmentDateTime` | `DateTime` | Required; validated by nested `FutureDateAttribute` (must be > `DateTime.Now`) |
| `Status` | `AppointmentStatus` | Scheduled / Completed / Canceled (default Scheduled) |
| `CancellationReason` | `string?` | ≤500 |
| `CreatedAt` / `UpdatedAt` | `DateTime?` | Default SQL `GETUTCDATE()` on `CreatedAt` |
| `ConsultationFee` | `decimal` | 0–10 000, currency |
| `WasNoShow` | `bool` | default `false` — no-show flag |
| `NoShowEvaluatedAt` | `DateTime?` | |
| `NoShowRiskScore` | `double?` | 0–1 — cached ML risk score |

Nested `Appointment.FutureDateAttribute : ValidationAttribute` rejects dates `<= DateTime.Now` with "Appointment must be in the future".

**`Prescription`** — `Id`, `PatientId`, `DoctorId` (both required), `AppointmentId?` (FK, `SetNull` on delete), `PrescriptionDate` (default `GETUTCDATE()`), `Notes` (required, ≤1000), `Status` (`PrescriptionStatus`: Pending / Completed / Canceled), `PrescriptionItems` collection, `DispensedDate?`, `DispensedBy?` (≤100), `TotalCost` (computed → Σ `Quantity × Drug.Price`), `MedicationTotal?` (0–10 000), `CreatedAt`, `UpdatedAt`, `DispensingNotes?` (≤500).

**`PrescriptionItem`** — `Id`, `PrescriptionId`, `DrugId?` (nullable despite `[Required]`), `DrugName` (≤100), `Dosage` (≤50), `Duration` (≤50), `Frequency` (≤50), `Quantity` (1–1000), `Instructions` (≤500), `Prescription`, `Drug`.

**`Drug`** — `Id`, `Name` (≤100), `Supplier?` (≤100), `Price` (0.01–10 000, currency), `Quantity` (0–10 000), `ExpiryDate` (required; nested `FutureDateAttribute`), `Description?` (≤500), `CreatedAt`, `UpdatedAt`, `PrescriptionItems` collection.
Computed `ExpiryStatus`: `"Expired"` (≤0 d), `"Expiring Soon"` (≤30 d), `"Expiring Shortly"` (≤60 d), else `"Valid"` — computed against `DateTime.Now`.

**`ClinicalNote`** — `Id`, `PatientId`, `DoctorId`, `Date` (default `GETUTCDATE()`), `NoteType` (≤50), `Content` (required, ≤2000), `Diagnosis?` (≤500), `Patient`, `Doctor`.

**`Invoice`** — `Id`, `PatientId` (required), `AppointmentId?`, `PrescriptionId?` (both `SetNull` on delete), `InvoiceDate` (default `GETUTCDATE()`), `ConsultationFee` (0–10 000), `MedicationTotal` (0–10 000), `TotalAmount` (0.01–20 000), `PaymentStatus` (`PaymentStatus`: Pending / Paid), `PaymentDate?`, `AmountPaid?`, `Notes?` (≤500), `UpdatedAt`, `PharmacistId?` (FK → `Pharmacist`, "Dispensed By"), `DispensedAt?`.

### 8.3 Pharmacy / procurement

**`Specialization`** — `Id`, `Name` (≤100), `Description?` (≤500), `ConsultationFee` (0–10 000), `IsActive` (default `true`), `Doctors` collection. **Seeded with 6 rows** (see §9).

**`Supplier`** — `Id`, `Name` (≤100), `Address?` (≤200), `Phone?` (≤20), `Email?` (≤100), `ContactPerson?` (≤100), `CreatedAt`, `IsActive` (default `true`), `PurchaseRequests` collection.

**`PurchaseRequest`** — `Id`, `RequestNumber` (required), `PharmacistId` (required FK), `SupplierId?` (FK), `RequestDate` (default UTC now), `Status` (`PurchaseRequestStatus`: Pending / Approved / Rejected / Ordered / Received / Cancelled), `TotalAmount`, `Notes?` (≤500), `ApprovedDate?`, `ApprovedBy?` (≤100), `RejectionReason?` (≤500), `ReceivedDate?`, `CreatedAt`, `UpdatedAt`, `Items` collection.

**`PurchaseRequestItem`** — `Id`, `PurchaseRequestId`, `DrugId`, `DrugName` (≤100), `Quantity` (1–10 000), `UnitPrice` (0.01–10 000), `Subtotal` (computed), `Notes?` (≤500).

### 8.4 Scheduling, leave, feedback

**`DoctorLeave`** — `Id`, `DoctorId`, `Doctor`, `StartDate`, `EndDate`, `LeaveType` (`LeaveType`: Sick / Annual / Emergency / Other), `Notes?` (≤500), `Status` (`LeaveStatus`: Pending / Approved / Rejected), `CreatedAt?`, `CreatedBy?`.

**`LeaveRequest`** — `Id`, `DoctorId`, `Doctor`, `StartDate`, `EndDate`, `LeaveType`, `Reason?` (≤500), `Status` (default Pending), `CreatedAt?`.
> Two parallel leave concepts exist: a doctor-submitted `LeaveRequest` and an admin-registered `DoctorLeave`. Approving a `LeaveRequest` materialises a corresponding `DoctorLeave`.

**`VisitRating`** — `Id`, `PatientId`, `DoctorId`, `AppointmentId`, `Rating` (1–5), `Comment?` (≤1000), `DoctorCooperative?`, `WaitingTimeReasonable?`, `CreatedAt` (default `GETUTCDATE()`).

**`Complaint`** — `Id`, `PatientId`, `Title` (≤200), `Type` (`ComplaintType`: Service / Doctor / Appointments / Billing / Other), `Description` (required, ≤2000), `AssociatedVisitDate?`, `AttachmentPath?` (≤500), `TrackingNumber`, `SubmissionDate` (default `GETUTCDATE()`), `Status` (`ComplaintStatus`: Submitted / UnderReview / Resolved / Closed), `ResolvedAt?`, `ResolutionNotes?` (≤1000).

**`PrescriptionReissueRequest`** — `Id`, `PatientId`, `PrescriptionId`, `DoctorId`, `RequestDate` (default `GETUTCDATE()`), `Status` (`ReissueRequestStatus`: Pending / Approved / Rejected / Expired), `DoctorResponse?` (≤500), `ReviewedAt?`.

**`DoctorReviewRequest`** — `Id`, `PharmacistId`, `PrescriptionId`, `DoctorId`, `ReasonForReview` (`ReviewReason`: UnclearDosage / DrugInteraction / MedicationUnavailable / AlternativeMedication / Other), `AdditionalComments?` (≤1000), `SuggestedAlternative?` (≤500), `RequestDate` (default `GETUTCDATE()`), `Status` (`ReviewRequestStatus`: Pending / Responded / Closed), `DoctorResponse?` (≤1000), `ReviewedAt?`.

**`PharmacistPrescriptionNote`** — `Id`, `PrescriptionId`, `PharmacistId`, `NoteText` (required, ≤2000), `NotifyDoctor` (bool, default false), `CreatedAt` (default `GETUTCDATE()`).

**`MedicalArticle`** — `Id`, `DoctorId`, `Title` (≤200), `Summary?` (≤500), `Content` (required), `Category?` (≤100), `Status` (`ArticleStatus`: Draft / Published, default Draft), `CreatedAt`, `UpdatedAt`, `PublishedAt?`, `Doctor`.

### 8.5 Surveys

**`Survey`** — `Id`, `Title` (≤200), `Description?` (≤1000), `StartDate`, `EndDate?`, `TargetAudience` (`SurveyTargetAudience`: AllPatients / SpecificCategory / SpecificPatients), `TargetCriteria?` (≤300), `Status` (`SurveyStatus`: Draft / Active / Closed), `CreatedByUserId` (FK → `Users`, `Restrict`), `CreatedAt`, `UpdatedAt`, `SentAt?`, `Questions`, `Assignments`.

**`SurveyQuestion`** — `Id`, `SurveyId`, `QuestionText` (≤500), `QuestionType` (`SurveyQuestionType`: MultipleChoice / Rating / YesNo / OpenText), `IsRequired`, `DisplayOrder`, `Options`, `Answers`.

**`SurveyQuestionOption`** — `Id`, `SurveyQuestionId`, `OptionText` (≤200), `DisplayOrder`.

**`SurveyAssignment`** — `Id`, `SurveyId`, `PatientId`, `Status` (`SurveyAssignmentStatus`: Pending / Completed), `AssignedAt`, `CompletedAt?`, `Answers`. **Unique index** on `(SurveyId, PatientId)`.

**`SurveyAnswer`** — `Id`, `SurveyAssignmentId`, `SurveyQuestionId`, `AnswerText?` (≤2000), `NumericValue?`, `BooleanValue?`.

### 8.6 Platform entities

**`UserNotification`** — `Id`, `UserId` (FK → `Users`), `Title` (≤200), `Message` (≤1000), `Type` (`NotificationType`: General / AppointmentReminder / Complaint / PrescriptionReissue / DoctorReview / PrescriptionNote / Survey), `LinkUrl?` (≤500), `RelatedEntityType?` (≤100), `RelatedEntityId?`, `IsRead`, `CreatedAt` (default `GETUTCDATE()`). **Index** on `(UserId, IsRead)`.

**`SystemActivityLog`** — `Id`, `UserId?` (≤450), `UserName?` (≤256), `Action` (mapped to column `ActivityType`, ≤100), `EntityType` (≤100), `EntityId?` (mapped to column `RelatedEntityId`, ≤100), `Description` (≤2000), `CreatedAt` (default `GETUTCDATE()`).

**`DailyReport`** — `Id`, `ReportDate`, `TotalInvoices`, `TotalAmount`, `GeneratedAt`, `ReportData?` (JSON blob).

### 8.7 `HCMS4.Shared.Common`

| Type | Purpose |
|---|---|
| `BusinessRules` (static) | Centralised constants — see [§10](#10-business-rules-catalogue) |
| `ConcurrencyEntity` (abstract) | `[Timestamp] byte[] RowVersion` — optimistic concurrency base class. **Currently unused**: no entity derives from it, and no `RowVersion` column exists on the domain tables. |
| `PaginatedResult<T>` | `Items`, `TotalCount`, `PageNumber`, `PageSize`, computed `TotalPages`, `HasPreviousPage`, `HasNextPage` |
| `PaginationParams` | `PageNumber` (default 1), `PageSize` (default 20, hard-capped at 100) |
| `ServiceResult` | `Success`, `Message`, `Errors` (List<string>); static `Ok(message)` / `Fail(message, errors)` |
| `ServiceResult<T>` : `ServiceResult` | adds `Data`; static `Ok(data, message)` / `Fail(message, errors)` |
| `RiskLevelHelper` (static) | `GetLevel(score)` → `"high"` / `"medium"` / `"low"`; `Matches(selectedRiskLevel, score)` — `"all"`/blank matches everything |

### 8.8 Relationship map

```
ApplicationUser (Identity) 1──1 Doctor / Patient / Pharmacist
      │
      ├──1:N── Appointment ──N:1── Doctor
      │                 └──N:1── Patient
      ├──1:N── Prescription ──N:1── Doctor / Patient
      │                   └──0..1── Appointment
      │                   └──1:N── PrescriptionItem ──N:1── Drug
      ├──1:N── ClinicalNote ──N:1── Doctor + Patient
      ├──1:N── Invoice ──N:1── Patient
      │                ──0..1── Appointment / Prescription
      │                ──0..1── Pharmacist
      ├──1:N── DoctorLeave ──N:1── Doctor
      ├──1:N── LeaveRequest ──N:1── Doctor
      ├──1:N── VisitRating ──N:1── Patient / Doctor / Appointment
      ├──1:N── PrescriptionReissueRequest ── Patient / Prescription / Doctor
      ├──1:N── DoctorReviewRequest ── Pharmacist / Prescription / Doctor
      ├──1:N── PharmacistPrescriptionNote ── Prescription / Pharmacist
      ├──1:N── MedicalArticle ──N:1── Doctor
      ├──1:N── UserNotification
      └──1:N── Survey (as CreatedByUserId)

Specialization 1──N Doctor
Supplier 1──N PurchaseRequest ──1:N PurchaseRequestItem ──N:1 Drug
Survey 1──N SurveyQuestion ──1:N SurveyQuestionOption
Survey 1──N SurveyAssignment ──N:1 Patient
SurveyAssignment 1──N SurveyAnswer ──N:1 SurveyQuestion
```

**Delete behaviours** — `Patient` → appointments/prescriptions/invoices = **Cascade**; `Doctor` → appointments/prescriptions = **Restrict**; `PrescriptionItem` → `Drug` = **Restrict** (prevents deleting a drug referenced by a prescription); `PrescriptionItem` → `Prescription` = **Cascade**; `Appointment`/`Prescription` → `Invoice` = **SetNull**; `Doctor` → `Specialization` = **SetNull**.

---

## 9. Database Schema, Migrations & Seed Data

`src/HCMS4/Data/ApplicationDbContext.cs` defines the `DbSet`s and applies one `IEntityTypeConfiguration<T>` per entity from the modules (`ApplyConfigurationsFromAssembly`). It contains **no** `SaveChanges` override, **no** global query filters, and **no** value converters.

### 9.1 Migrations (29 migrations + model snapshot)

`src/HCMS4/Data/Migrations/` holds the migrations; migrations 1–24 predate Increment 1 and 25–29 were added by it. Originally: **24 migration classes**, 23 `.Designer.cs` snapshot files and 1 `ApplicationDbContextModelSnapshot.cs`. The `<identity>` migration is hand-written and has no `.Designer.cs` companion.

| # | Migration | Purpose |
|---|---|---|
| 1 | `00000000000000_CreateIdentitySchema` | ASP.NET Identity tables |
| 2 | `20251204090228_Init` | Core domain schema |
| 3 | `20251208073926_AddIsAvailableToDoctor` | `Doctor.IsAvailable` |
| 4 | `20251210133141_AddingFieldsToPresc` | Prescription fields |
| 5 | `20251210172704_AddConsultationFeeAndMedicationTotal` | Invoice money split |
| 6 | `20251211062230_AddingFieldsToInvoice` | Invoice extras |
| 7 | `20251218121627_AddingFieldsToPatient` | Patient extras |
| 8 | `20251221145636_FixClinicalNoteValidation` | ClinicalNote constraint fixes |
| 9 | `20260319065424_AddPharmacistTable` | Pharmacist profile table |
| 10 | `20260319065919_EditedPharmacistTable` | Pharmacist tweaks |
| 11 | `20260321094049_EditingInvoiceAndPrescriptionModels` | Model tweaks |
| 12 | `20260321162610_PhInvoice` | `Invoice.PharmacistId` / `DispensedAt` |
| 13 | `20260321163637_DailyReportTable` | `DailyReports` table |
| 14 | `20260322170159_DrugPurchase` | Purchase request tables |
| 15 | `20260407192731_Specialization` | `Specializations` table |
| 16 | `20260416140527_AddIncrement5Features` | Surveys, notifications, activity logs, `NoShowRiskScore`/`WasNoShow` |
| 17 | `20260422110016_AddRejectionReasonToPurchaseRequest` | `PurchaseRequest.RejectionReason` |
| 18 | `20260505114205_Increment6` | Feature bundle |
| 19 | `20260505120000_AddMissingTables` | Feature bundle |
| 20 | `20260506120000_AddDoctorMissingColumns` | Doctor columns |
| 21 | `20260506153000_AddNotificationMetadataColumns` | `UserNotification` metadata |
| 22 | `20260506154500_AddActivityLogMetadataColumns` | `SystemActivityLog` metadata |
| 23 | `20260506170000_AddSurveyMetadataColumns` | `Survey` metadata |
| 24 | `20260506183000_AddSurveyAnswerAnsweredAt` | `SurveyAnswer.AnsweredAt` |
| 25 | `20261117143856_Inc1_Cleanup_Net10Upgrade` | Empty baseline after the .NET 10 upgrade |
| 26 | `20261118072633_Inc1_Cleanup_PaymentMethodAndLeaveRejectionReason` | `Invoice.PaymentMethod`, `LeaveRequest.RejectionReason` |
| 27 | `20261118073100_Inc1_Cleanup_DamascusTime` | SQL defaults → Damascus time; one-time +3 h on system timestamps |
| 28 | `20261119121620_Inc1_Cleanup_DefaultsAndConstraints` | Last 3 UTC defaults; explicit money precision; 12 check constraints; 4 unique indexes |
| 29 | `20261119123826_Inc1_Cleanup_EnglishSpecializationDescriptions` | English seed descriptions |
| — | *(snapshot)* `ApplicationDbContextModelSnapshot.cs` | Current model snapshot (61 KB) — not a migration |

### 9.2 Seed data

`SpecializationConfiguration` seeds 6 specializations (`HasData`); the descriptions are English since migration 29:

| Id | Name | ConsultationFee | Description |
|---|---|---|---|
| 1 | Cardiology | 150 | Heart and blood vessels |
| 2 | Dermatology | 120 | Skin, hair and nails |
| 3 | Orthopedics | 130 | Bones, joints and muscles |
| 4 | Neurology | 180 | Brain and nervous system |
| 5 | Pediatrics | 110 | Children's health |
| 6 | General Medicine | 100 | General health care |

### 9.3 SQL Server defaults

Every date default is Damascus time: `CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)` (`ClinicTime.SqlNow`). No `GETUTCDATE()` default remains (migrations 27 and 28).

### 9.4 Admin seeding (`Shared/Identity/Services/AdminSeeder.cs`)

1. Ensures the 7 roles exist (`Roles.All`).
2. Reads `AdminSettings:Email` / `UserName` / `Password` (User Secrets).
3. **Fails fast** when they are missing or the admin cannot be created; there are no default credentials.
4. Creates the admin user if needed and adds it to the `Admin` role.

---

## 10. Business Rules Catalogue

`src/HCMS4/Shared/Common/BusinessRules.cs`:

| Constant | Value | Meaning |
|---|---|---|
| `AppointmentCancellationWindowHours` | 24 | Minimum notice before an appointment can be cancelled |
| `AppointmentSlotBufferMinutes` | 30 | Buffer around a slot that blocks overlapping bookings |
| `UpcomingAppointmentsWindowDays` | 30 | Look-ahead window for "upcoming" lists |
| `MaxUpcomingAppointments` | 50 | Cap on upcoming-appointment results |
| `DrugExpiryWarningDays` | 30 | "Expiring Soon" threshold |
| `DrugExpiryShortWarningDays` | 60 | "Expiring Shortly" threshold |
| `LowStockThreshold` | 10 | Quantity below which a drug is "low stock" |
| `HighRiskThreshold` | 0.7 | No-show score ≥ 0.7 → high risk |
| `MediumRiskThreshold` | 0.4 | No-show score ≥ 0.4 → medium risk |
| `PrescriptionReissueValidityDays` | 30 | Days after which a prescription cannot be re-issued |
| `ComplaintAttachmentMaxBytes` | 5 MiB | Max complaint attachment size |
| `DefaultPageSize` | 20 | Default pagination page size |
| `MaxPageSize` | 100 | Hard cap on page size |

---

## 11. Service Layer

All data access goes through services that return `ServiceResult` / `ServiceResult<T>` or plain query results. Every service reads the time from `IClinicClock`. Lookups that can find nothing return `Task<T?>`.

| Module | Service | Responsibility |
|---|---|---|
| Appointments | `IAppointmentService` | Booking (future date, ±30 min slot check), reschedule, cancel (24 h window), lists, consultation fee, past → completed |
| | `IDailyVisitReportService` | The doctor's daily visiting-patients report |
| | `INoShowRiskService` | No-show scores from the AI service. It falls back to the local estimate on timeout, error, 503 or a missing key, and is stateless (`NoShowRiskResult`) |
| Records | `IPatientService` | Patients: list, details, edit, enable/disable, delete, own details |
| | `IMedicalRecordService` | A doctor's view of a patient's record. Access requires an appointment with the patient, and the same rule applies to adding clinical notes |
| Staff | `IStaffService` | Doctors, pharmacists, specializations and fees, bookable doctors (available, not on approved leave), user deletion |
| | `ILeaveService` | Leave requests, approval or rejection with a reason, registered leaves; notifies the admins |
| Pharmacy | `IDrugService` | Drug catalogue, stock, expiry status (30 / 60 days), prescribable drugs |
| | `IPrescriptionService` | Prescriptions. Creating one **checks** stock and expiry but **does not take stock**; only pending prescriptions can be canceled |
| | `IDispensingService` | The pharmacist's queue. Dispensing covers the **whole** prescription (nothing if any item is short): it takes the stock, completes the prescription and creates one pending invoice (+5 % tax). Also notes and doctor review requests |
| | `IPrescriptionRequestService` | Patient prescription views and re-issuance requests (completed, ≤ 30 days, one pending); the doctor's answers to re-issuance and review requests |
| | `ISupplierService`, `IPurchaseRequestService` | Suppliers (unique active name; deactivate when used); purchase requests (create → approve adds stock / reject with a reason) |
| | `IPharmacistService` | Pharmacist profile, hold a prescription |
| Billing | `IInvoiceService` | Admin invoice list (filters, totals, pages), invoice generation (total = fee + medication, ownership checks), payment status updates, patient invoice views |
| | `IInvoicePaymentService` | Patient payments: cash only, never above the remaining balance |
| Feedback | `IComplaintService`, `IComplaintAttachmentService` | Complaints (tracking number, status flow) and attachments (stored outside `wwwroot`; type, size and signature checks) |
| | `ISurveyService` | Surveys: create, send to a target audience, answer once |
| | `IVisitRatingService` | One rating per completed visit; updates the doctor's average |
| | `IMedicalArticleService` | Doctor articles: draft or publish, with a publishing permission |
| Portal | `IDashboardService` | Read-only figures for the four role dashboards |
| Shared | `INotificationService`, `IActivityLogService`, `IDailyReportService`, `ICurrentUserService`, `IPatientLookup`, `IDoctorLookup` | Notifications, audit log, daily pharmacy report, current user lookups |

**Cross-module data access (known).** Some services read or write other modules' tables directly through the shared `DbContext` instead of going through an interface:
- `StaffService` (cascading user deletion).
- `MedicalRecordService`, `InvoiceService`, `SurveyService` and `DashboardService` (reads).
- `DispensingService` (creates invoices).
- `VisitRatingService` (updates the doctor's rating).

`Shared/Workflow/StateMachine<TStatus>` is the reusable status helper for the next increments' orders. It allows only declared transitions and records the time and user of every change.

---

## 12. Controller Layer

Controllers are thin. They derive from `HcmsControllerBase` (or `Controller` for Home/Account), call services, and never use `ApplicationDbContext`. Several module controllers share one URL prefix through `[ControllerName]`.

| URL prefix | Controller (module) | Policy | Actions |
|---|---|---|---|
| `/Admin` | `AdminPortalController` (Portal) | `Portal.AdminDashboard` | 1 |
| | `AdminStaffController` (Staff) | `Staff.Manage` | 17 |
| | `AdminRecordsController` (Records) | `Records.ManagePatients` | 8 |
| | `AdminAppointmentsController` (Appointments) | `Appointments.Manage` | 9 |
| | `AdminPharmacyController` (Pharmacy) | `Pharmacy.Oversee` | 11 |
| | `AdminBillingController` (Billing) | `Billing.ManageInvoices` | 10 |
| | `AdminFeedbackController` (Feedback) | `Feedback.Manage` | 6 |
| `/Doctor` | `DoctorPortalController` | `Portal.DoctorDashboard` | 1 |
| | `DoctorStaffController` | `Staff.OwnProfile` | 3 |
| | `DoctorRecordsController` | `Records.ViewMedicalRecords` | 3 |
| | `DoctorAppointmentsController` | `Appointments.ViewOwnSchedule` | 6 |
| | `DoctorPrescriptionsController` | `Pharmacy.Prescribe` | 6 |
| | `DoctorArticlesController` | `Feedback.PublishArticles` | 4 |
| `/Patient` | `PatientPortalController` | `Portal.PatientDashboard` | 1 |
| | `PatientAppointmentsController` | `Appointments.Book` | 4 |
| | `PatientPrescriptionsController` | `Pharmacy.ViewOwnPrescriptions` | 3 |
| | `PatientBillingController` | `Billing.ViewOwnInvoices` | 3 |
| | `PatientFeedbackController` | `Feedback.Submit` | 8 |
| `/Pharmacist` | `PharmacistPortalController` | `Portal.PharmacistDashboard` | 1 |
| | `PharmacistPharmacyController` | `Pharmacy.Dispense` | 20 |
| `/ComplaintAttachments` | `ComplaintAttachmentsController` (Feedback) | `Feedback.ViewComplaintAttachments` | 1 |
| `/` , `/Home` | `HomeController` (Portal) | public | 4 |
| `/Account` | `AccountController` (Shared/Identity) | public / signed-in (fallback) | 10 |

---

## 13. ViewModels

> **Since Increment 1:** the view models are in `src/HCMS4/ViewModels` (namespace `HCMS4.ViewModels`). The 8 unused ones were deleted, and new ones were added for the admin invoice list (`AdminInvoiceListViewModel`) and the new services.

`HCMS4/ViewModels/` — 61 files, 74 class declarations. All are plain POCOs with DataAnnotations; there is no FluentValidation.

### Authentication / profile
`LoginViewModel` (Email, Password, RememberMe), `RegisterViewModel` (FirstName, LastName, Email, Password, ConfirmPassword, PhoneNumber, DateOfBirth, ChronicConditions, PreferredTimeOfDay + two read-only suggestion lists), `ChangePasswordViewModel`, `ProfileViewModel` (roles, doctor/patient profile flags, specialisation, address, emergency contact), `EditProfileViewModel`, `UserViewModel`.

### Doctor
`DoctorRegisterViewModel`, `DoctorDashboardViewModel`, `DoctorAppointmentsViewModel` (today/upcoming/past buckets, `ViewMode`, `SelectedRiskLevel`, `AnalyticsServiceAvailable`, `IsUsingAI`, `AppointmentRiskScores`), `DoctorScheduleViewModel`, `DoctorSelectDto`, `MedicalArticleEditorViewModel`.

### Patient
`PatientDashboardViewModel`, `PatientBookAppointmentViewModel` (also carries `RecommendedDoctors` / `IsUrgent`), `BookAppointmentViewModel` (shared by patient + admin booking; `AvailableDoctors` / `AvailablePatients` / `Symptoms`), `AppointmentHistoryViewModel`, `RescheduleAppointmentViewModel`, `PatientMedicalRecordViewModel`, `PatientPrescriptionDetailViewModel`, `PatientPrescriptionsListViewModel`, `PatientPrescriptionsViewModel`, `PatientViewModel`, `PatientSelectDto`, `PatientStatsViewModel`, `PatientComplaintFormViewModel`, `VisitRatingFormViewModel`, `SurveyResponseViewModel` (+ `SurveyResponseQuestionViewModel`).

### Clinical
`ClinicalNoteViewModel`, `CreateClinicalNoteViewModel`, `CreatePrescriptionViewModel`, `PrescriptionItemCreateViewModel` (`SelectedDrugId`, `Price`, `AvailableStock`, `ExpiryStatus`), `PrescriptionItemViewModel`, `PrescriptionItemDetailViewModel`, `PrescriptionViewModel`, `PendingPrescriptionViewModel`, `PrescriptionNoteFormViewModel`, `PrescriptionNoteDisplayViewModel`, `PrescriptionReviewRequestFormViewModel`.

### Pharmacy / finance
`DrugSelectionViewModel`, `DrugAlternativeViewModel`, `ExpiryAlertsViewModel`, `InvoiceViewModel`, `GenerateInvoiceViewModel` (+ nested `AppointmentSelectDto`, `PrescriptionSelectDto`), `DailyInvoiceItem`, `DailyPharmacyReportViewModel`, `DailyReportFilterViewModel`, `DailyVisitingPatientsReportViewModel` (+ `VisitingPatientItem`), `AdminPurchaseRequestViewModel.cs` (contains `AdminPurchaseRequestListViewModel` and `AdminPurchaseRequestDetailViewModel`), `PurchaseRequestListViewModel`, `PurchaseRequestDetailViewModel`, `PurchaseRequestViewModel.cs` (contains `CreatePurchaseRequestViewModel` + `SupplierSelectDto`, `DrugSelectDto`, `PurchaseRequestItemDto`).

### Pharmacist
`PharmacistRegisterViewModel`, `PharmacistEditViewModel`, `PharmacistEditProfileViewModel`, `PharmacistProfileViewModel`, `PharmacistViewModel`, `PharmacistDashboardViewModel`.

### Surveys / misc
`SurveyEditorViewModel` (+ `SurveyQuestionEditorViewModel`), `LeaveRequestViewModel.cs` (contains `LeaveRequestViewModel`, `DoctorLeaveViewModel`, `DoctorLeaveItem`, `RegisterDoctorLeaveViewModel`), `DisablePatientViewModel`.

---

## 14. Razor Views & UI

> **Since Increment 1:**
> - Views live in `src/HCMS4/Modules/<Module>/Views/<Role>/`; only Account, Home and Shared stay under `Views/`.
> - The UI is English and left-to-right; jQuery and validation are loaded once.
> - The booking page script was repaired.
> - New page: `/Admin/ManageInvoices`.
> - The pharmacist dispensing page has no item selection: the whole prescription is dispensed.

`HCMS4/Views/` — 82 `.cshtml` files.

### Layout & shared
| File | Purpose |
|---|---|
| `_ViewStart.cshtml` | Chooses the layout at runtime: `_LayoutAuth` when `User.Identity.IsAuthenticated`, otherwise `_Layout` |
| `_ViewImports.cshtml` | `@using HCMS4`, `@using HCMS4.Models`, tag helpers |
| `Shared/_Layout.cshtml` | Guest layout (Bootstrap, AOS, GLightbox, Swiper, PureCounter, `main.css`) |
| `Shared/_LayoutAuth.cshtml` | Authenticated layout (adds `admintemp.css` and the role-aware sidebar/nav) |
| `Shared/_Layout.cshtml.css` | Scoped CSS for the guest layout |
| `Shared/_ValidationScriptsPartial.cshtml` | jQuery validation bundle |
| `Shared/_AppointmentStatusBadge.cshtml` | Status badge partial |
| `Shared/Error.cshtml` | Error page (`ErrorViewModel`) |

### Per-area view inventory
- **Account (4):** `Login`, `Register`, `Profile`, `EditProfile`
- **Home (4):** `Index`, `About`, `Contact`, `Privacy`
- **Admin (33):** `Dashboard`, `Doctors`, `CreateDoctor`, `Patients`, `EditPatient`, `PatientDetails`, `DisablePatient`, `ManageAppointments`, `BookAppointment`, `RescheduleAppointment`, `DoctorSchedule`, `RegisterDoctorLeave`, `ViewDoctorLeaves`, `PendingPrescriptions`, `GenerateInvoice`, `ViewInvoice`, `InvoiceDetails`, `UpdatePaymentStatus`, `DailyPharmacyReport`, `DailyVisitingPatientsReport`, `PriceList`, `Suppliers`, `CreateSupplier`, `EditSupplier`, `Pharmacists`, `CreatePharmacist`, `EditPharmacist`, `PurchaseRequests`, `PurchaseRequestDetails`, `Complaints`, `ComplaintDetails`, `Surveys`, `CreateSurvey`
- **Doctor (11):** `Dashboard`, `ViewAppointments`, `PatientRecord`, `CreateClinicalNote`, `CreatePrescription`, `Profile`, `RequestLeave`, `ReissueRequests`, `DoctorReviewRequests`, `MedicalArticles`, `CreateMedicalArticle`
- **Patient (11):** `Dashboard`, `BookAppointment`, `ViewAppointmentHistory`, `InvoiceHistory`, `Prescriptions`, `PrescriptionDetails`, `RateVisit`, `Complaints`, `CreateComplaint`, `Surveys`, `TakeSurvey`
- **Pharmacist (12):** `Dashboard`, `Drugs`, `CreateDrug`, `EditDrug`, `Prescriptions`, `ProcessPrescription`, `PrescriptionDetails`, `RequestDoctorReview`, `AddPrescriptionNote`, `Profile`, `PurchaseRequests`, `CreatePurchaseRequest`, `PurchaseRequestDetails`
- **Shared (5 `.cshtml` + 1 `.cshtml.css`):** `_Layout`, `_LayoutAuth`, `_ValidationScriptsPartial`, `_AppointmentStatusBadge`, `Error` (+ `_Layout.cshtml.css`)
- **Identity scaffold (`Areas/Identity/Pages/`):** full `Account/` set — `Login`, `Logout`, `Register`, `AccessDenied`, `ConfirmEmail`, `ExternalLogin`, `ForgotPassword`, `Lockout`, `ResetPassword`, `LoginWith2fa`, `LoginWithRecoveryCode`, plus the complete `Account/Manage/` area (change/delete email, password, 2FA, personal data, external logins, recovery codes).

### Notable client-side behaviours
- `Views/Patient/BookAppointment.cshtml` contains an "AI assistant" block that calls `/Patient/GetAppointmentSuggestions?patientId=…`. **No such action exists** on `PatientController` → the request always 404s.
- The same file contains a **corrupted JavaScript fragment** (duplicated lines around 229–235) that leaves an unbalanced block and an empty `if (patientId) { … }` body inside `initializeAssistant()`. The script block as written is not syntactically valid.
- `Views/Shared/_LayoutAuth.cshtml` loads jQuery **twice** (`code.jquery.com/jquery-3.6.0.min.js` at the top and `jquery-3.7.1.min.js` at the bottom) plus two copies of the validation libraries (CDN 1.19.5 / 4.0.0 at the top, and the local `~/lib/...` copies at the bottom).

---

## 15. Front-end Assets

`HCMS4/wwwroot/`

| Path | Contents |
|---|---|
| `assets/css/main.css` | 37 KB main theme (referenced by both layouts) |
| `assets/css/admintemp.css` | 14 KB admin dashboard theme (referenced only by `_LayoutAuth.cshtml`) |
| `assets/css/dashboard.css` | 1.9 KB — **orphaned**, not referenced by any view |
| `assets/js/main.js` | 5 KB shared client script (loaded by both layouts) |
| `assets/scss/` | SCSS sources for the theme |
| `assets/img/` | images, plus `doctors/`, `gallery/`, `testimonials/` |
| `assets/vendor/bootstrap/` | Bootstrap 5 CSS + JS (full, min, rtl, grid, reboot, utilities, esm) |
| `assets/vendor/bootstrap-icons/` | icon font + CSS + JSON manifest |
| `assets/vendor/fontawesome-free/` | Font Awesome 6 (solid/regular/brands, webfonts, svg) |
| `assets/vendor/aos/` | AOS scroll animations (js/css/esm/cjs) |
| `assets/vendor/glightbox/` | lightbox |
| `assets/vendor/swiper/` | Swiper slider |
| `assets/vendor/purecounter/` | counter animation |
| `assets/vendor/php-email-form/validate.js` | unused contact-form validator |
| `lib/jquery/dist/jquery.min.js` | local jQuery copy (also loaded from CDN) |
| `lib/jquery-validation/dist/jquery.validate.min.js` | local validation |
| `lib/jquery-validation-unobtrusiv/jquery.validate.unobtrusive.min.js` | local unobtrusive validation |

---

## 16. ML No-Show Prediction Microservice

### 16.1 The Flask service

`medical-appointment-no-shows-main/medical-appointment-no-shows-main/scripts/predict.py`

- Loads `../models/LGBMClassifier_tranformers_final.bin` — a pickle containing `(scaler, ohe, model)`.
- `prepare_data(df, test)`: renames `Hipertension` → `Hypertension`, parses `ScheduledDay`/`AppointmentDay` as datetimes, lower-cases column names and replaces `-` with `_`, lower-cases categorical string values and replaces spaces with `_`.
- `feature_engineering(df)`: for `scheduledday` and `appointmentday` it derives `Year, Month, Week, Day, Dayofweek, Dayofyear, Is_month_end, Is_month_start, Is_quarter_end, Is_quarter_start, Hour, Minute, Second` and drops the original columns.
- Feature order:
  - **Numerical (10):** `patientid, age, scheduleddayWeek, scheduleddayDay, scheduleddayDayofyear, scheduleddayHour, scheduleddayMinute, scheduleddaySecond, appointmentdayDay, appointmentdayDayofyear`
  - **Categorical (20):** `gender, scholarship, hypertension, diabetes, alcoholism, handcap, sms_received, scheduleddayYear, scheduleddayMonth, scheduleddayDayofweek, scheduleddayIs_month_end, scheduleddayIs_month_start, scheduleddayIs_quarter_end, scheduleddayIs_quarter_start, appointmentdayMonth, appointmentdayWeek, appointmentdayDayofweek, appointmentdayIs_month_end, appointmentdayIs_month_start, neighbourhood`
- `POST /predict` → `model.predict_proba(X_test)[0,1]`; `no_show = y_pred >= 0.5`.
- Response body: `{ "no_show_probability": <float>, "no_show": <bool> }`.
- Served via `app.run(debug=True, host='0.0.0.0', port=9696)` — **`debug=True` on a network-facing host is unsafe.**

### 16.2 The .NET client

> **Since Increment 1:**
> - The configuration section is `AiService` (`BaseUrl`, `ApiKey`), and the client sends `X-API-Key`.
> - Without a key, or on timeout, error or 503, it uses the local estimate.
> - The service is stateless and returns `NoShowRiskResult`.
> - The Flask service runs with `debug=False` on `127.0.0.1` and requires `HCMS_AI_API_KEY`.

`HCMS4/Services/NoShowRiskService.cs`

**Request** → `POST {NoShowAPI:BaseUrl}/predict`, JSON body (`NoShowAppointmentRequest`):

| JSON key | C# type | Value sent by HCMS4 |
|---|---|---|
| `PatientId` | `double` | `appointment.PatientId` |
| `AppointmentID` | `int` | `appointment.Id` |
| `Gender` | `string` | hard-coded `"m"` |
| `ScheduledDay` | `string` | `appointment.CreatedAt ?? UtcNow` formatted `yyyy-MM-dd HH:mm:ss` |
| `AppointmentDay` | `string` | `appointment.AppointmentDateTime` formatted `yyyy-MM-dd 00:00:00` |
| `Age` | `int` | computed from `Patient.DateOfBirth` (default 30, clamped 0–120) |
| `Neighbourhood` | `string` | hard-coded `"clinic"` |
| `Scholarship` | `bool` | hard-coded `false` |
| `Hipertension` | `bool` | hard-coded `false` |
| `Diabetes` | `bool` | hard-coded `false` |
| `Alcoholism` | `bool` | hard-coded `false` |
| `Handcap` | `int` | hard-coded `0` |
| `SMS_received` | `bool` | hard-coded `false` |

> The clinical/socio-economic features are **all constants** — the model therefore sees no real patient data beyond age and the two dates. The `Hipertension` (sic) key matches the Flask side's rename of `Hipertension` → `Hypertension`, and `sms_received` matches the lower-cased column name.

**Response** → `NoShowPredictionResponse`: `no_show_probability` (double), `no_show` (bool).

**Transport details**
- Named `HttpClient` `"NoShowAPI"` (30 s timeout) created via `IHttpClientFactory`; `NoShowRiskService` additionally overrides the timeout to **10 s** per call.
- `NoShowAPI:ApiKey` is **never sent** — the endpoint is unauthenticated.
- On any failure (`HttpRequestException`, non-success status, deserialization failure) the service logs, sets `_serviceAvailable = false` / `_lastErrorMessage`, and falls back to the local heuristic (`CalculateRiskScoresLocallyAsync`).
- `IsUsingAI` is `true` only when at least one live API prediction was obtained.

**Where it is consumed**
- `AdminController.ManageAppointments` (line ~401) → fills `ManageAppointmentsViewModel.AppointmentRiskScores` and the `AnalyticsServiceAvailable` / `IsUsingAI` flags.
- `DoctorController.ViewAppointments` (line ~208) → fills `DoctorAppointmentsViewModel.AppointmentRiskScores`.

**Risk levels** — `RiskLevelHelper.GetLevel(score)` → `"high"` (≥ 0.7), `"medium"` (≥ 0.4), else `"low"`; `Matches(selected, score)` treats `"all"`/blank as match-all.

### 16.3 Other ML assets
`models/` also contains `DecisionTreeClassifier.bin`, `LGBMClassifier.bin`, `LogisticRegression.bin`, `RandomForestClassifier.bin`, `XGBClassifier.bin`, `XGBClassifier_final.bin`, `LGBMClassifier_tranformers_final.bin` (the one in use) and `XGBClassifier_tranformers_final.bin`. `scripts/train.py` reproduces the training pipeline; `data/KaggleV2-May-2016.csv` is the source dataset.

---

## 17. Test Suite

`tests/HCMS4.Tests/` is an xUnit project in `HCMS4.sln` with **371 tests, all passing**. Service line coverage is **82.8 %**: every module is at or above 70 %; the lowest files are `DrugService` 30 %, `DailyReportBackgroundService` 36 % and `LeaveService` 46 %.

| Kind | Examples |
|---|---|
| Service unit tests (EF InMemory, `FakeTimeProvider`) | `StaffServiceTests`, `MedicalRecordServiceTests`, `AppointmentQueryTests`, `PharmacyWorkflowServiceTests`, `InvoiceServiceTests`, `FeedbackServiceTests`, `DashboardServiceTests`, `StateMachineTests` |
| Integration tests (`HcmsWebApplicationFactory`, `TestAuthHandler` header login, antiforgery helper) | `RoleAccessMatrixTests` (every GET route × 7 roles), `PageRenderSmokeTests` (60+ pages), `RouteInventoryTests`, `AntiforgeryTests`, `FallbackPolicyTests`, `LoginLockoutTests` |
| Model / architecture checks | `ModelSnapshotTests` (no pending model changes), `DatabaseConstraintsModelTests`, `ModuleRegistrationTests`, `PermissionPolicyTests` |
| Guard rails | `UiLanguageTests` (no Arabic text or RTL markup in src), `ClockUsageTests` (no `DateTime.Now/UtcNow/Today` in src or tests) |

**Known gaps:**
- `DrugServiceTests` and `AdminServiceTests` still test EF directly instead of the services (finding N1).
- The Flask service has no automated tests.

```powershell
dotnet test HCMS4.sln
# Passed! - Failed: 0, Passed: 371
```

---

## 18. Key End-to-End Workflows

> Controller names below are the original ones. Since Increment 1 the same URLs are served by the module controllers in §12, and the logic lives in the services of §11.

### 18.1 Patient books an appointment
1. `PatientController.BookAppointment()` (GET) loads available doctors (`Doctor.IsAvailable`) into `BookAppointmentViewModel.AvailableDoctors`.
2. The view calls `GET /Patient/GetDoctorConsultationFee?doctorId=` to display the fee.
3. `BookAppointment(BookAppointmentViewModel)` (POST) → `IAppointmentService.CreateAsync` → `IsTimeSlotAvailableAsync` (±30 min conflict check, canceled appointments ignored) → insert with `Status = Scheduled` and `ConsultationFee` from the doctor's specialization.
4. A notification can be raised; the appointment appears in the patient, doctor and admin dashboards.

### 18.2 Doctor issues a prescription
1. `DoctorController.CreatePrescription(patientId, appointmentId?)` (GET) loads the drug catalogue into `CreatePrescriptionViewModel.AvailableDrugs`.
2. `CreatePrescription(CreatePrescriptionViewModel)` (POST) builds `Prescription` + `PrescriptionItem`s and calls `IPrescriptionService.CreateAsync`.
3. `CreateAsync` validates every line (drug exists, stock sufficient, not expired), returns **all** validation errors at once, then inserts the prescription (status `Pending`, `MedicationTotal` computed). **Since Increment 1 it does not take stock**; stock leaves only when the pharmacist dispenses.
4. The prescription appears in the pharmacist's pending queue.

### 18.3 Pharmacist dispenses (since Increment 1)
1. `ProcessPrescription(id)` (GET) shows every line, current stock and the totals; dispensing is disabled when any line is short.
2. `DispensePrescription(id, dispensingNotes)` (POST) → `IDispensingService.DispenseAsync`. In one transaction it takes the stock for **all** lines, sets `Completed` / `DispensedDate` / `DispensedBy`, and creates **one** pending invoice (medication + 5 % tax). There is no partial dispensing, and a completed prescription cannot be dispensed again.
3. A pharmacist note can be added (optionally notifying the doctor), or a doctor review requested.
4. The former "mark as completed" shortcuts (admin and pharmacist) were removed: dispensing is the only way to complete a prescription.

### 18.4 Billing
1. `AdminController.GenerateInvoice(patientId)` (GET) loads recent appointments, recent prescriptions and existing invoices.
2. `GenerateInvoice(GenerateInvoiceViewModel)` (POST) creates the `Invoice` with `ConsultationFee` + `MedicationTotal` = `TotalAmount`, `PaymentStatus = Pending`.
3. `UpdatePaymentStatusPost(id)` (POST) sets `PaymentStatus = Paid`, `AmountPaid`, `PaymentDate`.
4. `PatientController.ProcessPayment(invoiceId, amount, method, notes)` lets the patient settle their own invoice (cash only; partial payments allowed, never above the remaining balance). Admins list and filter all invoices at `/Admin/ManageInvoices`.
5. `DailyReportService` aggregates pharmacy invoices (`PrescriptionId != null`) per day; `DailyReportBackgroundService` persists a `DailyReport` row for yesterday every night.

### 18.5 Doctor leave
1. `DoctorController.RequestLeave(LeaveRequestViewModel)` (POST) → `ILeaveService.CreateLeaveRequestAsync` → `LeaveRequest` (Pending).
2. `AdminController.ApproveLeaveRequest(requestId, notes)` (POST) → `ILeaveService.ApproveLeaveRequestAsync` → creates an approved `DoctorLeave` **and** approves the request.
3. `AdminController.RegisterDoctorLeave(RegisterDoctorLeaveViewModel)` (POST) → `ILeaveService.RegisterDoctorLeaveAsync` → validated against overlapping approved leaves.
4. `ILeaveService.IsDoctorOnLeaveAsync(doctorId, date)` reports whether a doctor is on leave.

### 18.6 No-show risk scoring
1. `AdminController.ManageAppointments` / `DoctorController.ViewAppointments` call `INoShowRiskService.CalculateRiskScoresAsync(ids)`.
2. Cached `Appointment.NoShowRiskScore` values are reused; otherwise `POST /predict` is called per appointment.
3. Failures fall back to the local heuristic; the UI shows a "local estimate" badge via `AnalyticsServiceAvailable` / `IsUsingAI`.
4. `RiskLevelHelper` buckets each score and the views filter by the selected level.

### 18.7 Survey lifecycle
`AdminController.CreateSurvey(SurveyEditorViewModel)` → persists the survey + questions/options → `GetSurveyTargetPatientsAsync` resolves the audience → creates `SurveyAssignment` rows → `PatientController.TakeSurvey(id)` (GET) renders the questions → `TakeSurvey(SurveyResponseViewModel)` (POST) writes `SurveyAnswer` rows and flips the assignment to `Completed`.

---

## 19. Known Issues, Defects & Risks

### Status after Increment 1 (2026-11-19)

| # | Status | # | Status | # | Status |
|---|---|---|---|---|---|
| 1 | Fixed | 14 | Fixed (Damascus time everywhere) | 27 | Fixed |
| 2 | Fixed | 15 | Fixed (nightly job) | 28 | Open (low, harmless) |
| 3 | Fixed (User Secrets) | 16 | Fixed | 29 | Fixed |
| 4 | Fixed | 17 | Fixed | 30 | Fixed |
| 5 | Fixed | 18 | Fixed | 31 | Fixed (cash only) |
| 6 | Fixed | 19 | Fixed | 32 | Fixed |
| 7 | Fixed | 20 | Fixed | 33 | Fixed |
| 8 | Fixed | 21 | Fixed | 34 | Fixed |
| 9 | Fixed | 22 | Fixed | 35 | Fixed |
| 10 | Fixed | 23 | Fixed | 36 | Fixed |
| 11 | Fixed | 24 | Deferred (needs a decision; changes the schema and the leave flow) | 37 | Kept (intentional) |
| 12 | Fixed (.NET 10) | 25 | Fixed | 38 | Fixed (`BaseController` removed) |
| 13 | Fixed (0 vulnerable packages) | 26 | Deferred to A1 (Increment 7) | | |

The numbered descriptions below are the **original findings of 2026-10-21**, kept for reference. File names and line numbers in them refer to the code before Increment 1.

### Original findings (2026-10-21)

#### Critical

1. **`AdminController` is reachable by any Doctor** — the class attribute is `[Authorize(Roles = "Admin,Doctor")]`, so doctors can list/edit patients, create doctors, approve purchase requests, change specialization fees, generate invoices and change payment status. Only complaints and surveys are Admin-only. Fix: change the class attribute to `[Authorize(Roles = "Admin")]` and re-apply `[Authorize(Roles = "Doctor")]` (or move `DailyVisitingPatientsReport` into `DoctorController`).
2. **Hard-coded fallback admin credentials** — `AdminSeeder` falls back to `admin@hospital.com` / `Admin@123` when User Secrets are not configured. Any deployment that forgets `dotnet user-secrets set` ships a publicly known admin password.
3. **SQL Server connection string committed** in `appsettings.json`, pointing at a specific developer machine.
4. **`app.run(debug=True, host='0.0.0.0')`** in the Flask service exposes the Werkzeug debugger on all interfaces.
5. **`NoShowAPI:ApiKey` is configured but never used**; the ML endpoint is completely unauthenticated.

#### High

6. **Account lockout never triggers.** `Program.cs` configures Identity lockout (5 attempts / 5-minute window), but **both** login implementations pass `lockoutOnFailure: false`:
   - `Controllers/AccountController.cs` → `PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false)`
   - `Areas/Identity/Pages/Account/Login.cshtml.cs` → `PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false)` (with the scaffold comment "To enable password failures to trigger account lockout, set lockoutOnFailure: true")

   Brute-force protection is therefore effectively disabled on both login screens.
7. **Corrupted JavaScript in `Views/Patient/BookAppointment.cshtml`** — an orphaned fragment (duplicate `this.classList.remove(...)` / `showToast(...)` block at lines ~229–235) sits outside any function and leaves the braces unbalanced. Extracting the `@section Scripts` block and running `node --check` fails with `SyntaxError: missing ) after argument list`, so the **entire** script block fails to parse and no booking-page JS runs at all. `initializeAssistant()` (line 276) additionally has an empty `if (patientId) { ... }` body.
8. **`/Patient/GetAppointmentSuggestions` does not exist** — the assistant UI always 404s.
9. **`AccessDeniedPath = "/Account/AccessDenied"`** points to a non-existent action; there is no `Views/Account/AccessDenied.cshtml`. Role-denied users get a 404.
10. **Two competing login UIs** — `/Account/Login` (MVC) and `/Identity/Account/Login` (scaffolded Razor Page), with different look-ups and behaviours.
11. **Test project is not in `HCMS4.sln`** — `dotnet test HCMS4.sln` silently runs zero tests in CI.
12. **`net6.0` is out of support** (build emits `NETSDK1138`); the test project is on `net10.0` while the app is on `net6.0`.
13. **44 NuGet advisories** on the test project, including a **critical** `System.Drawing.Common 4.7.0` vulnerability (`GHSA-rxg9-xrhp-64gj`).

#### Medium

14. **`Drug` mixes UTC and local time** — `CreatedAt`/`UpdatedAt` use `DateTime.UtcNow` while `ExpiryStatus` compares `ExpiryDate` to `DateTime.Now`. `DrugService.GetAllAsync` and `GetExpiringDrugsAsync` also compare `ExpiryDate` against `DateTime.UtcNow`. `PrescriptionService` uses `DateTime.UtcNow` for expiry checks. The same inconsistency exists in `AppointmentService.CancelAsync`, which compares a local appointment time against `DateTime.UtcNow`, and in `PatientController.ProcessPayment`, which stamps `PaymentDate = DateTime.Now`.
15. **Past appointments only auto-complete when an admin opens the appointments page.** `AppointmentService.UpdatePastScheduledToCompletedAsync()` has exactly one call site — `AdminController.ManageAppointments` (`AdminController.cs:358`). `DailyReportBackgroundService` does **not** call it, so `Scheduled` appointments stay `Scheduled` for doctors/patients until an administrator loads `/Admin/ManageAppointments`.
16. **`LeaveService.NotifyManagerAsync` is a stub** — `LeaveService.cs:186` only calls `_logger.LogInformation(...)` and returns `Task.CompletedTask`; no `UserNotification` is created.
17. **`LeaveService.RejectLeaveRequestAsync` discards the rejection reason** — the `reason` parameter (`LeaveService.cs:107`) is never assigned to the entity, and `LeaveRequest` has no reason column. The rejection is logged only.
18. **`AppointmentService.CreateAsync` does not validate that the appointment is in the future** (the model attribute only fires on model binding in MVC paths that bind the entity, not on direct service calls).
19. **`PharmacistService.ProcessPrescriptionAsync` is a thin alias** — `PharmacistService.cs:153` simply `return await MarkPrescriptionCompletedAsync(...)` with the comment "ProcessPrescription is essentially the same as MarkPrescriptionCompletedAsync / but we keep it separate for potential future differentiation". Dead API surface.
20. **`ConcurrencyEntity` is defined but unused** — no entity has a `RowVersion`, so the `DbUpdateConcurrencyException` handlers in `DrugService`/`PrescriptionService` can never trigger.
21. **`wwwroot/assets/css/dashboard.css` is orphaned** (no view references it).
22. **jQuery and the validation libraries are loaded twice** in `_LayoutAuth.cshtml` (CDN 3.6.0 + CDN 3.7.1 + local copies).
23. **`Drug.ExpiryStatus` / `DrugService` expiry thresholds are inconsistent**: the model uses 30/60-day buckets, `DrugService.GetAllAsync("valid")` uses only the 30-day threshold, and `expiring-soon` means 30 days while `DrugExpiryShortWarningDays` (60) is never used by the service.
24. **`DoctorLeave` and `LeaveRequest` are two parallel leave concepts** — `RegisterDoctorLeaveAsync` manages `DoctorLeave` while `RejectLeaveRequestAsync`/`ApproveLeaveRequestAsync` manage `LeaveRequest`, and the two are never reconciled. Both are surfaced in the admin UI. `BusinessRules` is applied correctly elsewhere: `IsPrescriptionEligibleForReissue` (`PatientController.cs:1118`) reads `BusinessRules.PrescriptionReissueValidityDays`, and `PatientController.CreateComplaint` reads `BusinessRules.ComplaintAttachmentMaxBytes`.
25. **`DailyReportService.GetFilteredReportAsync` reports `ReportDate = DateTime.UtcNow`** rather than the requested range.
26. **`NoShowRiskService` sends constant clinical features** (`Scholarship`, `Hipertension`, `Diabetes`, `Alcoholism`, `Handcap`, `SMS_received`, `Gender`, `Neighbourhood` are all hard-coded) — the ML model is effectively scoring on age + dates only.
27. **`INoShowRiskService.IsServiceAvailable` / `IsUsingAI` / `LastErrorMessage` are mutable instance state on a scoped service** — they reset per request and are not thread-safe across concurrent calls within one request scope.
28. **`AdminSeeder` creates a scope inside a scope** (`Program.cs` already creates one) — harmless but redundant.
29. **Complaint attachments are written to `wwwroot/uploads/complaints`** — the directory is created at runtime and is served as static content, so uploaded files are publicly retrievable by URL.
30. **`PatientController.ProcessPayment` allows overpayment.** The guard is `amount <= 0 || amount > invoice.TotalAmount` — it compares against the **total**, not the **remaining balance** (`TotalAmount - AmountPaid`). Paying an already-fully-paid invoice again pushes `AmountPaid` above `TotalAmount` (e.g. total 100, paid 100, pay 100 again → `AmountPaid = 200`). The `else if` branch then leaves `PaymentStatus` untouched, so the invoice reports `Paid` while the recorded payment exceeds the amount due.
31. **The payment method is never persisted.** `Invoice` has no payment-method column. `ProcessPayment` only embeds `paymentMethod` inside the free-text `Invoice.Notes` string — and only when `notes` is non-empty. A payment submitted without notes loses the method entirely.

#### Low / cosmetic

32. `IMPLEMENTATION_STATUS.md`, `IMPLEMENTATION_SUMMARY.md` and `TESTING_GUIDE.md` (all under `HCMS4/`) are **UTF-8 without a BOM**. The Arabic content is valid UTF-8, but BOM-less files are mis-detected as ANSI/Windows-1252 by some Windows editors and legacy tools, which renders the Arabic as mojibake. Saving them with a BOM (or as UTF-16LE) removes the ambiguity.
33. `TESTING_GUIDE.md` documents admin credentials (`admin@gmail.com` / `SecureP@ssw0rd123`) that do not match the seeder's defaults — stale documentation.
34. `PrescriptionItem.DrugId` is declared `[Required]` but typed `int?`; `PrescriptionItem.Drug` is non-nullable while the FK is nullable.
35. `PatientController.GetDoctorConsultationFee` and `AdminController.GetDoctorConsultationFee` are separate implementations of the same JSON endpoint.
36. `BaseController`'s error-handling helpers are commented out; controllers duplicate their own try/catch blocks.
37. `HomeController` has no `[Authorize]`, so `Home/Index` (which queries published articles) is public — intentional, but worth noting.
38. `PharmacistController` and `AccountController` do not derive from `BaseController`, so they duplicate the current-patient/doctor/user lookup logic.

---

## 20. Recommended Next Steps

1. **Increment 2 (Lab, L1–L6):**
   - Build `Modules/Lab` on the prepared role (`LabTechnician`), the permissions (`Permissions.Lab.*`) and `StateMachine<TStatus>`.
   - The migration is `Inc2_L_LabRequests`.
2. **Decide #24.** Merge `DoctorLeave` and `LeaveRequest`; this needs a schema change and a new leave flow.
3. **Decide whether a doctor may change their own specialization** on `Account/EditProfile` (it changes the consultation fee).
4. **Replace the cross-module table access** listed in §11 with interface calls as the modules grow.
5. **Rewrite `DrugServiceTests` / `AdminServiceTests`** against the real services (N1) and raise `DrugService` / `LeaveService` coverage.
6. **Move the shared view models into their modules** (optional).
7. **Remove the redundant scope in `AdminSeeder`** (#28) when it is next touched.

---

## Appendix A — File Map

| Path | Contents |
|---|---|
| `src/HCMS4/Program.cs` | Startup (§5) |
| `src/HCMS4/Data/` | `ApplicationDbContext`, `Migrations/` (29 + snapshot) |
| `src/HCMS4/Modules/<Module>/Controllers/` | 22 module controllers (§12) |
| `src/HCMS4/Modules/<Module>/Services/` | Service interfaces and implementations (§11) |
| `src/HCMS4/Modules/<Module>/Models/` | Entities and enums, namespace `HCMS4.Modules.<Module>.Models` |
| `src/HCMS4/Modules/<Module>/Data/` | One `IEntityTypeConfiguration<T>` per entity (26 in total) |
| `src/HCMS4/Modules/<Module>/Views/<Role>/` | 71 Razor views |
| `src/HCMS4/Modules/<Module>/<Module>Module.cs` | `Add<Module>Module()` registration |
| `src/HCMS4/Modules/{Lab,Radiology,Emergency}/README.md` | Skeletons for Increments 2–6 |
| `src/HCMS4/Shared/Common/` | `ServiceResult`, `Pagination`, `BusinessRules`, `ClinicTime`, `HcmsControllerBase`, `ControllerNameAttribute`, `ModuleViewLocationExpander`, `RiskLevelHelper` |
| `src/HCMS4/Shared/Identity/` | `ApplicationUser`, `AccountController`, `AdminSeeder`, `CurrentUserService`, lookups, `Authorization/` |
| `src/HCMS4/Shared/{Audit,Notifications,Reporting,Workflow}/` | Shared services (§6, §11) |
| `src/HCMS4/ViewModels/` | Shared view models (`HCMS4.ViewModels`) |
| `src/HCMS4/Views/` | `Account/`, `Home/`, `Shared/` (layouts, partials) — 15 views |
| `tests/HCMS4.Tests/` | Unit tests, `Integration/` (factory, test login, route inventory), guard tests |
| `ai-service/` | Flask service: `scripts/predict.py`, `scripts/train.py`, `models/*.bin`, `data/*.csv`, `Dockerfile` |
| `docs/` | `REQUIREMENTS.md`, this file, `diagrams/*.puml` |
| `.github/workflows/ci.yml` | CI (§3) |
