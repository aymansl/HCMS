# Emergency module (empty skeleton)

The emergency department. No code yet. It is built in **Increment 6**, together with the shared requirements S1, S3 and S4.

## Requirements

| Code | Requirement | Increment |
|---|---|---|
| E1 | Search for an emergency case and verify it | 6 |
| E2 | Register an emergency case and assign a doctor | 6 |
| E3 | Show the available doctors | 6 |
| E4 | Record and show the patient's initial data (symptoms, first examination, condition, address) | 6 |
| E5 | List emergency cases by category (for example women, children) | 6 |
| E6 | Daily emergency report | 6 |

Use cases, flows and acceptance tests (TC-…) are in `docs/REQUIREMENTS.md`. The diagrams are `docs/diagrams/UCD_E.puml` and `UC-E01`…`UC-E06`.

## Already prepared

- **Role:** `EmergencySupervisor`, seeded by `AdminSeeder`.
- **Permission:** `Permissions.Emergency.ManageCases`, mapped in `Shared/Identity/Authorization/RolePermissions.cs`.
- **Emergency cases:** use `Shared/Workflow/StateMachine<TStatus>`. The `StatusTransition` records feed the StatusHistory table and the turnaround dashboard (S4).

## Planned

- **Migration:** `Inc6_E_Emergency_S_Shared`: EmergencyCase, TriageInfo, unified invoice changes (S1), StatusHistory.
- **Folders:** `Controllers/`, `Services/`, `Models/`, `Data/`, `Views/`.
- **Registration:** `EmergencyModule.AddEmergencyModule()`.
- **Doctors:** the list of available doctors (E3) comes from the Staff module through `IStaffService.GetBookableDoctorsAsync`, not from its tables.
