# BA DMO — Dapper / Infrastructure Technical Map

> MAP-04 · Transversal technical map · Pure technical inventory + location.
> Scope: `src\BA.Dmo.Infrastructure\` only.

## Navigation Index

- [1. Purpose](#1-purpose)
- [2. Infrastructure Project Structure](#2-infrastructure-project-structure)
- [3. Global Infrastructure Inventory](#3-global-infrastructure-inventory)
- [4. Persistence / Dapper Foundation](#4-persistence--dapper-foundation)
- [5. Repository Implementations (Access / Identity)](#5-repository-implementations-access--identity)
- [6. SQL Inventory](#6-sql-inventory)
- [7. Table / DB Object References](#7-table--db-object-references)
- [8. Connection Infrastructure](#8-connection-infrastructure)
- [9. Transaction Infrastructure](#9-transaction-infrastructure)
- [10. Hydration / Mapping](#10-hydration--mapping)
- [11. JSON / Serialization](#11-json--serialization)
- [12. Supabase / Authentication Adapters](#12-supabase--authentication-adapters)
- [13. Document / PDF Infrastructure](#13-document--pdf-infrastructure)
- [14. File / Storage Infrastructure](#14-file--storage-infrastructure)
- [15. Migration Infrastructure](#15-migration-infrastructure)
- [16. Settings / Options](#16-settings--options)
- [17. Dependency Injection Registration](#17-dependency-injection-registration)
- [18. Classification / Reconciliation Notes](#18-classification--reconciliation-notes)
- [Direct Technical References](#direct-technical-references)
- [Sources Verified](#sources-verified)

---

## 1. Purpose

Pure technical inventory of the Infrastructure project:

- repository implementation classes and their persistence ports;
- SQL statements embedded in Infrastructure;
- connection / transaction / hydration infrastructure;
- Supabase / auth adapters;
- document / PDF renderers and file / image storage adapters;
- migration runner / gateway;
- settings/options and DI-relevant technical facts;
- exact source locations.

No business workflow, Domain interpretation, Application orchestration, Database
schema, or migration chronology is explained here.

---

## 2. Infrastructure Project Structure

Project: `src\BA.Dmo.Infrastructure\BA.Dmo.Infrastructure.csproj`

Project references:
- `..\BA.Dmo.Application\BA.Dmo.Application.csproj`
- `..\BA.Dmo.Domain\BA.Dmo.Domain.csproj`

NuGet / package references (Infrastructure-local):
- `Dapper` 2.1.79
- `Npgsql` 10.0.3

Source folders under `src\BA.Dmo.Infrastructure\`:

| Folder | `.cs` files | Technical role |
|---|---|---|
| `Access\` | 31 | Dapper repositories, context lookups, UoW factories, PDF renderers, file/image provider |
| `Auth\` | 3 | Supabase auth + admin provisioning adapters + settings |
| `Identity\` | 1 | Dapper internal-user repository |
| `Persistence\` | 6 | Db foundation, connection factory, unit of work, type/name mappings |
| `Persistence\Migrations\` | 7 | Migration runner, gateway, discovery, checksum, records, exceptions |
| **Total source `.cs`** | **48** | Excludes `bin\`, `obj\`, and generated `obj\Debug\...` files |

Generated files excluded (not source-controlled) — `obj\Debug\net10.0\`:
`BA.Dmo.Infrastructure.GlobalUsings.g.cs`, `BA.Dmo.Infrastructure.AssemblyInfo.cs`,
`.NETCoreApp,Version=v10.0.AssemblyAttributes.cs`.

SQL-bearing source file count: **26** (files containing embedded SQL statement
strings, incl. `DapperArticleReferenceImageRepository`; excludes the `Db.cs`
wrapper and the PDF/migration-discovery helpers that contain no SQL).

---

## 3. Global Infrastructure Inventory

| Area | Type | Kind | Main Technical Role | File |
|---|---|---|---|---|
| Access | `DapperAdminRepository` | Repository implementation | Admin users/templates/audit persistence | `Access\DapperAdminRepository.cs` |
| Access | `DapperAppSettingsReader` | Repository implementation | Read app_settings value | `Access\DapperAppSettingsReader.cs` |
| Access | `DapperArmazemRepository` | Repository implementation | Armazém stock/movements persistence | `Access\DapperArmazemRepository.cs` |
| Access | `DapperArmazemRepairMovementRepository` | Repository implementation | Repair-cycle armazém movement port | `Access\DapperArmazemRepairMovementRepository.cs` |
| Access | `DapperArticleReferenceImageRepository` | Repository implementation | Master Article/Reference image association + Job On audit facts | `Access\DapperArticleReferenceImageRepository.cs` |
| Access | `DapperBoquilhasRepository` | Repository implementation | Boquilhas lots/traces/movements persistence | `Access\DapperBoquilhasRepository.cs` |
| Access | `DapperControloSheetRepository` | Repository implementation | Controlo sheets/items/events persistence | `Access\DapperControloSheetRepository.cs` |
| Access | `DapperFerramentasRepository` | Repository implementation | Ferramentas references/lotes/pieces/rules | `Access\DapperFerramentasRepository.cs` |
| Access | `DapperHistoriaRepository` | Repository implementation | História transversal read from audit_events | `Access\DapperHistoriaRepository.cs` |
| Access | `DapperJobOnRepository` | Repository implementation | Job On + revision graph persistence | `Access\DapperJobOnRepository.cs` |
| Access | `DapperJobOnUserContextRepository` | Repository implementation | User Job On context upsert/read | `Access\DapperJobOnUserContextRepository.cs` |
| Access | `DapperModuleCatalogMirrorRepository` | Repository implementation | Module catalog mirror read/upsert/delete | `Access\DapperModuleCatalogMirrorRepository.cs` |
| Access | `DapperPegamentoRepository` | Repository implementation | Pegamentos controls/measurements/docs | `Access\DapperPegamentoRepository.cs` |
| Access | `DapperPesoRepository` | Repository implementation | Peso references/lotes/controls/leituras | `Access\DapperPesoRepository.cs` |
| Access | `DapperRepairRepository` | Repository implementation | Reparação externa exits/items/repairers | `Access\DapperRepairRepository.cs` |
| Access | `DapperReparacaoInternaRepository` | Repository implementation | Reparação interna records + scope interna events | `Access\DapperReparacaoInternaRepository.cs` |
| Access | `DapperTampaoRepository` | Repository implementation | Tampões fields/configs/saldos/planos/machines | `Access\DapperTampaoRepository.cs` |
| Access | `DapperJobOnProductionContextLookup` | Context lookup | Read Job On revision production context | `Access\DapperJobOnProductionContextLookup.cs` |
| Access | `DapperJobOnActiveContextLookup` | Context lookup | Read effective Job On active context | `Access\DapperJobOnActiveContextLookup.cs` |
| Access | `DapperJobOnProductionFolderResolver` | Context lookup | Read job_on.production_folder | `Access\DapperJobOnProductionFolderResolver.cs` |
| Access | `DapperControloProductionContextLookup` | Context lookup | Read Controlo production context | `Access\DapperControloProductionContextLookup.cs` |
| Access | `DapperFerramentasRuleLookup` | Context lookup | Read active tool_check_rules | `Access\DapperFerramentasRuleLookup.cs` |
| Access | `DapperFerramentasIdentityLookup` | Context lookup | Read tool reference/lote identity | `Access\DapperFerramentasIdentityLookup.cs` |
| Access | `DapperFerramentasPieceLookup` | Context lookup | Read physical piece identity | `Access\DapperFerramentasPieceLookup.cs` |
| Access | `DapperRepairUnitOfWorkFactory` | UoW factory | Open repair-scope unit of work | `Access\DapperRepairUnitOfWorkFactory.cs` |
| Access | `DapperTampoesUnitOfWorkFactory` | UoW factory | Open Tampões unit of work | `Access\DapperTampoesUnitOfWorkFactory.cs` |
| Access | `DapperBoquilhasUnitOfWorkFactory` | UoW factory | Open Boquilhas unit of work | `Access\DapperBoquilhasUnitOfWorkFactory.cs` |
| Access | `JobOnPdfRenderer` | Renderer | Pure-PDF Job On document renderer | `Access\JobOnPdfRenderer.cs` |
| Access | `PegamentoPdfRenderer` | Renderer | Pure-PDF Pegamentos sheet renderer | `Access\PegamentoPdfRenderer.cs` |
| Access | `PesoSingleFilePdfRenderer` | Renderer | Pure-PDF Peso folha renderer | `Access\PesoSingleFilePdfRenderer.cs` |
| Access | `FileSystemJobOnImageProvider` | Adapter | Filesystem article image resolution | `Access\FileSystemJobOnImageProvider.cs` |
| Auth | `SupabaseAuthAdapter` | Adapter | Supabase GoTrue password sign-in | `Auth\SupabaseAuthAdapter.cs` |
| Auth | `SupabaseAdminProvisioningAdapter` | Adapter | Supabase privileged admin provisioning | `Auth\SupabaseAdminProvisioningAdapter.cs` |
| Auth | `SupabaseSettings` | Options/Settings | Supabase environment variable contract | `Auth\SupabaseSettings.cs` |
| Identity | `DapperInternalUserRepository` | Repository implementation | Internal users + admin template persistence | `Identity\DapperInternalUserRepository.cs` |
| Persistence | `Db` | Service | Static Dapper execution helpers | `Persistence\Db.cs` |
| Persistence | `DbConnectionFactory` | Connection helper | Npgsql connection factory | `Persistence\DbConnectionFactory.cs` |
| Persistence | `LazyDbConnectionFactory` | Connection helper | Lazy wrapper of `DbConnectionFactory` | `Persistence\DbConnectionFactory.cs` |
| Persistence | `DapperUnitOfWork` | Transaction helper | One connection + one transaction scope | `Persistence\DapperUnitOfWork.cs` |
| Persistence | `PersistenceMappings` | Mapper | Dapper default name-mapping conventions | `Persistence\PersistenceMappings.cs` |
| Persistence | `DateTimeOffsetHandler` | Mapper | Dapper type handler timestamptz→DateTimeOffset | `Persistence\DateTimeOffsetHandler.cs` |
| Persistence | `DatabaseConnectionSettings` | Options/Settings | Connection string env-var contract | `Persistence\DatabaseConnectionSettings.cs` |
| Migrations | `MigrationRunner` | Service | Orchestrates full-script migration run | `Persistence\Migrations\MigrationRunner.cs` |
| Migrations | `IMigrationScriptGateway` | Contract | Migration transport port | `Persistence\Migrations\IMigrationScriptGateway.cs` |
| Migrations | `NpgsqlMigrationScriptGateway` | Client | Npgsql migration gateway | `Persistence\Migrations\NpgsqlMigrationScriptGateway.cs` |
| Migrations | `MigrationDiscovery` | Factory | Discovers `N##_name.sql` family | `Persistence\Migrations\MigrationDiscovery.cs` |
| Migrations | `MigrationChecksum` | Helper | SHA-256 of migration file bytes | `Persistence\Migrations\MigrationChecksum.cs` |
| Migrations | `MigrationFile` / `AppliedMigration` | Record | Migration records | `Persistence\Migrations\MigrationFile.cs` |
| Migrations | `MigrationExceptions` | Exception | Migration subsystem exceptions | `Persistence\Migrations\MigrationExceptions.cs` |

