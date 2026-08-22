# BA DMO — Baseline Verification Record

## Baseline identity

- **SOURCE:** `D:\BA-DMO-RECOVERY`
- **TARGET:** `D:\BA-DMO-CODEX-CLEAN`
- **DATE:** 2026-08-22
- **SOURCE GIT BRANCH:** `u13-wip-transfer`
- **SOURCE HEAD:** `e5d794d37e77554eae51d742077f8b6b23d26262`

> Source branch and HEAD were available at inventory time and are recorded
> here. They reference the source workspace Git history only; that history is
> intentionally **not** carried into the target repository.

## Baseline nature

This is a **clean implementation baseline** — the current BA DMO application
copied faithfully, byte-for-byte, before any pre-design repair work. No fixes,
refactors, or behavior changes were made. Requirements and design authority are
explicitly **not** reconstructed from excluded historical material and will be
added in a separate later commit.

## Copied inventory summary

Copied the complete current application baseline:

- **Root / build files** (4): `BA-DMO.sln`, `global.json`,
  `Directory.Build.props`, `.gitignore` (existing app .gitignore, extended with
  `coverage/`, `.env`, `.env.*` protections only).
- **`src/`** — the four application projects (`BA.Dmo.Domain`,
  `BA.Dmo.Application`, `BA.Dmo.Infrastructure`, `BA.Dmo.Web`) including
  `Properties/`, `wwwroot/`, `Pages/`, and `src/logo.png`.
- **`tests/`** — `BA.Dmo.UnitTests` and `BA.Dmo.IntegrationTests`.
- **`database/`** — `migrations/` (N01–N26, current migration order retained)
  and `consolidated_clean_install.sql`.

**Source files verified for byte identity: 508** (git-tracked application
source under `src/`, `tests/`, `database/` plus the one untracked-but-source
file `src/BA.Dmo.Domain/Modules/Armazem/ArmazemLocationOccupiedException.cs`),
plus the 4 root/build files = **512 files copied**.

## Excluded categories

The following source-workspace content was intentionally **not** copied:

- `.git` (no history preserved)
- Agent tooling: `.kilo/`, `.opencode/`, DeepSeek/Qwen harness files
- `AI-CONTEXT/` (curated AI package)
- Historical / recovery / audit material: `audit/`, `reports/`, `.md` audit and
  checkpoint reports at root, `TestDbConnection/` (diagnostic tool, not in the
  solution), `tools/` (empty)
- Design / mockup packages: `BA-Design/`, `design/`
- Compiled / published output: `freeze-smoke-build/`
- `database/fresh-baseline-v2/` (untracked, emptied `_work` workspace — no files)
- Build artifacts: `bin/`, `obj/`, `TestResults/`, coverage output
- Logs and records: `*.log`, `*.log.err`, `*.binlog`, `msbuild.log`,
  `full_build.log`, `build*.log`/`build*.txt`, `.infra-restore.txt`,
  `.restore-log.txt`, `app-run*.log`
- Runtime HTML/script snapshots and verification scripts: `live-admin*.html`,
  `live-jobon.html`, `*.ps1`, `*.csx`
- Local dev / secret-bearing artifacts: `dbdiag/`, `.tmp-*`,
  `session-cookies.txt`, `start-debug.ps1`, `.dotnet-sdk/`, `.env` (absent)

## Byte / hash verification result

- **Result:** PASS
- Expected source files verified present and SHA-256 byte-identical:
  **508 / 508** (0 missing, 0 mismatches).
- Root/build files verified byte-identical: **4 / 4**.
- Target confirmed free of forbidden content (no `bin/`, `obj/`, logs,
  binlogs, TestResults, coverage, `.git`, agent/AI/design/report material).
- **0 unintended content changes.**

## Secret scan result

- **Result:** PASS — no real secrets found in the target.
- All matches surfaced by the scanner were false positives: connection-string
  *format* documentation with `...` placeholders, and synthetic test fixtures
  (localhost `127.0.0.1` / discard port, `.example` domains) with clearly fake
  values (e.g. `secret-value`, `P@ssw0rd-123`).
- The application reads all runtime credentials from environment variables and
  explicitly documents "No connection string is ever stored in the repository"
  (`src/BA.Dmo.Infrastructure/Persistence/DbConnectionFactory.cs`).
- No `.env` files, service-role keys, anon keys, API keys, bearer tokens,
  private keys, or credentialed connection strings were copied.
- No actual secret values are reproduced in this document.

## Baseline validation (build / test)

Validation was attempted from `D:\BA-DMO-CODEX-CLEAN` with the globally
installed SDK 10.0.400 (matches `global.json`; runtime .NET 10.0.11 present).
The environment is correctly configured and is **not** the cause of any failure.

- **Solution/project discovery:** PASS — 6 projects discovered; all restored
  successfully (packages, including Dapper, Npgsql, xunit, resolved).
- **BUILD RESULT:** **EXISTING CODE FAILURE**
  `BA.Dmo.Domain` and `BA.Dmo.Application` compiled (21 warnings). The build
  failed with **4 genuine compiler errors**:
  - `src/BA.Dmo.Infrastructure/Access/DapperJobOnRepository.cs(17,45)` —
    **CS0535**: `DapperJobOnRepository` does not implement interface member
    `IJobOnRepository.DuplicateAtomicallyAsync(JobOn, JobOnRevision, Guid,
    string, CancellationToken)`.
  - `tests/BA.Dmo.UnitTests/Modules/JobOn/FakeJobOnRepository.cs(177,179)` —
    **CS0200**: read-only `JobOn` properties (`CopiedFromJobOnId`,
    `CreatedAtUtc`, `ArticleReferenceId`) are being assigned.
- **UNIT TEST RESULT:** **NOT RUN — BLOCKED BY EXISTING BUILD FAILURE**
  `BA.Dmo.UnitTests` does not compile (the CS0200 errors above originate in
  the unit-test project itself), so no unit tests could execute.
- **INTEGRATION TEST RESULT:** **NOT RUN — BLOCKED BY EXISTING BUILD FAILURE**
  `BA.Dmo.IntegrationTests` references `BA.Dmo.Web` and `BA.Dmo.Infrastructure`,
  which fail to compile.
- **APP STARTUP SMOKE:** **NOT FEASIBLE** — the application does not compile
  into a runnable assembly as currently checked in.

These failures are **baseline existing defects**, faithfully reproduced, and
were deliberately **not fixed** (the primary failure corresponds to the Job On
duplication / atomic-save work on the stated later repair path).

## Known baseline failures (do not fix here)

The following are recorded **only** as current baseline truth. No code was
changed to address them in this baseline commit.

1. `DapperJobOnRepository` does not implement `IJobOnRepository
   .DuplicateAtomicallyAsync(...)` → **CS0535** (Job On duplication / atomic
   save gap).
2. `FakeJobOnRepository` assigns read-only `JobOn` properties
   (`CopiedFromJobOnId`, `CreatedAtUtc`, `ArticleReferenceId`) → **CS0200**,
   which also blocks the unit-test project from compiling.

These correspond to the pre-design repair items explicitly out of scope for
this baseline (Job On atomic save / duplication). They are expected to be
resolved in a later commit **after** this baseline.