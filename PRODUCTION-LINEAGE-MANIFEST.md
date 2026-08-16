# Production Lineage Reconstruction — Baseline Manifest
# استعادة مصدر Backend المنشور على Production / RC — بطاقة الأساس

> Branch/Tag: `production-lineage-20260707`
> Purpose: A permanent, pinned reconstruction of the EXACT backend source currently
> deployed to Production (`reporting_prod`, port 5090) and RC (`reporting_rc`, port 5092),
> which are byte-for-byte identical deployments. This branch is the trustworthy Source
> Baseline for future patches. **Do NOT merge `develop` into this branch.**

---

## 1. Provenance / المنشأ

| Item | Value |
|------|-------|
| Nearest Git commit (functional/deps/API/build-date) | `6fd2253b9d720a1584c04b81f40cc5440c8f3500` — "RC-4 Sales Module baseline" (2026-07-08 00:11:49 +0300) |
| Reconstruction branch base | `6fd2253` (this branch's parent) |
| Reconstruction method | Reliable decompilation of deployed assemblies (ilspycmd 8.2.0) + apply ONLY proven diffs from DLL/PDB. NO DLL editing, NO binary patching, NO __EFMigrationsHistory edit, NO new migration. |
| Deployed artifacts source of truth | `/tmp/prod-lineage-recon-20260707/deployed-dll/` (pulled read-only from RC == Prod) |

### Proven diffs applied to `6fd2253` to reach the deployed state

1. **Removed the entire `FlexiblePositionsPhase1A` feature** — Production/RC were NEVER built with it.
   Deleted Positions entities, service, controller, EF configuration, tests, and the
   `20260620001156_FlexiblePositionsPhase1A` migration + Designer. Cleaned residual
   references in `Program.cs`, `Roles.cs`, `RoleCapabilities.cs`, `Enums.cs`
   (`PositionScopeKind`), `DependencyInjection.cs`, `AppDbContext.cs`,
   `AppDbContextModelSnapshot.cs`, and `RoleMatrixCapabilitiesTests.cs`.
   Verification: the deployed decompiled `Reporting.Infrastructure.dll` contains **26**
   `[Migration]` attributes and **no** FlexiblePositions type — matching this branch exactly.

2. **Renamed 2 migration IDs to match the deployed timestamps** (content proven identical
   by decompiled comparison — the executed operations are byte-equivalent; only the
   `[Migration("id")]` string + filenames differ):
   - `20260622140138_KpiTemplateAssignmentsPhaseT1` → `20260622144900_KpiTemplateAssignmentsPhaseT1`
   - `20260626124527_AddReportViewGrants` → `20260626135944_AddReportViewGrants`

3. **`ScopeResolver.cs`** restored to the pre-Positions form, which was PROVEN functionally
   identical to the deployed decompiled `ScopeResolver` (own/team/department BFS/company
   logic identical). It does NOT reference Positions.

No other source changes. Frontend and all other backend files are byte-identical to `6fd2253`.

---

## 2. Toolchain / سلسلة الأدوات

| Tool | Version |
|------|---------|
| .NET SDK | 8.0.421 (`/Users/ibrahimelbahrawi/.dotnet/sdk`) |
| Target framework | `.NETCoreApp,Version=v8.0` (LTS) |
| Decompiler | ilspycmd 8.2.0 (`DOTNET_ROLL_FORWARD=LatestMajor`) |
| Local Postgres | PostgreSQL 16 (`/opt/homebrew/opt/postgresql@16/bin`) |
| Build env | `export DOTNET_ROOT=/Users/ibrahimelbahrawi/.dotnet && export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"` |

---

## 3. Deployed DLL Manifest / بطاقة التجميعات المنشورة

SHA-256 of the authoritative deployed assemblies (RC == Prod), pulled read-only:

| Assembly | Size (bytes) | SHA-256 |
|----------|-------------:|---------|
| Reporting.Api.dll | 296960 | `e14bbcb5051d46be9454718b85defe761870eca2043326057ab8bfd303bd1578` |
| Reporting.Application.dll | 1169920 | `fba631a959100bb2053853ea8ae48e4392f32614fbaedf43816afbaa8496d0ee` |
| Reporting.Domain.dll | 77824 | `71898eea64c0f66b4e9c78533a3ff6a8ad26bec280085e97dd441715e5ae9adc` |
| Reporting.Infrastructure.dll | 3190272 | `f158634dbaa16ff9eb1c66217fcec51763c6e3036b11f174f12396d7cee7457e` |

> Note: A byte-for-byte SHA-256 match between a locally rebuilt DLL and the deployed DLL is
> NOT expected due to build non-determinism (timestamps, MVID, PDB paths). Per the mandate,
> equivalence is proven **functionally** (API surface, migration lineage, dependencies,
> startup behavior) rather than by hash. These hashes pin the deployed artifacts as evidence.

---

## 4. Migration Lineage (26, in order) — IDENTICAL to deployed DLL

```
20260609142107_InitialIdentity
20260609145345_DomainModel
20260609150255_RefreshTokens
20260615130912_ManagementNotesAndGovernanceLinks
20260615164459_TemplateClassification
20260615221415_ClientProjectDimension
20260617144026_LeaveRequests
20260617184510_HrLeaveRequestRouting
20260618022906_EmailOutbox
20260618191937_ReportTemplateAssignments
20260619121512_EmployeeSelfServiceBalancesAndHrRequests
20260622144900_KpiTemplateAssignmentsPhaseT1        (renamed from 140138; content identical)
20260622180127_LeaveBalanceGuardSnapshot
20260622200553_PermissionShortfallResolution
20260623145426_PayrollImpactReviews
20260624075349_EmployeeServiceFinalDocumentMetadata
20260626012810_AddUserTeamMemberships
20260626135944_AddReportViewGrants                   (renamed from 124527; content identical)
20260627022301_GovernanceWorkspaceItems
20260628022650_GovernanceIndividualEscalations
20260628155250_AddGovernanceActionItems
20260630181557_AddGovernanceItemApplicationScope
20260630195242_AddEmailNotifications
20260702232631_AddEmailControlCenter
20260706092852_AddCourseCatalog
20260706230935_AddServiceCatalog
```

**`20260620001156_FlexiblePositionsPhase1A` is intentionally ABSENT** — it was never in
Production/RC. `diff candidate_migids deployed_migids` = EMPTY (identical set + order).

---

## 5. Baseline Acceptance Gate — Results (all PASS)

| # | Gate item | Result |
|---|-----------|--------|
| 1 | Clean Release build | PASS |
| 2 | Migration IDs from candidate == deployed DLL (26) | PASS — `diff` empty |
| 3 | `has-pending-model-changes` | PASS — "No changes have been made to the model since the last migration." |
| 4 | Startup on disposable clone (from `reporting_rc`) | PASS — no DDL, no unexpected Seeder write; pre/post snapshots identical (history=26, tables=56, templates=41, roles=12, users=35) |
| 5 | `/health` = 200 | PASS |
| 6 | Migrations apply cleanly from EMPTY DB | PASS — 26 in order, no FlexiblePositions, health=200, history=26 |
| 7 | Public API Surface candidate vs deployed Api.dll | PASS — 295 endpoints, `diff` empty, no Positions controller |
| 8 | Assembly dependencies candidate vs deployed | PASS — 68 libraries, all versions match, `.NETCoreApp v8.0` |
| 9 | Full Integration Suite | 1246 passed / 29 failed — the 29 are PROVEN baseline (see §6) |
| 10 | Pending migrations on clone | 0 |

---

## 6. Documented Baseline Failures (29) — NOT reconstruction defects

The 29 failing integration tests are all **Rollup aggregation** tests (28 `ReportsTests` + 1
`ProjectRepeatableGridTests`). They fail because they depend on curated fixture data
(workflow auto-approval / seeded org tree) that exists only in the long-lived shared
`reporting_test`, and is absent in a fresh Testing DB.

**Proof they are environmental, not caused by the reconstruction:** the SAME 29 failures
reproduce IDENTICALLY on the UNMODIFIED reference commit `6fd2253` run against an equivalent
fresh Testing DB. Rollup subset on reference: 29 failed / 35 passed / 64 total. `diff` of
candidate-failures vs reference-failures = EMPTY →
`>>> IDENTICAL BASELINE FAILURE SET (candidate == unmodified reference) <<<`.

The reconstruction diffs (Positions removal, 2 migration renames, ScopeResolver=matches-deployed)
provably do not touch `SubmissionService` / Reports aggregation / `TemplateSeeder`.

---

## 7. Go / No-Go

**GO for Baseline Acceptance.** The reconstructed source on branch `production-lineage-20260707`
is proven equivalent to the deployed Production/RC backend across migration lineage, public API
surface, assembly dependencies, clean startup on a clone, and zero pending migrations. The only
integration failures are pre-existing environmental baseline failures identical on the unmodified
reference commit.

**STOP HERE.** Per mandate, the Project-First Execution Aggregation package (Phase 2, 8 files)
must NOT be added until a NEW explicit approval is granted.