---

## 4. Persistence / Dapper Foundation

### `Db` (static)
File: `Persistence\Db.cs`
- Static Dapper execution foundation, parameterized via `CommandDefinition`;
  cancellation flows through `CommandDefinition`.
- `QueryAsync<T>` → `connection.QueryAsync` (returns `IReadOnlyList<T>`).
- `QuerySingleOrDefaultAsync<T>` → `connection.QuerySingleOrDefaultAsync`.
- `ExecuteAsync` → `ExecuteAsync`, returns affected row count.
- `ExecuteScalarAsync<T>` → `ExecuteScalarAsync`.
- Static ctor registers `SqlMapper.AddTypeHandler(new DateTimeOffsetHandler())`.

### `DapperUnitOfWork`
File: `Persistence\DapperUnitOfWork.cs`
- Implements `IDbUnitOfWork`.
- One `IDbConnection` + one `IDbTransaction`; commit-on-success, rollback-on-failure or on dispose-without-commit.
- `Connection`, `Transaction` properties.
- `BeginAsync(IDbConnectionFactory, ct)` — opens connection, `BeginTransaction()`.
- `RunAsync<TResult>(factory, operation, ct)` — managed scope: commits after success, rolls back and rethrows on failure.
- `CommitAsync`, `RollbackAsync`, `DisposeAsync` (async disposal of transaction + connection).

### `PersistenceMappings`
File: `Persistence\PersistenceMappings.cs`
- `Configure()` idempotent: sets `DefaultTypeMap.MatchNamesWithUnderscores = true`.
- `IsConfigured` read-only.

### `DateTimeOffsetHandler`
File: `Persistence\DateTimeOffsetHandler.cs`
- `SqlMapper.TypeHandler<DateTimeOffset>` bridging Npgsql `timestamptz` (returned as `DateTime`) to `DateTimeOffset`.
- `SetValue` binds `value.UtcDateTime`; `Parse` handles `DateTimeOffset` / `DateTime` (unspecified → UTC).

---

## 5. Repository Implementations (Access / Identity)

All parameters are anonymous-object / `DynamicParameters` Dapper bindings over
explicitly enumerated columns. Interface names are the persistence ports each
class implements (interfaces defined in `BA.Dmo.Application`).

### `DapperJobOnRepository`
File: `Access\DapperJobOnRepository.cs`
- Implements `IJobOnRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL:
  - `CreateAsync` — `INSERT INTO job_on (... ) RETURNING job_on_id`.
  - `GetByIdAsync` — `SELECT ... FROM job_on WHERE job_on_id=@Id` + revision load.
  - `GetActiveAsync` — `SELECT ... FROM job_on WHERE machine_code=@MachineCode AND status IN ('planeado','em_fabrico')` with optional from/to.
  - `GetByProductionCodeAsync` — `SELECT ... FROM job_on WHERE production_code=@ProductionCode`.
  - `UpdateLifecycleStateAsync` — `UPDATE job_on SET status=@NewState WHERE job_on_id=@Id`.
  - `InsertRevisionAsync`, `GetRevisionsAsync` — `job_on_revision`.
  - `InsertComponentsAsync` — `job_on_component`.
  - `InsertFieldsAsync` — `job_on_component_field`.
  - `InsertRowsAsync` (CAL rows) — `job_on_component_row`.
  - `InsertVerificationsAsync` — `job_on_verification_occurrence`.
  - `UpdateVerificationStatusAsync` — `UPDATE job_on_verification_occurrence ...`.
  - `GetCurrentRevisionIdAsync` — `SELECT current_revision_id FROM job_on`.
  - `UpdateCurrentRevisionAsync` — `UPDATE job_on SET current_revision_id`.
  - `InsertAuditEventAsync` — `job_on_audit_event`.
  - `InsertImageMutationAsync` — transaction: INSERT revision + UPDATE current + INSERT audit event.
  - `SaveRevisionGraphAsync` — transaction: revision graph + current revision + audit.
  - `DuplicateAtomicallyAsync` — transaction: new job_on + copied revision graph + current + audit; returns new `job_on_id`.
  - `GetHistoricalProductionsAsync` — multi-LEFT-JOIN aggregate over `job_on`/`job_on_component`/`job_on_revision` with `GROUP BY`.
- Transaction behavior: `SaveRevisionGraphAsync`, `InsertImageMutationAsync`, `DuplicateAtomicallyAsync` use `DapperUnitOfWork.RunAsync`.
- Hydration: `HarvestRevisionChildrenAsync` loads component/field/row/verification graph grouped by revision id; `MapField`, `MapComponentRow`, `MapVerificationOccurrence`, `MapRevision`, `MapJobOn`; `ParseComponentFamily` (enum), weight snapshot `SerializeWeight`/`ParseWeight`.

### `DapperPesoRepository`
File: `Access\DapperPesoRepository.cs`
- Implements `IPesoRepository`.
- Constructor: `IDbConnectionFactory`; static `JsonOptions` (CamelCase).
- SQL by method:
  - References: `CreateReferenceAsync`/`GetReferenceByIdAsync`/`GetReferencesAsync`/`GetReferenceByMoldNeckringAsync`/`UpdateReferenceAsync` — `peso_references`.
  - Lots: `CreateLoteAsync`/`GetLoteByIdAsync`/`GetLotesAsync` — `peso_lotes` (arrays column `allowed_lines`).
  - Controls: `CreateControlAsync` — transaction: INSERT `peso_controlos` + per-leitura INSERT `peso_leituras`; `GetControlByIdAsync`/`GetControlsAsync`/`GetApprovedControlsForJobOnAsync` (`LEFT JOIN peso_references`); `UpdateControlAsync` — transaction: UPDATE control + DELETE+INSERT leituras; `DeleteControlAsync` — transaction DELETE.
  - Previous: `GetPreviousApprovedAsync` — `peso_controlos` WHERE approved, ordering by date.
  - Day approvals: `SaveDayApprovalAsync` — `peso_day_approvals` with `ON CONFLICT ... DO UPDATE`; `GetRecordDatesAsync` — `to_char(control_date,...)` distinct by year/month.
  - Settings: `SaveSettingAsync`/`GetSettingAsync` — `peso_settings`.
  - Audit: `InsertAuditEventAsync` — `audit_events` (module 'peso', entity 'peso_controlo').
- JSON: `BuildMeasurementsSnapshot` (JSON serialize of measurements), reading rows store `readings` JSON (`{PesoEmAgua,PesoVidro}`), `ExtractSnapshotAverages`, `DeserializeReadings`.

### `DapperPegamentoRepository`
File: `Access\DapperPegamentoRepository.cs`
- Implements `IPegamentoRepository`.
- Constructor: `IDbConnectionFactory`; static `JsonOptions` (CamelCase).
- Methods + SQL:
  - `CreateAsync`/`GetByIdAsync`/`GetByRevisionAsync`/`GetByJobOnAsync`/`SearchAsync`/`UpdateAsync` — `pegamento_controlos`.
  - `AddMeasurementAsync`/`GetMeasurementsAsync` — `pegamento_medicoes`.
  - `UpsertDocumentAsync`/`GetDocumentAsync` — `pegamento_documentos` (insert with `ON CONFLICT (pegamento_controlo_id) DO UPDATE`).
- Status codec: `ToDbStatus`/`FromDbStatus` (`aberto`/`fechado`).
- Hydration: `PegamentoControlo.Hydrate`, `MapControlRow`, `MapMeasurement` (component key "CM"/"BQ"/"MF").
- JSON: `SerializeJson`, `SerializeToolSnapshot`/`DeserializeToolSnapshot` (`{reference,lot}`), `DeserializeString`.

### `DapperArmazemRepository`
File: `Access\DapperArmazemRepository.cs`
- Implements `IArmazemRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL:
  - Locations: `GetOrCreateLocationAsync` (transaction: INSERT `warehouse_locations` `ON CONFLICT (code) DO NOTHING` + re-select), `GetLocationByCodeAsync`, `GetLocationByIdAsync`.
  - Stock: `GetActiveStockByLocationAsync`, `GetActiveStockByToolIdAsync`, `GetActiveStocksAsync`, `GetStockByLocationAsync`, `GetStockByToolIdAsync` — `warehouse_stock` (`released_at_utc IS NULL` for active).
  - Writes (transaction + explicit locking): `RegisterEntradaAsync` — `SELECT ... FROM warehouse_locations ... FOR UPDATE` then `SELECT ... FROM warehouse_stock ... FOR UPDATE`, collision → `ArmazemLocationOccupiedException`, plus INSERT stock + movement; `RegisterSaidaAsync` — `UPDATE warehouse_stock SET released... WHERE ... released_at_utc IS NULL` + `ConcurrencyGuard` + movement; `ReplaceOccupationAsync` — release + insert + two movements; `CorrectLocationAsync` — release and/or occupy with target-location `FOR UPDATE` occupancy guard + matching movements (all-or-nothing, transaction-scoped).
  - History: `GetMovementHistoryAsync` — `warehouse_movements` JOIN `warehouse_stock`; `ListMovementFactsAsync` — paged movement facts JOIN stock + LEFT JOIN location, `@FromUtc/@ToUtc` window.
  - Audit: `InsertAuditEventAsync` — `audit_events` (module 'armazem', entity 'armazem').
