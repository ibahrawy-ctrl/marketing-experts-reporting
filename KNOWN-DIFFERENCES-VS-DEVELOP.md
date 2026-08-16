# Known Differences vs `develop` — and Why NOT to Merge
# الفروق المعروفة مقابل develop — ولماذا لا يُدمَج

> Branch: `production-lineage-20260707`
> `develop` HEAD at reconstruction time:
> `6859ee0d51bef574a4dc4623c015817af325e78c` — "test(clients): add Client 360 foundation
> integration tests (CPW-R1B)" (2026-07-13 01:32:04 +0300).

---

## 1. Why this branch exists

This branch is a **frozen, provable reconstruction** of the backend source currently running
on Production and RC. It exists so that any future production patch is built from a source tree
that is KNOWN to match what is actually deployed — not from `develop`, which has diverged.

## 2. The critical divergence: FlexiblePositions migration

`develop` (and its ancestor `6fd2253`, this branch's parent) **contains** the migration
`20260620001156_FlexiblePositionsPhase1A` and its Positions feature (entities, service,
controller, EF config, tests).

Production and RC were **NEVER** built or migrated with FlexiblePositions. Their deployed
`Reporting.Infrastructure.dll` contains exactly **26** `[Migration]` attributes and **no**
Positions type. Their `__EFMigrationsHistory` has 26 rows and does NOT include
`20260620001156`.

### The danger of merging `develop` migrations into a production build

If a build that INCLUDES `20260620001156_FlexiblePositionsPhase1A` is ever deployed to
Production/RC:

- EF Core auto-migration (`db.Database.MigrateAsync()` on boot) compares `[Migration]` ids
  against `__EFMigrationsHistory`. Since `20260620001156` is absent from the deployed history
  but its timestamp sorts BEFORE many already-applied migrations, EF would attempt to apply it
  **out of order**, executing its `Up()` (CREATE TABLE positions / position_permissions /
  position_scopes / user_positions, etc.) against a schema that never expected it.
- This is an unreviewed, unapproved DDL change on the production database — a direct violation
  of the standing prohibitions (no unapproved schema change, no DDL on real DB).

**Therefore: do NOT merge `develop` into this branch, and do NOT cherry-pick `develop`'s
migration lineage onto a production deployment.** Production patches must be built from THIS
branch and add only NEW, explicitly-approved migrations on top of the 26.

## 3. Migration-ID renames on this branch (cosmetic, content-proven)

Two migrations were renamed to match the deployed timestamp IDs. Decompiled comparison proved
the executed operations are byte-equivalent; only the `[Migration("id")]` string + filename
differ:

| develop / 6fd2253 ID | this branch (deployed) ID | Feature |
|----------------------|---------------------------|---------|
| `20260622140138_KpiTemplateAssignmentsPhaseT1` | `20260622144900_KpiTemplateAssignmentsPhaseT1` | KPI template assignment rules (T1) |
| `20260626124527_AddReportViewGrants` | `20260626135944_AddReportViewGrants` | Report view grants |

## 4. Other differences vs `develop`

- `develop` continues forward with additional features/migrations added after `6fd2253`
  (e.g., CPW-R1B Client 360 tests and any later work). Those are NOT part of the deployed
  Production/RC backend and are intentionally excluded here.
- Frontend on this branch = frontend at `6fd2253` (unchanged by the reconstruction). It is not
  the subject of the baseline; the baseline concerns the deployed backend.

## 5. How to use this branch

- To reconstruct/verify the deployed backend: build THIS branch.
- To make a production patch: branch FROM this tag, add only the minimal approved change +
  (if needed) exactly one new additive migration whose timestamp sorts AFTER
  `20260706230935_AddServiceCatalog`. Never reintroduce `20260620001156`.
- Never `git merge develop` here.
