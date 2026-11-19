# Lab module (empty skeleton)

The laboratory department. No code yet. It is built in **Increments 2 and 3**.

## Requirements

| Code | Requirement | Increment |
|---|---|---|
| L1 | Lab technician logs in and out | 2 |
| L2 | View the requests that reach the lab | 2 |
| L3 | Receive a test request from a doctor | 2 |
| L4 | Receive the cancellation of a test request | 2 |
| L5 | Receive a lab appointment booked by a patient | 2 |
| L6 | Manage the test catalogue (add, edit, delete) | 2 |
| L7 | Add a lab note | 3 |
| L8 | Search issued results | 3 |
| L9 | View a patient's test history | 3 |
| L10 | Notify when a result is issued | 3 |
| L11 | Daily lab activity report | 3 |

Use cases, flows and acceptance tests (TC-…) are in `docs/REQUIREMENTS.md`. The diagrams are `docs/diagrams/UCD_L.puml` and `UC-L01`…`UC-L11`.

## Already prepared

- **Role:** `LabTechnician`, seeded by `AdminSeeder`.
- **Permissions:** `Permissions.Lab.ReceiveRequest`, `ManageTests` and `EnterResults`, mapped in `Shared/Identity/Authorization/RolePermissions.cs`.
- **Lab requests:** use `Shared/Workflow/StateMachine<TStatus>`. Allowed transitions only, and each change records the time and the user (turnaround time, S4).

## Planned

- **Migrations:**
  - `Inc2_L_LabRequests`: LabTest, LabRequest, LabRequestItem, LabAppointment.
  - `Inc3_L_LabResults`: LabResult, LabNote, IsCritical.
- **Folders:** `Controllers/`, `Services/`, `Models/`, `Data/` (one `IEntityTypeConfiguration` per entity), `Views/`.
- **Registration:** `LabModule.AddLabModule()`, the same pattern as the other modules.
- **Other modules:** reached only through their interfaces (for example `IPatientLookup` and `INotificationService`).