- Codecs: `WarehouseMovementDirectionCodec`.

### `DapperArmazemRepairMovementRepository`
File: `Access\DapperArmazemRepairMovementRepository.cs`
- Implements `IArmazemRepairMovementPort`.
- Constructor: `IDbConnectionFactory`. Participates in caller-provided `IDbUnitOfWork`.
- Methods + SQL:
  - `ConfirmPickupAsync` — find active `warehouse_stock`, `UPDATE warehouse_stock ... RETURNING warehouse_stock_id`, then movement (direction 'out', destination 'reparacao_externa', `repair_exit_id` provenance).
  - `ConfirmReturnAsync` — normalize position code, get/create location, occupancy check, INSERT or reuse `warehouse_stock`, movement (direction 'in').
  - `GetOrCreateLocationAsync` — `warehouse_locations` `ON CONFLICT DO NOTHING` + re-select.
- InsertMovement inserts into `warehouse_movements` with `repair_exit_id`.

### `DapperArticleReferenceImageRepository`
File: `Access\DapperArticleReferenceImageRepository.cs`
- Implements `IArticleReferenceImageRepository` (interface in `BA.Dmo.Application\Modules\JobOn\ArticleReferenceImage.cs`).
- Constructor: `IDbConnectionFactory`.
- Methods + SQL (table `article_reference_images` from N29/N30; key normalized via `ArticleReferenceImageRules.NormalizeReferenceCode`):
  - `GetAsync` — `SELECT reference_code, image_asset_id, updated_by, updated_at_utc FROM article_reference_images WHERE reference_code=@ReferenceCode`.
  - `SetAsync` — `DapperUnitOfWork.RunAsync` transaction: upsert `article_reference_images` (`ON CONFLICT (reference_code) DO UPDATE SET image_asset_id/updated_by/updated_at_utc`) + INSERT `job_on_audit_event` (before/after snapshots as `CAST(... AS jsonb)`).
  - `RemoveAsync` — `DapperUnitOfWork.RunAsync` transaction: `DELETE FROM article_reference_images WHERE reference_code=@ReferenceCode` (exactly-1 row guard → `InvalidOperationException`) + INSERT `job_on_audit_event`.
- Audit: writes to `job_on_audit_event` (N05 columns: job_on_id, job_on_revision_id, event_type, before_snapshot, after_snapshot, actor_id, occurred_at_utc); the audit insert is atomic with the association write.
- Related migrations: N29 (table + RLS + legacy `job_on_revision.image_asset_id` promotion), N30 (`ix_article_reference_images_updated_by`).
- Application consumers: `JobOnService` (set/remove image, `GetAsync` for pre-state) and `FileSystemJobOnImageProvider` (`GetAsync`); registered in `src\BA.Dmo.Web\Program.cs` for `IArticleReferenceImageRepository`.

### `DapperBoquilhasRepository`
File: `Access\DapperBoquilhasRepository.cs`
- Implements `IBoquilhasRepository`.
- Constructor: `IDbConnectionFactory`.
- SQL by table:
  - `bq_lotes`: `GetLoteByIdAsync`, `GetLoteByReferenceBatchAsync`, `ListLotesAsync`, `CountLotesAsync`, `CreateLoteAsync`, `UpdateLoteAsync`, `UpdateLifecycleStateAsync`.
  - `bq_lifecycle_history`: `InsertLifecycleEventAsync`.
  - `bq_traces`: `GetTraceByIdAsync`, `GetActiveTraceForLoteAsync`, `GetLastClosedOrActiveTraceAsync`, `GetTraceForMovementAsync`, `CreateTraceAsync`, `CloseTraceAsync` (status='closed'), `ReopenTraceAsync` (status='active'), `AppendReopenHistoryAsync` (`reopen_history = coalesce(...,'[]'::jsonb) || jsonb_build_object(...)`).
  - `bq_movements`: `InsertMovementAsync`, `ListMovementsForTraceAsync`, `ListMovementsAsync`, `CountMovementsAsync`.
  - `bq_traces.deleted_movements` (void): `VoidMovementAsync` (`deleted_movements || to_jsonb`), `ListVoidedMovementIdsAsync`.
  - `bq_utilisation_readings`: `InsertUtilisationReadingAsync`, `GetUtilisationReadingAsync`.
  - `bq_discrepancies`: `GetOpenDiscrepancyForTraceAsync`, `InsertDiscrepancyAsync`, `UpdateDiscrepancyAsync`, `ListDiscrepanciesAsync`.
  - `repairers` / `repairer_repair_types` / `line_repairer_defaults` (tool_type='BQ'): `ListRepairersAsync` (bulk types load per UD-03), `GetRepairerByIdAsync`, `CreateRepairerAsync`, `UpdateRepairerAsync`, `GetLineRepairerDefaultAsync`, `SetLineRepairerDefaultAsync`.
  - Audit: `InsertAuditEventAsync` — `audit_events` (module 'boquilhas').
- JSON: `ParseGuidJsonArray`, `LikePattern` (LIKE escape for `%`/`_`).
- Codecs: `BqLifecycleStateCodec`, `BqLifecycleEventKindCodec`, `BqTraceStatusCodec`, `BqTracePurposeCodec`, `BqMovementTypeCodec`, `BqUtilisationReadingKindCodec`, `BqDiscrepancyStatusCodec`.

### `DapperFerramentasRepository`
File: `Access\DapperFerramentasRepository.cs`
- Implements `IFerramentasRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL:
  - References: `CreateReferenceAsync`, `GetReferenceByIdAsync`, `GetReferenceByTypeAndCodeAsync`, `UpdateReferenceAsync`, `SearchReferencesAsync` — `tool_references` (search uses `EXISTS` subqueries on `tool_lotes`, `@Line = ANY(l.allowed_lines)`).
  - Lotes: `CreateLoteAsync`, `GetLoteByIdAsync`, `UpdateLoteAsync`, `GetLotesByReferenceAsync`, `LoteExistsInReferenceAsync` — `tool_lotes`.
  - Pieces: `RegisterPieceAsync`, `UpdatePieceAsync`, `GetPiecesByLoteAsync` — `physical_pieces`.
  - Check rules: `AddCheckRuleAsync`, `UpdateCheckRuleAsync`, `ToggleCheckRuleActiveAsync`, `DeleteCheckRuleAsync` (soft-deactivate `active=FALSE`), `CopyCheckRuleAsync`, `GetCheckRulesByLoteAsync`, `GetCheckRuleByIdAsync` — `tool_check_rules`.
  - Occurrences: `GetOccurrencesByRuleAsync` — `tool_check_occurrences`.
  - Atomic multi-write: `CreateReferenceWithFirstLoteAsync` (transaction INSERT references + lotes).
  - Utilisation: `RecordUtilisationReadingAsync`, `ListUtilisationReadingsAsync` — `tool_usage_records`.
  - Audit: `InsertAuditEventAsync` — `audit_events` (module 'ferramentas', entity 'ferramenta').
- Codecs: `FerramentasToolTypeCodec`, `FerramentasCheckFrequencyCodec`, `ToolConditionCodec`.

### `DapperRepairRepository`
File: `Access\DapperRepairRepository.cs`
- Implements `IRepairRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL (single-table self-managed connection; coordinated writes via `IDbUnitOfWork`):
  - Exits: `CreateExitAsync`, `GetExitByIdAsync`, `ListExitsAsync` — `repair_exits`; `GetExitItemsAsync` — `repair_exit_items`; `ExistsItemInOpenExitAsync` — `EXISTS` on items joined exits with open statuses.
  - Items: `AddItemAsync`, `GetItemByIdAsync`, `DeleteItemAsync` — `repair_exit_items`.
  - Coordinated (share `IDbUnitOfWork`): `ConfirmItemPickedAsync`, `ConfirmItemReturnedAsync`, `UpdateExitStatusAsync` — `repair_exit_items`/`repair_exits`; `InsertRepairEventAsync` — `repair_events` scope 'externa'.
  - Repairers: `CreateRepairerAsync`, `UpdateRepairerAsync`, `DeactivateRepairerAsync`, `GetRepairerByIdAsync`, `ListRepairersAsync` — `repairers`; `UpsertLineDefaultAsync`, `ListLineDefaultsAsync` — `line_repairer_defaults`; `SetRepairerRepairTypesAsync`, `ListRepairerRepairTypesAsync` — `repairer_repair_types`.
  - Audit: `InsertAuditEventAsync` — `audit_events` (module 'reparacao_externa', entity 'reparacao_externa').
- Codecs: `RepairTypeCodec`, `RepairExitStatusCodec`; JSON `RepairerSnapshot` deserialize in `MapExit`.

### `DapperReparacaoInternaRepository`
File: `Access\DapperReparacaoInternaRepository.cs`
- Implements `IReparacaoInternaRepository`.
- Constructor: `IDbConnectionFactory`. Coordinated writes via `IDbUnitOfWork`.
- Methods + SQL:
  - `InsertAsync`, `GetByIdAsync`, `GetChainRootAsync` (recursive CTE `WITH RECURSIVE` over `internal_repair_records`), `GetChainAsync`, `ListAsync` (`SELECT DISTINCT ON (root_id)`, `COALESCE(correction_of_id, id) AS root_id`) — `internal_repair_records`.
  - `InsertRepairEventAsync` — `repair_events` scope 'interna'.
  - `InsertAuditEventAsync` — `audit_events` (module 'reparacao_interna').
- Codec: `InternalRepairToolTypeCodec`.

### `DapperTampaoRepository`
File: `Access\DapperTampaoRepository.cs`
- Implements `ITampaoRepository`.
- Constructor: `IDbConnectionFactory`. Atomic multi-row writes via `IDbUnitOfWork`.
- Methods + SQL:
  - Fields/values: `ListFieldDefsAsync`, `CreateFieldDefAsync`, `UpdateFieldDefAsync` — `tampao_field_defs`; `ListFieldValuesAsync`, `CreateFieldValueAsync`, `UpdateFieldValueAsync` — `tampao_field_values`.
  - Configs/saldos: `FindConfigurationByKeyAsync`, `GetConfigurationByIdAsync`, `ListConfigurationsAsync`, `CreateConfigurationAsync` — `tampao_configurations`; `GetSaldoByConfigurationAsync` — `tampao_saldos`; `GetSaldoInTransactionAsync` — `SELECT ... FROM tampao_saldos ... FOR UPDATE` (explicit row lock); `SetSaldoAsync` — upsert `tampao_saldos` (`gen_random_uuid()`, `ON CONFLICT (tampao_configuration_id) DO UPDATE`).
  - Movements: `InsertMovementAsync` — `tampao_movements`; `ListMovementsAsync`.
  - Planos: `CreatePlanoAsync`, `GetPlanoByIdAsync`, `CancelPlanoAsync`, `ListPlanosAsync` — `tampao_planos` (`NULLS LAST` order).
  - Machines/notes: `GetMachinesByConfigurationAsync`, `ReplaceConfigurationMachinesAsync`, `InsertMachineEventAsync`, `ListMachineEventsAsync` — `tampao_configuration_machines`/`tampao_configuration_machine_event`; `AddConfigurationNoteAsync`, `ListConfigurationNotesAsync` — `tampao_configuration_notes`; `ListConfigurationsByMachineAsync` — `tampao_configurations` JOIN machines.
  - Audit: `InsertAuditEventAsync` — `audit_events` (module 'tampoes').
- Codecs: `TampaoMovementTypeCodec`; JSON `ParseValues` (values_json to sorted decimal dict); `MapConfiguration`.

### `DapperControloSheetRepository`
File: `Access\DapperControloSheetRepository.cs`
- Implements `IControloSheetRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL:
  - `InsertAsync` (via `IDbUnitOfWork`) — INSERT `controlo_sheets` + items.
  - `GetByIdAsync`, `GetForProductionAsync`, `ListByProductionAsync`, `ListAsync` — `controlo_sheets`.
  - `UpdateAsync` (via `IDbUnitOfWork`) — header UPDATE + item fact UPDATE (+ clear results).
  - `InsertEventAsync` (via `IDbUnitOfWork`) — `controlo_sheet_events`.
  - Private: `InsertItemsAsync` — `controlo_sheet_items`; `LoadItemsAndEventsAsync`, `MapHeader`.
- Codec: `ControloFolhaStateCodec` (+ `FromStorageDecision`).

### `DapperAdminRepository`
File: `Access\DapperAdminRepository.cs`
- Implements `IAdminRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL:
  - Users: `ListUsersAsync` (interpolated `$"SELECT {UserColumns}..."` + optional ILIKE search; `UserColumns` includes an ARRAY subquery `TemplateIds` over `internal_user_access_templates`), `GetUserAsync`, `AuthUserIdAlreadyRegisteredAsync`, `CreateInternalUserAsync` (`WITH inserted AS (INSERT internal_users ... ON CONFLICT (actor_id) DO NOTHING RETURNING actor_id) INSERT INTO internal_user_access_templates ... ON CONFLICT (actor_id, template_id) DO NOTHING`), `UpdateUserAsync` (optimistic via `updated_at_utc = @ExpectedUpdatedAt` + `ConcurrencyGuard`), `ChangeUserTemplateAsync` → `ReplaceUserAccessTemplatesAsync` (transaction: optimistic `UPDATE internal_users SET template_id=@PrimaryTemplateId` + `DELETE` + INSERT junction rows via `unnest(@TemplateIds::text[])` + admins-count guard), `SetUserActiveAsync` (guarded + admins count), `SetUserModulesOverrideAsync` (N26 `modules_override` guarded write, NO lockout guard — override grants do not feed the admin count) — `internal_users`.
  - Templates: `ListTemplatesAsync`, `GetTemplateAsync`, `CreateTemplateAsync`, `UpdateTemplateAsync` (guarded + admins count) — `access_templates`.
  - Admin count / self-lockout: `CountActiveAdminsAsync` / `CountActiveAdminsOnAsync` — `SELECT COUNT(DISTINCT u.actor_id) FROM internal_users u JOIN internal_user_access_templates ut JOIN access_templates t ... WHERE u.active AND u.profile_title='Admin' AND t.active AND t.modules @> @AdminGrantPattern::jsonb` (+ optional `excludeActorId`). Guarded writes run in a `DapperUnitOfWork.RunAsync` that performs write + admins-count + rollback on zero (inner `LockoutViolationException` → false).
  - Audit: `InsertAuditEventAsync` — `audit_events` (module 'admin'); `QueryAuditAsync` — dynamic WHERE via `DynamicParameters`, COUNT + paged SELECT `ORDER BY occurred_at_utc DESC LIMIT ... OFFSET ...`.
- Literal JSON grant pattern: `[{"moduleId":"admin"}]`.
- Schema-gate: catches `PostgresException` `SQLSTATE 42703` (undefined column, N26 `modules_override` missing) → `SchemaMigrationRequiredException`.
- Related migrations: N01, N26 (modules_override), N27 (junction), N31 (single effective template + profiles).

### `DapperInternalUserRepository`
File: `Identity\DapperInternalUserRepository.cs`
- Implements `IInternalUserRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL:
  - `FindByAuthUserIdAsync` — `SELECT u.*, t.* FROM internal_users u LEFT JOIN internal_user_access_templates ut ON ut.actor_id = u.actor_id LEFT JOIN access_templates t ON t.template_id = ut.template_id WHERE u.auth_user_id = @AuthUserId` (explicit count; multiple distinct actor ids → `AmbiguousIdentityException`; templates collected via the N27 junction).
  - `AdminExistsAsync` — `SELECT 1 ... FROM internal_users u JOIN internal_user_access_templates ut ... JOIN access_templates t ... WHERE u.active AND u.profile_title = 'Admin' AND t.active AND t.modules @> @AdminGrantPattern::jsonb LIMIT 1`.
  - `CreateBootstrapAdminAsync` — `DapperUnitOfWork.RunAsync` transaction (4 inserts): INSERT `access_templates` (`ON CONFLICT (template_id) DO NOTHING`) + INSERT `internal_users` (`ON CONFLICT (actor_id) DO NOTHING`) + INSERT `internal_user_access_templates` (`ON CONFLICT (actor_id, template_id) DO NOTHING`) + INSERT `audit_events` (module 'admin', action 'bootstrap_admin', entity 'internal_user').
- Literal JSON grant pattern: `[{"moduleId":"admin"}]`.
- Related migrations: N01 identity tables, N27 `internal_user_access_templates` (junction), N31 unique single-template constraint (`ux_internal_user_access_templates_actor`) + `access_template_profiles` (profile kept in sync on `internal_users.profile_title`; the class reads `profile_title`, not the profile table — see Classification Notes §18).

### `DapperModuleCatalogMirrorRepository`
File: `Access\DapperModuleCatalogMirrorRepository.cs`
- Implements `IModuleCatalogMirrorRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL: `GetAllAsync` — `SELECT ... FROM module_catalog_mirror ORDER BY`; `UpsertAllAsync` — `DapperUnitOfWork.RunAsync` transaction: DELETE stale (`module_catalog_mirror`) + per-row UPSERT (`ON CONFLICT (module_id) DO UPDATE`).

### `DapperJobOnUserContextRepository`
File: `Access\DapperJobOnUserContextRepository.cs`
- Implements `IJobOnUserContextRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL: `SetCurrentAsync` — UPSERT `jobon_user_current` (`ON CONFLICT (actor_id) DO UPDATE`, `now()`); `GetCurrentAsync` — `SELECT ... FROM jobon_user_current`.

### `DapperHistoriaRepository`
File: `Access\DapperHistoriaRepository.cs`
- Implements `IHistoriaRepository`.
- Constructor: `IDbConnectionFactory`.
- Methods + SQL (read-only over `audit_events`):
  - `QueryAsync` — group-keys COUNT (`COUNT(DISTINCT entity_type||'|'||entity_id)`), paged group keys (`MAX(occurred_at_utc)`), then `WHERE entity_type||'|'||entity_id = ANY(@GroupKeys)`.
  - `QueryFlatAsync` — flat `SELECT {RowColumns} ... ORDER BY occurred_at_utc DESC LIMIT ... OFFSET ...`.
  - `BuildWhere` — module visibility (`module_id = ANY(@VisibleModules)`) + filter predicates (ILIKE / equality) via `DynamicParameters`.
- Row type: `HistoriaEntryRow`, `PagedGroupKey` (private record).

### `DapperAppSettingsReader`
File: `Access\DapperAppSettingsReader.cs`
- Implements `IAppSettingsReader`.
- Constructor: `IDbConnectionFactory`.
- Method + SQL: `GetOutputRootAsync` — `SELECT setting_value FROM app_settings WHERE setting_key=@SettingKey` (key `main_documents_output_root`); JSON-parse of value.

### Context / resolver lookups (read-only Dapper ports)

- `DapperJobOnProductionContextLookup` — `IJobOnProductionContextLookup`; SQL on `job_on_revision` (3 queries) + snapshot JSON extraction (production_code, machine_code, reference). `Access\DapperJobOnProductionContextLookup.cs`.
- `DapperJobOnActiveContextLookup` — `IJobOnActiveContextLookup`; SQL on `job_on_revision` + `job_on_component` (lot ids by family); uses `IJobOnRepository` + `ReparacaoInternaProductionProjection`. `Access\DapperJobOnActiveContextLookup.cs`.
- `DapperJobOnProductionFolderResolver` — `IJobOnProductionFolderResolver`; `SELECT production_folder FROM job_on WHERE job_on_id=@JobOnId`. `Access\DapperJobOnProductionFolderResolver.cs`.
- `DapperControloProductionContextLookup` — `IControloProductionContextLookup`; SQL on `job_on_revision` + `job_on_component`; uses `IJobOnRepository`. `Access\DapperControloProductionContextLookup.cs`.
- `DapperFerramentasRuleLookup` — `IFerramentasRuleLookup`; `SELECT tool_check_rule_id, rule_text, frequency FROM tool_check_rules WHERE ... active=TRUE` + frequency mapping. `Access\DapperFerramentasRuleLookup.cs`.
- `DapperFerramentasIdentityLookup` — `IFerramentasIdentityLookup`; joins `tool_references`/`tool_lotes` (ILIKE search). `Access\DapperFerramentasIdentityLookup.cs`.
- `DapperFerramentasPieceLookup` — `IFerramentasPieceLookup`; joins `physical_pieces`/`tool_lotes`/`tool_references`. `Access\DapperFerramentasPieceLookup.cs`.

### Unit-of-work factories

- `DapperRepairUnitOfWorkFactory` — `IRepairUnitOfWorkFactory`; `DapperUnitOfWork.BeginAsync`. `Access\DapperRepairUnitOfWorkFactory.cs`.
- `DapperTampoesUnitOfWorkFactory` — `ITampoesUnitOfWorkFactory`; `DapperUnitOfWork.BeginAsync`. `Access\DapperTampoesUnitOfWorkFactory.cs`.
- `DapperBoquilhasUnitOfWorkFactory` — `IBoquilhasUnitOfWorkFactory`; `DapperUnitOfWork.BeginAsync`. `Access\DapperBoquilhasUnitOfWorkFactory.cs`.

---

## 6. SQL Inventory

SQL inventory across the 25 SQL-bearing Infrastructure files; grouped by pattern.

**Dapper parameterised statements via `Db` helpers** (all repositories): explicit
`SELECT ... FROM <table>` with quoted `@Param` bindings; `INSERT INTO ... VALUES (@...)`.

**INSERT ... RETURNING** (ids): `DapperJobOnRepository` — `job_on` (`RETURNING job_on_id`); `DapperArmazemRepairMovementRepository` — `warehouse_stock` (release `RETURNING warehouse_stock_id`).

**UPDATE guarded writes** (optimistic concurrency via `updated_at_utc = @ExpectedUpdatedAt`): `DapperAdminRepository` (users, template) with `ConcurrencyGuard.EnsureSingleRowUpdated`.

**ON CONFLICT / UPSERT**:
- `DapperAdminRepository`, `DapperInternalUserRepository` — `INSERT ... ON CONFLICT (actor_id) DO NOTHING` (internal_users) + `ON CONFLICT (actor_id, template_id) DO NOTHING` (internal_user_access_templates junction).
- `DapperModuleCatalogMirrorRepository`, `DapperPesoRepository` (day approvals, settings), `DapperTampaoRepository` (saldos), `DapperJobOnUserContextRepository` (context), `DapperPegamentoRepository` (documents), `DapperRepairRepository` (line defaults), `DapperArticleReferenceImageRepository` (article_reference_images) — `ON CONFLICT ... DO UPDATE SET ...`.

**Explicit row locking (`FOR UPDATE`)**: `DapperArmazemRepository` (warehouse_locations, warehouse_stock occupation guards), `DapperTampaoRepository` (`GetSaldoInTransactionAsync`).

**JSONB operators / casts**:
- `@>` containment: `DapperAdminRepository` / `DapperInternalUserRepository` — `t.modules @> @AdminGrantPattern::jsonb`.
- `::jsonb` casts on module/override columns.
- `coalesce(...,'[]'::jsonb) || jsonb_build_object(...)`: `DapperBoquilhasRepository` (reopen history), `|| to_jsonb(...)` (voided movements).
- `::text` casts on jsonb columns (module/override snapshots).

**Arrays**:
- Paging: `LIMIT @PageSize OFFSET @Offset` (Audit, História, Boquilhas).
- `ANY(@...)`: revision ids, component ids, visible modules, group keys, allowed_lines membership.
- text[] columns (`allowed_lines`).

**Recursive CTE**: `DapperReparacaoInternaRepository` — `GetChainRootAsync` (`WITH RECURSIVE root AS (...)`).

**DISTINCT ON**: `DapperReparacaoInternaRepository` — `ListAsync`.

**Functions called inline**: `now()`, `EXTRACT(YEAR FROM now())`, `gen_random_uuid()`, `to_char(...)`, `COUNT(DISTINCT ...)`, `COALESCE`, `ORDER BY ... NULLS LAST`.

**LIKE / ILIKE** with boundary `%...%`: Admin, Ferramentas lookups, Boquilhas (`LikePattern` escape), Peso, Pegamentos search.

**Migration SQL** (`NpgsqlMigrationScriptGateway`): `CREATE TABLE IF NOT EXISTS schema_migrations (...)`, `SELECT ... FROM schema_migrations`, `INSERT INTO schema_migrations (...)`.

---

## 7. Table / DB Object References

Reverse navigation — table names referenced literally in Infrastructure SQL.

| Table / DB Object | Infrastructure Class | Methods |
|---|---|---|
| `access_templates` | `DapperInternalUserRepository` | `FindByAuthUserIdAsync`, `AdminExistsAsync`, `CreateBootstrapAdminAsync` |
| `access_templates` | `DapperAdminRepository` | `ListTemplatesAsync`, `GetTemplateAsync`, `CreateTemplateAsync`, `UpdateTemplateAsync`, `CountActiveAdminsOnAsync` |
| `app_settings` | `DapperAppSettingsReader` | `GetOutputRootAsync` |
| `article_reference_images` | `DapperArticleReferenceImageRepository` | `GetAsync`, `SetAsync`, `RemoveAsync` |
| `audit_events` | `DapperInternalUserRepository`, `DapperAdminRepository`, `DapperArmazemRepository`, `DapperPesoRepository`, `DapperRepairRepository`, `DapperFerramentasRepository`, `DapperReparacaoInternaRepository`, `DapperTampaoRepository`, `DapperBoquilhasRepository`, `DapperHistoriaRepository` | audit inserts / Historia reads |
| `bq_*` (lotes, traces, movements, lifecycle_history, utilisation_readings, discrepancies) | `DapperBoquilhasRepository` | all BQ methods |
| `controlo_sheets` / `controlo_sheet_items` / `controlo_sheet_events` | `DapperControloSheetRepository` | all sheet methods |
| `internal_repair_records` | `DapperReparacaoInternaRepository` | `InsertAsync`, `GetByIdAsync`, `GetChainRootAsync`, `GetChainAsync`, `ListAsync` |
| `internal_users` | `DapperInternalUserRepository`, `DapperAdminRepository` | user queries / writes |
| `internal_user_access_templates` | `DapperInternalUserRepository`, `DapperAdminRepository` | N27 junction: identity lookup / template assignment / admin count |
| `job_on` | `DapperJobOnRepository`, `DapperJobOnProductionFolderResolver` | job on CRUD / folder |
| `job_on_revision` | `DapperJobOnRepository`, `DapperJobOnProductionContextLookup`, `DapperJobOnActiveContextLookup`, `DapperControloProductionContextLookup` | revision graph / snapshots |
| `job_on_component` | `DapperJobOnRepository`, context lookups | component rows / lots |
| `job_on_component_field` / `job_on_component_row` / `job_on_verification_occurrence` | `DapperJobOnRepository` | namespaced child nodes |
| `job_on_audit_event` | `DapperJobOnRepository`, `DapperArticleReferenceImageRepository` | `InsertAuditEventAsync`, graph writes / article-image association audit facts |
| `jobon_user_current` | `DapperJobOnUserContextRepository` | `SetCurrentAsync`, `GetCurrentAsync` |
| `line_repairer_defaults` | `DapperRepairRepository`, `DapperBoquilhasRepository` | line defaults |
| `module_catalog_mirror` | `DapperModuleCatalogMirrorRepository` | `GetAllAsync`, `UpsertAllAsync` |
| `pegamento_controlos` / `pegamento_medicoes` / `pegamento_documentos` | `DapperPegamentoRepository` | all pegamento methods |
| `peso_*` (references, lotes, controlos, leituras, day_approvals, settings) | `DapperPesoRepository` | all peso methods |
| `physical_pieces` | `DapperFerramentasRepository`, `DapperFerramentasPieceLookup` | pieces |
| `repair_exits` / `repair_exit_items` / `repair_events` | `DapperRepairRepository`, `DapperReparacaoInternaRepository` | exits / items / events |
| `repairers` / `repairer_repair_types` | `DapperRepairRepository`, `DapperBoquilhasRepository` | repairer vocabulary |
| `schema_migrations` | `NpgsqlMigrationScriptGateway` | `EnsureTrackingTableAsync`, `GetAppliedAsync`, `RecordAppliedAsync` |
| `tampao_*` (field_defs, field_values, configurations, saldos, movements, planos, configuration_machines, configuration_machine_event, configuration_notes) | `DapperTampaoRepository` | all tampão methods |
| `tool_references` / `tool_lotes` / `tool_check_rules` / `tool_check_occurrences` / `tool_usage_records` | `DapperFerramentasRepository`, `DapperFerramentasIdentityLookup` | ferramentas |
| `warehouse_locations` / `warehouse_stock` / `warehouse_movements` | `DapperArmazemRepository`, `DapperArmazemRepairMovementRepository` | stock / movements |

---

## 8. Connection Infrastructure

### `DbConnectionFactory`
File: `Persistence\DbConnectionFactory.cs`
- Constructor validates connection string: empty → `DatabaseConnectionException`; `postgres://`/`postgresql://` URI rejected; non-URI parsed via `NpgsqlConnectionStringBuilder`.
- `FromEnvironment(Func<string,string?> environment)` — resolves via `DatabaseConnectionSettings.ResolveConnectionString`.
- `ConnectionString` property.
- `OpenConnectionAsync` — `new NpgsqlConnection(...)`, `OpenAsync`, on failure dispose + `DatabaseConnectionException`.

### `LazyDbConnectionFactory`
Same file (`Persistence\DbConnectionFactory.cs`)
- Implements `IDbConnectionFactory`; lazily builds `DbConnectionFactory` once (guarded by `SemaphoreSlim`), then delegates `OpenConnectionAsync`.

### `DatabaseConnectionSettings`
File: `Persistence\DatabaseConnectionSettings.cs`
- Constants: `ConnectionStringVariable = "BA_DMO_DB_CONNECTION_STRING"`, `FallbackConnectionStringVariable = "DATABASE_URL"`.
- `ResolveConnectionString(environment)` — primary then fallback.
- `DatabaseConnectionException` — safe diagnostic exception (no connection string echoed).

### Disposal pattern
All repositories dispose connections in `finally` via a static `DisposeAsync(IDbConnection)` that prefers `IAsyncDisposable`; `DapperUnitOfWork.RunAsync`/`BeginAsync` handle async disposal of connection + transaction.

---

## 9. Transaction Infrastructure

### Reusable transaction mechanism
`DapperUnitOfWork` (single connection + single `IDbTransaction`), used everywhere a multi-statement atomic write is needed. `IDbUnitOfWork` (Application contract) exposes `Connection`/`Transaction`; coordinated repos take `IDbUnitOfWork` and run their writes against `uow.Connection` + `uow.Transaction`.

### Unit-of-work factories (open a fresh `DapperUnitOfWork`)
`DapperRepairUnitOfWorkFactory.BeginAsync`, `DapperTampoesUnitOfWorkFactory.BeginAsync`, `DapperBoquilhasUnitOfWorkFactory.BeginAsync`.

### Repository methods that open/use transactions (`DapperUnitOfWork.RunAsync`)
- `DapperJobOnRepository` — `SaveRevisionGraphAsync`, `InsertImageMutationAsync`, `DuplicateAtomicallyAsync`.
- `DapperPesoRepository` — `CreateControlAsync`, `UpdateControlAsync`, `DeleteControlAsync`.
- `DapperArmazemRepository` — `GetOrCreateLocationAsync`, `RegisterEntradaAsync`, `RegisterSaidaAsync`, `ReplaceOccupationAsync`.
- `DapperFerramentasRepository` — `CreateReferenceWithFirstLoteAsync`.
- `DapperInternalUserRepository` — `CreateBootstrapAdminAsync` (template + user + audit).
- `DapperAdminRepository` — `GuardedUserWriteAsync`, `UpdateTemplateAsync` (optimistic write + admins-count, rollback on zero).
- `DapperModuleCatalogMirrorRepository` — `UpsertAllAsync` (delete + upserts).
- `DapperArticleReferenceImageRepository` — `SetAsync`, `RemoveAsync` (association write + audit fact atomic).

### Repository methods participating in a caller-provided `IDbUnitOfWork` (transaction shared with callers / coordinated writes)
- `DapperRepairRepository` — `ConfirmItemPickedAsync`, `ConfirmItemReturnedAsync`, `UpdateExitStatusAsync`, `InsertRepairEventAsync`.
- `DapperArmazemRepairMovementRepository` — `ConfirmPickupAsync`, `ConfirmReturnAsync`, `InsertMovementAsync`, `GetOrCreateLocationAsync`.
- `DapperReparacaoInternaRepository` — `InsertAsync`, `InsertRepairEventAsync`, `InsertAuditEventAsync`.
- `DapperTampaoRepository` — `CreateConfigurationAsync`, `GetSaldoInTransactionAsync` (FOR UPDATE), `SetSaldoAsync`, `InsertMovementAsync`, `CancelPlanoAsync`, `ReplaceConfigurationMachinesAsync`, `InsertMachineEventAsync`, `AddConfigurationNoteAsync`, `InsertAuditEventAsync`.
- `DapperBoquilhasRepository` — lot/trace/movement/discrepancy/lifecycle/audit writes.
- `DapperControloSheetRepository` — `InsertAsync`, `UpdateAsync`, `InsertEventAsync` (+ private helpers).

### Transaction type
All are Npgsql/ADO.NET `IDbTransaction`/`DbTransaction` via `connection.BeginTransaction()`; the migration gateway uses `NpgsqlConnection.BeginTransactionAsync` (one transaction per migration script).

---

## 10. Hydration / Mapping

Mechanisms used across Infrastructure repositories:

- **Private row records / mapping helpers**: `HydratedRevisionChildren` (file-scoped internal holder, `DapperJobOnRepository`), `PagedGroupKey` (private record, `DapperHistoriaRepository`), `RevisionContext` (private record, `DapperJobOnActiveContextLookup`).
- **Service-backed hydration**: `PegamentoControlo.Hydrate(...)` (Domain factory) in `DapperPegamentoRepository`; `JobOnEntity`/`JobOnRevision` assignment + `FromRow` in `DapperJobOnRepository`.
- **Dictionary grouping**: revision child graph grouped by revision id / component id (`DapperJobOnRepository.GetHydratedRevisionContent`); nominal-by-family and snapshot-by-family dictionaries (`DapperJobOnProductionContextLookup`); types grouped by repairer id (`DapperBoquilhasRepository`).
- **Multi-query (per-entity) loading**: sheet header + items + events (`DapperControloSheetRepository.LoadItemsAndEventsAsync`); control header + per-control leituras soaks (`DapperPesoRepository`).
- **Enum storage codecs**: `*Codec.ToStorage` / `*Codec.FromStorage` / `.Parse` converting between C# enums and DB text values across modules.
- **Type bridging**: `DateTimeOffsetHandler` (timestamptz→DateTimeOffset); `.ToDateTimeOffset()` on `DateTime`; `ToDateOnly(...)` (Repair, Tampão); explicit nullable `as string` / `as Guid?` casts on `dynamic` rows; `DBNull` → null handling.
- **Direction-specific bypasses**: `_ = componentRow...`, `_ = leituraId` accumulator with explicit row-count checks.

---

## 11. JSON / Serialization

- `System.Text.Json` (`JsonSerializer`, `JsonDocument`, `JsonElement`) used across adapters/repositories.
- `SupabaseAuthAdapter` — `JsonContent.Create`, `ReadFromJsonAsync<...>`, `JsonDocument.Parse` for GoTrue error body; private `SignInResponse`/`UserPayload`.
- `SupabaseAdminProvisioningAdapter` — `JsonContent.Create` request bodies; `ReadFromJsonAsync` for `UserPayload`/`UserListing`.
- `DapperPesoRepository` — `JsonOptions` (CamelCase); `readings` column serialized as `{PesoEmAgua,PesoVidro}`; `BuildMeasurementsSnapshot`; `ExtractSnapshotAverages`, `DeserializeReadings`.
- `DapperPegamentoRepository` — `JsonOptions`; `reference_snapshot`, tool snapshots `SerializeToolSnapshot`/`DeserializeToolSnapshot`; `DeserializeString`.
- `DapperJobOnRepository` — weight snapshot `SerializeWeight` (`{value}`) / `ParseWeight`; revision snapshot JSON texts.
- Context lookups — JSON snapshot extraction (`ExtractStringFromSnapshot`, `ExtractReferenceFromSnapshot`, `ExtractString`, `ExtractReference`) parsing production/reference/machine snapshots.
- `DapperTampaoRepository` — `ParseValues` (values_json → sorted decimal dict).
- `DapperBoquilhasRepository` — `ParseGuidJsonArray` (deleted_movements), `LikePattern`.
- `DapperRepairRepository` — `RepairerSnapshot` deserialize via `JsonSerializer`.
- `DapperArticleReferenceImageRepository` — before/after audit snapshots serialized as `{"reference": ..., "image_asset_id": ...}` (jsonb casts).
- `DapperAppSettingsReader` — `JsonDocument` parse of `setting_value`.

---

## 12. Supabase / Authentication Adapters

### `SupabaseAuthAdapter`
File: `Auth\SupabaseAuthAdapter.cs`
- Implements `ISupabaseAuthAdapter`.
- Constructor: `HttpClient`, `supabaseUrl`, `anonKey`.
- Public method: `SignInWithPasswordAsync(string email, string password, ct)`.
- External endpoint: `POST {url}/auth/v1/token?grant_type=password` (GoTrue), header `apikey: <anon>`.
- Mapping: parses `SignInResponse.User.Id` → `AuthUser(Guid, email)`.
- Error/status handling: missing config → `AUTH_PROVIDER_MISCONFIGURED`; network → `AUTH_PROVIDER_UNAVAILABLE`; HTTP 429 → rate-limit; 401/403 → misconfigured apikey; other 4xx → `INVALID_CREDENTIALS`; 5xx → unavailable. `ParseGoTrueError` extracts `error`/`error_description`.
- Private response types: `SignInResponse`, `UserPayload`.

### `SupabaseAdminProvisioningAdapter`
File: `Auth\SupabaseAdminProvisioningAdapter.cs`
- Implements `IAdminProvisioningAdapter`.
- Constructor: `HttpClient`, `supabaseUrl`, `serviceRoleKey`.
- Public methods:
  - `EnsureAuthUserAsync` / `EnsureAuthUserWithStatusAsync` — create-or-lookup (`EnsuredAuthUser` with `AccountPreExisted`).
  - `RequestPasswordResetAsync(Guid authUserId, ct)` — privileged GET user → `POST /auth/v1/admin/generate_link` (type=recovery).
  - `GetUserEmailsAsync(IReadOnlyCollection<Guid>, ct)` — paginated `GET /auth/v1/admin/users?page=N&per_page=100` (pageSize 100, maxPages 100).
- External endpoints: `POST /auth/v1/admin/users` (create, `email_confirm=true`), `GET /auth/v1/admin/users/{id}`, `POST /auth/v1/admin/generate_link`, `GET /auth/v1/admin/users?email=...`, `GET /auth/v1/admin/users?page=...`.
- Privileged headers: `Authorization: Bearer <service-role>` + `apikey: <service-role>`.
- Error/status: 422/409 → `PROVISIONING_CONFLICT` (idempotent lookup); other failure → `PROVISIONING_FAILED`; provider unreachable → `AUTH_PROVIDER_UNAVAILABLE`; config missing → `PROVISIONING_CONFIGURATION_MISSING`.
- Private response types: `UserPayload`, `UserListing`.

### `SupabaseSettings`
File: `Auth\SupabaseSettings.cs`
- Static settings contract (env vars): `BA_DMO_SUPABASE_URL`, `BA_DMO_SUPABASE_ANON_KEY`, `BA_DMO_SUPABASE_SERVICE_ROLE_KEY`, `BA_DMO_BOOTSTRAP_ADMIN_EMAIL`, `BA_DMO_BOOTSTRAP_ADMIN_PASSWORD`, `BA_DMO_BOOTSTRAP_ADMIN_NAME`.
- Resolvers: `ResolveUrl`, `ResolveAnonKey`, `ResolveServiceRoleKey` (trim/blank→null).

---

## 13. Document / PDF Infrastructure

Pure-PDF renderers (manual PDF byte assembly, no external SDK). All generate `%PDF-1.4` documents with Helvetica Type1 font and text escaping.

### `JobOnPdfRenderer`
File: `Access\JobOnPdfRenderer.cs`
- Implements `IJobOnPdfRenderer`.
- `RenderJobOnDocument(JobOnPdfData)` → `byte[]`; 4 A4 pages (595×842 pt): Ficha de Artigo ×2, Job-On Moldes, Trabalho de Equipa.
- Colour tokens from `dmo-tokens.css`; UTF-8 with `\uXXXX` escaping; private helpers `RenderFichaDeArtigo`, `RenderJobOnMoldes`, `RenderTrabalhoDeEquipa`, `WriteHeaderBlock`, `WriteToolSection`, `WriteCompactSection`, `WriteMoldDetail`, `Escape`, `WrapText`.

### `PegamentoPdfRenderer`
File: `Access\PegamentoPdfRenderer.cs`
- Implements `IPegamentoPdfRenderer`.
- `RenderPegamento(PegamentoPdfData)` → `byte[]`; single A4 page, component (CM/BQ/MF) summary + measurement tables; uses `PegamentoMeasurementCalculator` for status.
- `Escape` transliterates accented chars to ASCII for Helvetica.

### `PesoSingleFilePdfRenderer`
File: `Access\PesoSingleFilePdfRenderer.cs`
- Implements `IPdfRenderer`.
- `RenderPesoFolha(PesoFolhaPdf)` → `byte[]`; single A4 page with sections (Identificação / Comparação / Por Contra Molde / Referências / Rastreabilidade); DMO colour tokens; `Esc` with `\uXXXX`; private layout/graphic helpers (`Rect`, `HLine`, `Txt`, `SecHeader`, `CmTableRow`, `AssemblePdf`).

---

## 14. File / Storage Infrastructure

### `FileSystemJobOnImageProvider`
File: `Access\FileSystemJobOnImageProvider.cs`
- Implements `IJobOnImageProvider`.
- Constructor: `IJobOnRepository`, `IArticleReferenceImageRepository`, `IAppSettingsReader` (no `IDbConnectionFactory` — reads go through the injected ports).
- `ResolveAsync(Guid jobOnId, ct)` → `ImageResolution?`; chain (never throws, returns null on any missing part):
  1. `IJobOnRepository.GetByIdAsync` → extract Article/Reference code from the current revision's `reference_snapshot` (`ArticleReferenceImageRules.ExtractReferenceCode`);
  2. `IArticleReferenceImageRepository.GetAsync(referenceCode)` → current master `image_asset_id` (`article_reference_images`, N29/N30);
  3. `IAppSettingsReader.GetOutputRootAsync` → `main_documents_output_root` (`app_settings`);
  4. `Path.Combine(outputRoot, imageAssetId)` (asset id validated by `ArticleReferenceImageRules.TryNormalizeImageAssetId`; N29 CHECK forbids path separators/`..`), `File.ReadAllBytesAsync`, MIME by extension (`DetectMimeType`: jpg/jpeg/png/gif/webp/bmp, fallback image/jpeg).

(No dedicated storage/output-path writer class exists in Infrastructure; the image chain resolves through `article_reference_images` (master) — NOT `job_on.production_folder`. The `job_on.production_folder` resolver (`DapperJobOnProductionFolderResolver`) remains independently consumed by Peso/Pegamentos document metadata.)

---

## 15. Migration Infrastructure

### `MigrationRunner`
File: `Persistence\Migrations\MigrationRunner.cs`
- Constructor: `IMigrationScriptGateway`, `IClock`.
- `RunAsync(migrations, ct)` → `MigrationRunReport(Applied, Skipped)`; per file: SHA-256 via `MigrationChecksum.ComputeSha256File`, compare with applied; never applied → execute whole script (`gateway.ExecuteScriptAsync`) + record after success; applied same checksum → skip; applied different checksum → `MigrationChecksumMismatchException`. Execution timing via `IClock`. On failure wraps in `MigrationExecutionException`.

### `IMigrationScriptGateway` / `NpgsqlMigrationScriptGateway`
Files: `Persistence\Migrations\IMigrationScriptGateway.cs`, `Persistence\Migrations\NpgsqlMigrationScriptGateway.cs`
- Gateway contract: `OpenAsync`, `EnsureTrackingTableAsync`, `GetAppliedAsync`, `ExecuteScriptAsync`, `RecordAppliedAsync`; `IAsyncDisposable`.
- Npgsql implementation: `schema_migrations` tracking table (CREATE TABLE IF NOT EXISTS); `GetAppliedAsync` reads applied records; `ExecuteScriptAsync` executes the WHOLE script in one `NpgsqlTransaction` (commit/rollback); `RecordAppliedAsync` inserts with `AddWithValue` parameters.

### `MigrationDiscovery`
File: `Persistence\Migrations\MigrationDiscovery.cs`
- Regex `^(N\d{2})_[A-Za-z0-9_]+\.sql$`; discovers `N##_name.sql` in a directory in ordinal order; duplicate-version → `MigrationDiscoveryException`.

### `MigrationChecksum`
File: `Persistence\Migrations\MigrationChecksum.cs`
- `ComputeSha256(byte[])` / `ComputeSha256File(string)` — SHA-256 over raw file bytes, hex lowercase.

### Records / exceptions
- `MigrationFile(Version, FileName, FullPath)`; `AppliedMigration(Version, FileName, Sha256, AppliedAtUtc)` — `Persistence\Migrations\MigrationFile.cs`.
- `MigrationDiscoveryException`, `MigrationChecksumMismatchException`, `MigrationExecutionException` — `Persistence\Migrations\MigrationExceptions.cs`.
- `MigrationRunReport(Applied, Skipped)` with `NothingToDo` — `Persistence\Migrations\MigrationRunner.cs`.

---

## 16. Settings / Options

| Class | Env vars / keys | Consumers in Infrastructure | File |
|---|---|---|---|
| `DatabaseConnectionSettings` | `BA_DMO_DB_CONNECTION_STRING`, fallback `DATABASE_URL` | `DbConnectionFactory`, `LazyDbConnectionFactory` | `Persistence\DatabaseConnectionSettings.cs` |
| `SupabaseSettings` | `BA_DMO_SUPABASE_URL`, `BA_DMO_SUPABASE_ANON_KEY`, `BA_DMO_SUPABASE_SERVICE_ROLE_KEY`, bootstrap/admin vars | `SupabaseAuthAdapter`, `SupabaseAdminProvisioningAdapter` | `Auth\SupabaseSettings.cs` |
| `DapperAppSettingsReader` (key) | `main_documents_output_root` in `app_settings` | `FileSystemJobOnImageProvider` | `Access\DapperAppSettingsReader.cs` |

(No dedicated `IOptions`/`Options<T>` classes are present; configuration is resolved from environment via the static settings classes above.)

---

## 17. Dependency Injection Registration

No `AddInfrastructure(...)` / `IServiceCollection` registration extension exists
inside `src\BA.Dmo.Infrastructure\` (verified by search — no matches).
Concrete DI registrations live in the Web composition root: `src\BA.Dmo.Web\Program.cs`
(`AddSingleton` for `DbConnectionFactory`/`LazyDbConnectionFactory`,
`IInternalUserRepository`, `IAdminRepository`, `IModuleCatalogMirrorRepository`,
auth adapters, PDF renderers; `AddScoped` for the per-module repositories/lookups/
UoW factories; `PersistenceMappings.Configure()` called at line ~60).

---

## 18. Classification / Reconciliation Notes

Evidence-based labels from the reconciliation pass (source > migrations > prior map
text). Labels and evidence only; no fix or deletion is recommended here.

- **CONFIRMED CURRENT — `DapperArticleReferenceImageRepository` vs N29/N30.**
  Every column read/written (`reference_code`, `image_asset_id`, `updated_by`,
  `updated_at_utc`) matches `database\migrations\N29_jobon_reference_images.sql`;
  the `ON CONFLICT (reference_code) DO UPDATE` upsert matches the PK; audit rows
  target `job_on_audit_event` columns defined in N05 (before/after as `jsonb`);
  N30 covers the `updated_by` FK index. Reference normalization
  (`ArticleReferenceImageRules.NormalizeReferenceCode`) is consistent with the
  N29 CHECK (uppercase, trimmed).
- **CONFIRMED CURRENT — `DapperInternalUserRepository` / `DapperAdminRepository`
  vs N27/N31.** SQL uses the `internal_user_access_templates` junction created in
  N27 (PK `(actor_id, template_id)`), the N31 single-effective-template constraint,
  and `profile_title`/`template_id` kept in sync by N27/N31. Admin count + bootstrap
  queries match the `[{"moduleId":"admin"}]` containment tests used in the
  migrations. No schema or migration drift found.
- **CONFIRMED CURRENT — spot-cross-referenced module queries.** `jobon_user_current`
  (N24), `controlo_sheets/items/events` incl. `mcaliper_link`/`display_id` (N23),
  `repair_events` incl. `repair_scope`/`internal_repair_record_id`/`canceled` (N08),
  `repair_exits`/`repair_exit_items` status vocabularies (N08), `peso_leituras`/
  `peso_day_approvals` (N06), `internal_repair_records` `job_on_revision_id`,
  `correction_of_id` (N22), `warehouse_movements.repair_exit_id` (N09),
  `module_catalog_mirror` (N02), `app_settings` (N11) — all match the migrations.
- **INTENTIONAL NORMALIZATION — `job_on_revision.image_asset_id` legacy.** N29
  documents the per-revision image column as dormant (not dropped) after the move to
  the master `article_reference_images` table. `DapperJobOnRepository` still writes/
  reads `image_asset_id` on revisions (`InsertRevisionAsync`, `InsertImageMutationAsync`)
  and keeps them audited; the new master write path
  (`DapperArticleReferenceImageRepository`) does NOT touch the legacy column.
  Consistent with N29; not drift.
- **UNKNOWN / OWNER DECISION REQUIRED — `access_template_profiles` (N31) has no
  Infrastructure counterpart.** The table is created/maintained by migration
  (trigger `trg_access_templates_ensure_profile` + backfill) and is read/written
  directly by `src\BA.Dmo.Web\Pages\Admin\TemplateProfileStore.cs` (Web-layer SQL,
  outside Infrastructure). Infrastructure identity/admin repositories instead read
  the compatibility columns `internal_users.profile_title` / `internal_users.template_id`
  which N31 keeps in sync. Whether the Web-layer direct SQL or the dual
  profile_title/profile-table source should be consolidated is an architecture
  decision outside this map.
- **REVIEW NOTE (comment vs code, not behavior) — `Identity\DapperInternalUserRepository.cs`
  header comment still says "tables from U-02 N01_identity.sql" while the SQL uses the
  N27 `internal_user_access_templates` junction (and N31 constraint). The SQL is
  current; only the class-level comment is stale (evidence: comment line ~9 vs
  `FindByAuthUserIdSql`/`InsertUserTemplateSql` in the same file).
- **No evidence of role duplication.** Classes that query the same tables serve
  distinct roles (e.g. `DapperAdminRepository.CountActiveAdminsOnAsync` vs
  `DapperInternalUserRepository.AdminExistsAsync`; `DapperArmazemRepository`/
  `DapperArmazemRepairMovementRepository` location get-or-create; the three
  context lookups over `job_on_revision`). No POTENTIAL OVERLAP /
  LEGACY CANDIDATE / ORPHAN CANDIDATE finding is supported by evidence.

---

## Direct Technical References

### Infrastructure-internal references

Relations where both concrete technical objects live under
`src\BA.Dmo.Infrastructure\` (visible inside Infrastructure source):

- `LazyDbConnectionFactory` → `DbConnectionFactory.FromEnvironment` (concrete instantiation) → `DatabaseConnectionSettings`.
- `DbConnectionFactory.FromEnvironment` → `DatabaseConnectionSettings.ResolveConnectionString`.
- `Db` (static ctor) → `DateTimeOffsetHandler` (`SqlMapper.AddTypeHandler`).
- `DapperUnitOfWork` → `IDbConnection` / `IDbTransaction` (Open/commit/rollback mechanics); `BeginAsync`/`RunAsync` → `IDbConnectionFactory`.
- `MigrationRunner` → `MigrationChecksum.ComputeSha256File` (constructor also receives external `IMigrationScriptGateway` + `IClock`).
- `NpgsqlMigrationScriptGateway` → `NpgsqlConnection` / `NpgsqlTransaction` / `NpgsqlCommand`.
- `DbConnectionFactory` → `NpgsqlConnection` / `NpgsqlConnectionStringBuilder`.

### External contracts used by Infrastructure

Direct contracts / interfaces consumed or implemented by Infrastructure classes
(defined in `BA.Dmo.Application` unless noted). Names/technical use only:

- `DapperJobOnRepository` → implements `IJobOnRepository`; constructor receives `IDbConnectionFactory`.
- `DapperPesoRepository` → implements `IPesoRepository`; constructor receives `IDbConnectionFactory`.
- `DapperPegamentoRepository` → implements `IPegamentoRepository`; constructor receives `IDbConnectionFactory`.
- `DapperControloSheetRepository` → implements `IControloSheetRepository`; constructor receives `IDbConnectionFactory`; writes receive `IDbUnitOfWork`.
- `DapperAdminRepository` → implements `IAdminRepository`; constructor receives `IDbConnectionFactory`.
- `DapperInternalUserRepository` → implements `IInternalUserRepository`; constructor receives `IDbConnectionFactory`.
- `DapperRepairRepository` → implements `IRepairRepository`; constructor receives `IDbConnectionFactory`; coordinated writes receive `IDbUnitOfWork`.
- `DapperArmazemRepairMovementRepository` → implements `IArmazemRepairMovementPort`; constructor receives `IDbConnectionFactory`; writes receive `IDbUnitOfWork`.
- `DapperArmazemRepository` → implements `IArmazemRepository`; constructor receives `IDbConnectionFactory`.
- `DapperBoquilhasRepository` → implements `IBoquilhasRepository`; constructor receives `IDbConnectionFactory`; writes receive `IDbUnitOfWork`.
- `DapperFerramentasRepository` → implements `IFerramentasRepository`; constructor receives `IDbConnectionFactory`.
- `DapperHistoriaRepository` → implements `IHistoriaRepository`; constructor receives `IDbConnectionFactory`.
- `DapperTampaoRepository` → implements `ITampaoRepository`; constructor receives `IDbConnectionFactory`; writes receive `IDbUnitOfWork`.
- `DapperModuleCatalogMirrorRepository` → implements `IModuleCatalogMirrorRepository`; constructor receives `IDbConnectionFactory`.
- `DapperJobOnUserContextRepository` → implements `IJobOnUserContextRepository`; constructor receives `IDbConnectionFactory`.
- `DapperAppSettingsReader` → implements `IAppSettingsReader`; constructor receives `IDbConnectionFactory`.
- `DapperArticleReferenceImageRepository` → implements `IArticleReferenceImageRepository`; constructor receives `IDbConnectionFactory`.
- `DapperJobOnActiveContextLookup` → implements `IJobOnActiveContextLookup`; constructor receives `IJobOnRepository` + `IDbConnectionFactory`.
- `DapperControloProductionContextLookup` → implements `IControloProductionContextLookup`; constructor receives `IJobOnRepository` + `IDbConnectionFactory`.
- `DapperJobOnProductionContextLookup` → implements `IJobOnProductionContextLookup`; constructor receives `IDbConnectionFactory`.
- `DapperJobOnProductionFolderResolver` → implements `IJobOnProductionFolderResolver`; constructor receives `IDbConnectionFactory`.
- `DapperFerramentasRuleLookup` → implements `IFerramentasRuleLookup`; constructor receives `IDbConnectionFactory`.
- `DapperFerramentasIdentityLookup` → implements `IFerramentasIdentityLookup`; constructor receives `IDbConnectionFactory`.
- `DapperFerramentasPieceLookup` → implements `IFerramentasPieceLookup`; constructor receives `IDbConnectionFactory`.
- `FileSystemJobOnImageProvider` → implements `IJobOnImageProvider`; constructor receives `IJobOnRepository`, `IArticleReferenceImageRepository`, `IAppSettingsReader`.
- `DapperRepairUnitOfWorkFactory` → implements `IRepairUnitOfWorkFactory`; constructs `DapperUnitOfWork`.
- `DapperTampoesUnitOfWorkFactory` → implements `ITampoesUnitOfWorkFactory`; constructs `DapperUnitOfWork`.
- `DapperBoquilhasUnitOfWorkFactory` → implements `IBoquilhasUnitOfWorkFactory`; constructs `DapperUnitOfWork`.
- `SupabaseAuthAdapter` → implements `ISupabaseAuthAdapter`; constructor receives `HttpClient`.
- `SupabaseAdminProvisioningAdapter` → implements `IAdminProvisioningAdapter`; constructor receives `HttpClient`.
- `LazyDbConnectionFactory` → implements `IDbConnectionFactory`.
- `DbConnectionFactory` → implements `IDbConnectionFactory`.
- `DapperUnitOfWork` → implements `IDbUnitOfWork`.
- `MigrationRunner` → constructor receives `IMigrationScriptGateway` + `IClock`.
- `NpgsqlMigrationScriptGateway` → implements `IMigrationScriptGateway`.
- `JobOnPdfRenderer` → implements `IJobOnPdfRenderer`.
- `PegamentoPdfRenderer` → implements `IPegamentoPdfRenderer`.
- `PesoSingleFilePdfRenderer` → implements `IPdfRenderer`.

---

## Sources Verified

Primary evidence (this reconciliation pass, HEAD `8478308`):
- `src\BA.Dmo.Infrastructure\` — all **48** source `.cs` files across 5 folders
  (`Access\` 31, `Auth\` 3, `Identity\` 1, `Persistence\` 6, `Persistence\Migrations\` 7)
  read for class/interface/table/transaction facts. `DapperArticleReferenceImageRepository`
  (`Access\DapperArticleReferenceImageRepository.cs`) was verified and added to this map
  in this pass.
- `src\BA.Dmo.Web\Program.cs` — DI registrations of every Infrastructure port
  (incl. `IArticleReferenceImageRepository` line ~176, `IJobOnImageProvider` line ~180)
  and `PersistenceMappings.Configure()` call.
- `src\BA.Dmo.Application\` — port interfaces confirmed at the listed paths
  (e.g. `Modules\JobOn\ArticleReferenceImage.cs` → `IArticleReferenceImageRepository`,
  `Shared\IJobOnImageProvider.cs`); consumers confirmed (`JobOnService`,
  `JobOnPdfService`).
- `database\migrations\N01–N31` — column/key cross-reference for the most important
  queries (N29/N30 article images, N27/N31 access junction + profiles, N05
  job_on_audit_event, N08 repairs, N06 peso, N23 controlo, N24 jobon_user_current,
  N22 reparação interna, N09 armazém, N02 catalog, N11 app_settings).

Referenced contract names (Identification only):
- Application persistence-port interfaces implemented by Infrastructure classes
  (listed under each class; interface source locations live in `BA.Dmo.Application`).

Cross-references (relative links, same `docs\Maps\` folder):
- [00_INDEX.md](00_INDEX.md) (mapping contract / register)
- [01_DOMAIN.md](01_DOMAIN.md) · [02_DATABASE.md](02_DATABASE.md) ·
  [03_MIGRATIONS.md](03_MIGRATIONS.md) · [05_TESTS.md](05_TESTS.md) ·
  [19_APPLICATION.md](19_APPLICATION.md)
- Module maps: [06_JOB_ON.md](06_JOB_ON.md) · [07_CONTROLO.md](07_CONTROLO.md) ·
  [08_FERRAMENTAS.md](08_FERRAMENTAS.md) · [09_ARMAZEM.md](09_ARMAZEM.md) ·
  [10_BOQUILHAS.md](10_BOQUILHAS.md) · [11_REPARACAO_INTERNA.md](11_REPARACAO_INTERNA.md) ·
  [12_REPARACAO_EXTERNA.md](12_REPARACAO_EXTERNA.md) · [13_TAMPOES.md](13_TAMPOES.md) ·
  [14_HISTORIA.md](14_HISTORIA.md) · [15_ADMIN.md](15_ADMIN.md) ·
  [16_USERS_ACCESS.md](16_USERS_ACCESS.md) · [18_LOGIN.md](18_LOGIN.md)

**Outside this map's scope:**
- Database schema (`02_DATABASE.md`)
- migration evolution (`03_MIGRATIONS.md`)
- Domain (`01_DOMAIN.md`)
- tests — map: `05_TESTS.md`; physical test sources live under
  `AI-CONTEXT\docs\tests\` (`BA.Dmo.IntegrationTests\`, `BA.Dmo.UnitTests\`)