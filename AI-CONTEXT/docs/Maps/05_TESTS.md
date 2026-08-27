# BA DMO — Tests Technical Map

## Navigation Index

1. [Purpose](#1-purpose)
2. [Test Project Structure](#2-test-project-structure)
3. [Global Test Inventory](#3-global-test-inventory)
4. [Unit Test Project](#4-unit-test-project)
5. [Integration Test Project](#5-integration-test-project)
6. [Fixtures / Shared Test Infrastructure](#6-fixtures--shared-test-infrastructure)
7. [Test Doubles](#7-test-doubles)
8. [Builders / Test Data Helpers](#8-builders--test-data-helpers)
9. [Database / Integration Test Mechanics](#9-database--integration-test-mechanics)
10. [Web / HTTP Test Mechanics](#10-web--http-test-mechanics)
11. [Assertion / Mocking Patterns](#11-assertion--mocking-patterns)
12. [Parameterized / Conditional Tests](#12-parameterized--conditional-tests)
13. [Target-to-Test Index](#13-target-to-test-index)
14. [Module / Area Test Index](#14-module--area-test-index)
15. [Count Summary by Project](#15-count-summary-by-project)
16. [Count Summary by Area](#16-count-summary-by-area)
17. [Coverage Gaps — NEEDS REVIEW](#17-coverage-gaps--needs-review)
18. [Source Locations](#18-source-locations)
19. [Sources Verified](#sources-verified)

**Related maps (relative links):** [00_INDEX.md](00_INDEX.md) · [03_MIGRATIONS.md](03_MIGRATIONS.md) · [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md) · [19_APPLICATION.md](19_APPLICATION.md) · [20_WEB.md](20_WEB.md) · module maps [06_JOB_ON.md](06_JOB_ON.md) [07_CONTROLO.md](07_CONTROLO.md) [08_FERRAMENTAS.md](08_FERRAMENTAS.md) [09_ARMAZEM.md](09_ARMAZEM.md) [10_BOQUILHAS.md](10_BOQUILHAS.md) [11_REPARACAO_INTERNA.md](11_REPARACAO_INTERNA.md) [12_REPARACAO_EXTERNA.md](12_REPARACAO_EXTERNA.md) [13_TAMPOES.md](13_TAMPOES.md) [14_HISTORIA.md](14_HISTORIA.md) [15_ADMIN.md](15_ADMIN.md) [16_USERS_ACCESS.md](16_USERS_ACCESS.md) [17_DESIGN_LABORATORIO.md](17_DESIGN_LABORATORIO.md) [18_LOGIN.md](18_LOGIN.md)

---

## 1. Purpose

This is the pure technical **TESTS** transversal map (MAP-05) of the BA DMO codebase. It is **inventory + location**:

- test projects and `.csproj` definitions;
- test source files, test classes, test methods;
- fixtures, shared test infrastructure, fakes / stubs / test doubles;
- test data builders / helpers, database and web/HTTP test mechanics;
- direct test-to-target references visible in test code;
- exact source locations (all under `AI-CONTEXT\docs\tests\`).

This revision reconciles the map against the **current** tree: the test projects now live under `AI-CONTEXT\docs\tests\` (= a `tests` solution folder in `BA-DMO.sln`), and a third project, `BA.Dmo.VisualHost`, was added. Evidence-based `COVERAGE GAP — NEEDS REVIEW` records are included in §17 (per reconciliation mandate) together with classification labels where a suspicious structure is found (`CONFIRMED CURRENT`, `POTENTIAL OVERLAP — NEEDS AUDIT`, `UNKNOWN / OWNER DECISION REQUIRED`, …). No gap is fixed and no deletion is recommended here.

---

## 2. Test Project Structure

### 2.1 Project level facts

| Fact | Value |
|---|---|
| Root | `D:\BA-DMO` |
| SLN | `BA-DMO.sln` (all three test projects referenced under the `tests` solution folder) |
| Central build settings | `D:\BA-DMO\Directory.Build.props` |
| Target framework (all projects) | `net10.0` (from `Directory.Build.props`) |
| SDK / roll-forward | no `global.json` at repo root (none present anywhere in the tree) |
| Shared compile settings | `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `NeutralLanguage=pt-PT` |

Both test projects set `<IsPackable>false</IsPackable>` and declare a global `<Using Include="Xunit" />`, so xUnit assert helpers are available file-wide without explicit usings.

### 2.2 Test projects

| Test Project | Path | Framework | Source Files | Main Areas |
|---|---|---:|---:|---|
| `BA.Dmo.UnitTests` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\` | net10.0 | 81 | Domain + Application unit tests, no I/O |
| `BA.Dmo.IntegrationTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\` | net10.0 | 53 | Web + Infrastructure contract tests |
| `BA.Dmo.VisualHost` | `AI-CONTEXT\docs\tests\BA.Dmo.VisualHost\` | net10.0 | 1 | Manual Kestrel visual-verification host (Exe) |

**Total test source files:** 135. Excluded from all counts: `bin\`, `obj\`, build/test-results and coverage output.

### 2.3 Project references — `BA.Dmo.UnitTests.csproj`

`AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\BA.Dmo.UnitTests.csproj`

- Packages: `coverlet.collector` 6.0.4, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4.
- Project references: `src\BA.Dmo.Domain`, `src\BA.Dmo.Application`.

### 2.4 Project references — `BA.Dmo.IntegrationTests.csproj`

`AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\BA.Dmo.IntegrationTests.csproj`

- Packages: `coverlet.collector` 6.0.4, `Microsoft.AspNetCore.Mvc.Testing` 10.0.11, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4.
- Project references: `src\BA.Dmo.Web`, `src\BA.Dmo.Infrastructure`.

### 2.5 Project — `BA.Dmo.VisualHost`

`AI-CONTEXT\docs\tests\BA.Dmo.VisualHost\`

- `BA.Dmo.VisualHost.csproj`: `OutputType=Exe`, `<IsPackable>false</IsPackable>`; references `BA.Dmo.IntegrationTests` and `src\BA.Dmo.Web`.
- `Program.cs`: starts a real Kestrel host through `ShellRoutingTests.ShellFixture` (profile-switchable via CLI arg: `boquilhas`, `jobon`, `peso`, `peso-responsavel`, `armazem-create`, `reparacao-interna`, `tampoes`, default `armazem`; port default 5052, overridable as first arg). Serves the app shell with a test-only login page for manual visual verification, then blocks forever (`await Task.Delay(Timeout.Infinite)`).

### 2.6 Test folders (top-level)

`BA.Dmo.UnitTests`: `Modules\{Armazem,Boquilhas,Controlo,Ferramentas,Historia,JobOn,Pegamentos,Peso,ReparacaoExterna,ReparacaoInterna,Tampoes}`, `Shared\{Access,Admin,Identity,Kernel,Persistence}`.

`BA.Dmo.IntegrationTests`: `Access`, `Cli`, `Controlo`, `Design`, `Ferramentas`, `Identity`, `Integrity`, `JobOn`, `Migrations`, `Pegamentos`, `Persistence`, `Peso`, `ReparacaoExterna`, `ReparacaoInterna`, `Security`, `Tampoes`, plus root helper `IntegrationTestEnvironment.cs`.

---

## 3. Global Test Inventory

Direct targets named below are the production types imported and exercised by each test class. All paths are under `AI-CONTEXT\docs\tests\`.

| Project | Area / Folder | File / Class | Kind | Direct Target | Path |
|---|---|---|---|---|---|
| UnitTests | Modules/JobOn | `JobOnServiceTests` | Unit test class | `JobOnService`, `JobOnAuthorizationGate`, image attach/replace/remove path (`IArticleReferenceImageRepository`) | `BA.Dmo.UnitTests\Modules\JobOn\JobOnServiceTests.cs` |
| UnitTests | Modules/JobOn | `JobOnDomainTests` | Unit test class | `JobOn` (domain), `JobOnLifecycleStateCodec` | `BA.Dmo.UnitTests\Modules\JobOn\JobOnDomainTests.cs` |
| UnitTests | Modules/JobOn | `JobOnPdfTests` | Unit test class | `JobOnPdfService`, `JobOnService`, `IJobOnImageProvider` | `BA.Dmo.UnitTests\Modules\JobOn\JobOnPdfTests.cs` |
| UnitTests | Modules/JobOn | `JobOnVerificationGeneratorTests` | Unit test class | `JobOnVerificationGenerator` | `BA.Dmo.UnitTests\Modules\JobOn\JobOnVerificationGeneratorTests.cs` |
| UnitTests | Modules/JobOn | `JobOnActivityResolverTests` | Unit test class | `JobOnActivityResolver` | `BA.Dmo.UnitTests\Modules\JobOn\JobOnActivityResolverTests.cs` |
| UnitTests | Modules/JobOn | `JobOnUserContextTests` | Unit test class | `JobOnService` (current-open context) | `BA.Dmo.UnitTests\Modules\JobOn\JobOnUserContextTests.cs` |
| UnitTests | Modules/JobOn | `JobOnRevisionImmutabilityIntegrationTests` | Unit-project cross-module integration | `JobOnService`, `PesoService`, `PegamentoService` | `BA.Dmo.UnitTests\Modules\JobOn\JobOnRevisionImmutabilityIntegrationTests.cs` |
| UnitTests | Modules/JobOn | `FakeJobOnRepository` | Fake | `IJobOnRepository` | `BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnRepository.cs` |
| UnitTests | Modules/JobOn | `FakeJobOnUserContextRepository` | Fake | `IJobOnUserContextRepository` | `BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnUserContextRepository.cs` |
| UnitTests | Modules/JobOn | `FakeArticleReferenceImageRepository` | Fake | `IArticleReferenceImageRepository` | `BA.Dmo.UnitTests\Modules\JobOn\FakeArticleReferenceImageRepository.cs` |
| UnitTests | Modules/Peso | `PesoServiceTests` | Unit test class | `PesoService`, `PesoAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Peso\PesoServiceTests.cs` |
| UnitTests | Modules/Peso | `PesoDomainTests` | Unit test class | `PesoValidator`, `PesoProcessoCodec`, `PesoRecordTypeCodec`, `PesoControlStateCodec`, `ReportPathValidator` | `BA.Dmo.UnitTests\Modules\Peso\PesoDomainTests.cs` |
| UnitTests | Modules/Peso | `WeightCalculatorTests` | Unit test class | `WeightCalculator`, `PesoModuleCatalog` | `BA.Dmo.UnitTests\Modules\Peso\WeightCalculatorTests.cs` |
| UnitTests | Modules/Peso | `PesoControlWorkflowTests` | Unit test class | `PesoControl`, `PesoValidator`, `PesoCmDecisionCodec` | `BA.Dmo.UnitTests\Modules\Peso\PesoControlWorkflowTests.cs` |
| UnitTests | Modules/Peso | `FakePesoRepository` | Fake | `IPesoRepository` | `BA.Dmo.UnitTests\Modules\Peso\FakePesoRepository.cs` |
| UnitTests | Modules/Armazem | `ArmazemServiceTests` | Unit test class | `ArmazemService`, `ArmazemAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Armazem\ArmazemServiceTests.cs` |
| UnitTests | Modules/Armazem | `WarehouseStockRulesTests` | Unit test class | `WarehouseLocation`, `WarehouseStockRules` | `BA.Dmo.UnitTests\Modules\Armazem\WarehouseStockRulesTests.cs` |
| UnitTests | Modules/Armazem | `ArmazemAuthorizationGateTests` | Unit test class | `ArmazemAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Armazem\ArmazemAuthorizationGateTests.cs` |
| UnitTests | Modules/Armazem | `FerramentasArmazemToolIdentityResolverTests` | Unit test class | `FerramentasArmazemToolIdentityResolver` | `BA.Dmo.UnitTests\Modules\Armazem\FerramentasArmazemToolIdentityResolverTests.cs` |
| UnitTests | Modules/Armazem | `FakeArmazemRepository` / `FakeToolIdentityResolver` | Fakes | `IArmazemRepository`, `IToolIdentityResolver` | `BA.Dmo.UnitTests\Modules\Armazem\` |
| UnitTests | Modules/Armazem | (in `ArmazemTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `ICurrentUserAccessor`, `IFerramentasIdentityLookup` | `BA.Dmo.UnitTests\Modules\Armazem\ArmazemTestSupport.cs` |
| UnitTests | Modules/Boquilhas | `BoquilhasServiceTests` | Unit test class | `BoquilhasService`, `BqAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Boquilhas\BoquilhasServiceTests.cs` |
| UnitTests | Modules/Boquilhas | `BqAuthorizationGateTests` | Unit test class | `BqAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Boquilhas\BqAuthorizationGateTests.cs` |
| UnitTests | Modules/Boquilhas | `BqInventoryCalculatorTests` | Unit test class | `BqInventoryCalculator`, `BqRules` | `BA.Dmo.UnitTests\Modules\Boquilhas\BqInventoryCalculatorTests.cs` |
| UnitTests | Modules/Boquilhas | (in `BqTestSupport`) | Test helper + fakes | `IClock`, authorship/current-user, `IBoquilhasUnitOfWorkFactory`, `IBoquilhasRepository`, `IDbUnitOfWork` | `BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs` |
| UnitTests | Modules/Controlo | `ControloSheetServiceTests` | Unit test class | `ControloSheetService`, `ControloSheetAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Controlo\ControloSheetServiceTests.cs` |
| UnitTests | Modules/Controlo | `ControloFolhaTests` | Unit test class | `ControloFolha` | `BA.Dmo.UnitTests\Modules\Controlo\ControloFolhaTests.cs` |
| UnitTests | Modules/Controlo | (in `ControloTestSupport`) | Test helper + fakes | `IClock`, authorship, `IRepairUnitOfWorkFactory`, `IControloSheetRepository`, `IControloProductionContextLookup` | `BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs` |
| UnitTests | Modules/Ferramentas | `FerramentasServiceTests` | Unit test class | `FerramentasService`, `FerramentasAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasServiceTests.cs` |
| UnitTests | Modules/Ferramentas | `FerramentasDomainTests` | Unit test class | `ToolReference`, `ToolLote`, `ToolCheckRule`, `PhysicalPiece`, `FerramentasToolTypeCodec` | `BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasDomainTests.cs` |
| UnitTests | Modules/Ferramentas | `FerramentasUtilisationServiceTests` | Unit test class | `FerramentasService` (utilisation commands) | `BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasUtilisationServiceTests.cs` |
| UnitTests | Modules/Ferramentas | `FakeFerramentasRepository` + `FerramentasTestSupport` | Fake + helper | `IFerramentasRepository`, `IFerramentasRuleLookup`, clock/authorship/user | `BA.Dmo.UnitTests\Modules\Ferramentas\` |
| UnitTests | Modules/Historia | `HistoriaServiceTests` | Unit test class | `HistoriaService`, `HistoriaAuthorizationGate`, `IHistoriaRepository` | `BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs` |
| UnitTests | Modules/Historia | `HistoriaAuthorizationGateTests` | Unit test class | `HistoriaAuthorizationGate`, `HistoriaModuleCatalog` | `BA.Dmo.UnitTests\Modules\Historia\HistoriaAuthorizationGateTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoServiceTests` | Unit test class | `PegamentoService`, `PegamentoAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoServiceTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoPdfTests` | Unit test class | `PegamentoPdfService` (filename `PegamentoPdfFilename.Compute`) | `BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoPdfTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoMeasurementCalculatorTests` | Unit test class | `PegamentoMeasurementCalculator` | `BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoMeasurementCalculatorTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoHistoricalRelationshipTests` | Unit test class | `PegamentoService` | `BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoHistoricalRelationshipTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoDocumentConfirmationTests` | Unit test class | `PegamentoService` (document-confirmation commands) | `BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoDocumentConfirmationTests.cs` |
| UnitTests | Modules/Pegamentos | `JobOnProductionFolderResolverTests` | Unit test class | `FakeJobOnProductionFolderResolver`, `PegamentoService` | `BA.Dmo.UnitTests\Modules\Pegamentos\JobOnProductionFolderResolverTests.cs` |
| UnitTests | Modules/Pegamentos | `FakePegamentoRepository` / `PegamentoTestSupport` / `FakeJobOnProductionFolderResolver` | Fakes + helpers | `IPegamentoRepository`, `IAppSettingsReader`, `IJobOnProductionContextLookup`, `IPegamentoPdfRenderer`, `IJobOnProductionFolderResolver` | `BA.Dmo.UnitTests\Modules\Pegamentos\` |
| UnitTests | Modules/ReparacaoExterna | `ReparacaoExternaServiceTests` | Unit test class | `ReparacaoExternaService`, `ReparacaoExternaAuthorizationGate` | `BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaServiceTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `RepairExitStatusMachineTests` | Unit test class | `RepairExitStatusMachine` | `BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairExitStatusMachineTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `RepairerCapabilityTests` | Unit test class | `ReparacaoExternaService` (repairer capability commands) | `BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairerCapabilityTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `ReparacaoExternaAuthorizationGateTests` | Unit test class | `ReparacaoExternaAuthorizationGate` | `BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaAuthorizationGateTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `FakeRepairRepository` + `ReparacaoExternaTestSupport` | Fakes + helpers | `IRepairRepository`, `IRepairUnitOfWorkFactory`, `IArmazemRepairMovementPort`, `IToolPieceResolver` | `BA.Dmo.UnitTests\Modules\ReparacaoExterna\` |
| UnitTests | Modules/ReparacaoInterna | `ReparacaoInternaServiceTests` | Unit test class | `ReparacaoInternaService`, `ReparacaoInternaAuthorizationGate` | `BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaServiceTests.cs` |
| UnitTests | Modules/ReparacaoInterna | `ReparacaoInternaDomainTests` | Unit test class | `InternalRepairRecord`, `InternalRepairRules`, `InternalRepairToolTypeCodec` | `BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaDomainTests.cs` |
| UnitTests | Modules/ReparacaoInterna | `ReparacaoInternaProductionProjectionTests` | Unit test class | `ReparacaoInternaProductionProjection` | `BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaProductionProjectionTests.cs` |
| UnitTests | Modules/ReparacaoInterna | (in `ReparacaoInternaTestSupport`) | Fakes + helpers | `IReparacaoInternaRepository`, `IRepairUnitOfWorkFactory`, `IJobOnActiveContextLookup`, `IFerramentasPieceLookup` | `BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| UnitTests | Modules/Tampoes | `TampaoServiceTests` | Unit test class | `TampaoService`, `TampaoAuthorizationGate` | `BA.Dmo.UnitTests\Modules\Tampoes\TampaoServiceTests.cs` |
| UnitTests | Modules/Tampoes | `TampaoDomainTests` | Unit test class | `TampaoConfigurationKey`, `TampaoRules` | `BA.Dmo.UnitTests\Modules\Tampoes\TampaoDomainTests.cs` |
| UnitTests | Modules/Tampoes | `TampaoMachineTests` | Unit test class | `TampaoService` (multi-machine commands) | `BA.Dmo.UnitTests\Modules\Tampoes\TampaoMachineTests.cs` |
| UnitTests | Modules/Tampoes | (in `TampaoTestSupport`) | Fakes + helper | `ITampoesUnitOfWorkFactory`, `ITampaoRepository`, `IDbUnitOfWork` | `BA.Dmo.UnitTests\Modules\Tampoes\TampaoTestSupport.cs` |
| UnitTests | Shared/Access | `AccessResolverTests` | Unit test class | `AccessResolver`, `EffectiveAccess`, `CatalogValidator` | `BA.Dmo.UnitTests\Shared\Access\AccessResolverTests.cs` |
| UnitTests | Shared/Access | `CurrentUserTests`, `ModuleCatalogTests`, `CanonicalModuleCatalogTests`, `CanonicalPageCatalogTests`, `CapabilityAndModuleDefinitionTests`, `CatalogValidatorTests`, `GrantNormalizerTests`, `NavigationServiceTests`, `ModuleCatalogMirrorSynchronizerTests` | Unit test classes | `CurrentUser`/`ICurrentUserAccessor`, `ModuleCatalog`/`ModuleDefinition`, `CanonicalModuleCatalog`, `CanonicalPageCatalog`/`PageDefinition`, `Capability`, `CatalogValidator`, `GrantNormalizer`, `NavigationService`, `ModuleCatalogMirrorSynchronizer` | `BA.Dmo.UnitTests\Shared\Access\` |
| UnitTests | Shared/Admin | `AdminUserServiceTests`, `AdminAuditAndMirrorTests`, `AdminTemplateServiceTests` + `FakeAdminRepository` | Unit test classes + fake | `AdminUserService`, `AdminAuthorizationGate`, `AdminAuditService`, `AdminMirrorService`, `AdminTemplateService`, `GrantNormalizer`, `IAdminRepository` | `BA.Dmo.UnitTests\Shared\Admin\` |
| UnitTests | Shared/Identity | `IdentityResolutionServiceTests`, `AccessTemplateGrantsParserTests`, `BootstrapAdminServiceTests` | Unit test classes | `IdentityResolutionService`, `AccessTemplateGrantsParser`, `BootstrapAdminService`, `AccessResolver` | `BA.Dmo.UnitTests\Shared\Identity\` |
| UnitTests | Shared/Kernel | `ClockTests`, `ResultTests`, `DomainErrorTests` | Unit test classes | `SystemClock`/`IClock`, `Result`, `DomainError`/`ErrorCategory` | `BA.Dmo.UnitTests\Shared\Kernel\` |
| UnitTests | Shared/Persistence | `ConcurrencyGuardTests`, `PersistenceAuthorshipTests` | Unit test classes | `ConcurrencyGuard`, `ConcurrencyConflictException`, `PersistenceAuthorship` | `BA.Dmo.UnitTests\Shared\Persistence\` |
| IntegrationTests | Access | `AdminSecurityGuardTests` | Reflection/architecture guard | `Program` assembly, `AdminUserService`, `IAdminProvisioningAdapter`, `SupabaseAdminProvisioningAdapter` | `BA.Dmo.IntegrationTests\Access\AdminSecurityGuardTests.cs` |
| IntegrationTests | Access | `CatalogCompositionGuardTests` | Reflection/architecture guard | `CatalogValidator`, `CanonicalModuleCatalog`, `CanonicalPageCatalog`, `DapperModuleCatalogMirrorRepository`, `Program` | `BA.Dmo.IntegrationTests\Access\CatalogCompositionGuardTests.cs` |
| IntegrationTests | Access | `BoquilhasWebAuthorizationTests` | Integration (WAF `BoquilhasFixture`) | `Program`, `/boquilhas`, Boquilhas API, `IBoquilhasRepository` | `BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs` |
| IntegrationTests | Access | `FakeBoquilhasWebRepository` | Fake | `IBoquilhasRepository` | `BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` |
| IntegrationTests | Access | `AdminFormAntiforgeryTests` | Integration (WAF `AfFixture`, antiforgery enforced) | `Program`, /admin Razor forms, antiforgery pipeline | `BA.Dmo.IntegrationTests\Access\AdminFormAntiforgeryTests.cs` |
| IntegrationTests | Access | `AdminWebAuthorizationTests` | Integration (WAF `AdminFixture`) | `Program`, /admin pages, admin policy | `BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs` |
| IntegrationTests | Access | `DapperAdminRepositoryProjectionTests` | Integration (ADO doubles) | `DapperAdminRepository`, `AdminUserRow`, `IDbConnectionFactory` | `BA.Dmo.IntegrationTests\Access\DapperAdminRepositoryProjectionTests.cs` |
| IntegrationTests | Access | `AdminUserListResetTests` | Integration (WAF `ResetFixture`) | `Program`, /admin/users reset, `AdminUserService` | `BA.Dmo.IntegrationTests\Access\AdminUserListResetTests.cs` |
| IntegrationTests | Access | `ShellRoutingTests` | Integration (WAF `ShellFixture`, profile-switchable) | `Program`, module routes, shell, `/jobon`, `/peso`, `/armazem` GET APIs | `BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` |
| IntegrationTests | Access | `HistoriaWebAuthorizationTests` | Integration (WAF `HistoriaFixture`) | `Program`, `/historia`, `IHistoriaRepository` | `BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs` |
| IntegrationTests | Cli | `BootstrapAdminCliTests` | Integration (CLI) | `BootstrapAdminCommand`, `SupabaseSettings` | `BA.Dmo.IntegrationTests\Cli\BootstrapAdminCliTests.cs` |
| IntegrationTests | Cli | `CliCommandPlaceholderTests` (class `CliCommandContractTests`) | Integration (CLI) | `BootstrapAdminCommand` | `BA.Dmo.IntegrationTests\Cli\CliCommandPlaceholderTests.cs` |
| IntegrationTests | Cli | `CliRoutingTests` | Integration (CLI) | `CliModeResolver`, `CliMode` | `BA.Dmo.IntegrationTests\Cli\CliRoutingTests.cs` |
| IntegrationTests | Cli | `MigrateCliTests` | Integration (CLI) | `MigrateCommand`, connection-string env vars | `BA.Dmo.IntegrationTests\Cli\MigrateCliTests.cs` |
| IntegrationTests | Controlo | `ControloProjectionGuardTests` | Static source-text guard | `DapperControloProductionContextLookup` (5 resumo families) | `BA.Dmo.IntegrationTests\Controlo\ControloProjectionGuardTests.cs` |
| IntegrationTests | Design | `DesignSystemGuardTests` | Integration (WAF `DesignFixture`) + static guards | `wwwroot/styles`, `_Layout.cshtml`, `/design-laboratorio` | `BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` |
| IntegrationTests | Design | `ShellAndCalendarGuardTests` | Integration (WAF `LabFixture`) + static guards | `wwwroot/styles|scripts`, `Pages/_Layout*`, `/design-laboratorio` | `BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs` |
| IntegrationTests | Design | `JobOnScriptSafetyGuardTests` | Static file-content guard | `wwwroot/scripts/jobon.js` | `BA.Dmo.IntegrationTests\Design\JobOnScriptSafetyGuardTests.cs` |
| IntegrationTests | Design | `ArmazemBqGuardTests` | Static source-text guard | `Pages/Armazem/Index.cshtml` type selectors (BQ yes, PU/CS no) | `BA.Dmo.IntegrationTests\Design\ArmazemBqGuardTests.cs` |
| IntegrationTests | Design | `ArmazemCorrectionGuardTests` | Static source-text guard | `Armazem/Index.cshtml` + `armazem.js` correction card, `Program.cs` `/api/armazem/corrigir-localizacao`, `ArmazemService.CorrectLocationAsync` | `BA.Dmo.IntegrationTests\Design\ArmazemCorrectionGuardTests.cs` |
| IntegrationTests | Design | `ArmazemCreateGuardTests` | Static source-text guard | two-owner create flow (Ferramentas master → Armazém Entrada), `armazem.js` recovery | `BA.Dmo.IntegrationTests\Design\ArmazemCreateGuardTests.cs` |
| IntegrationTests | Design | `ArmazemRecentMovementsGuardTests` | Static source-text guard | recent movements / consulta / histórico / programadas surfaces, `armazem-layout.css` | `BA.Dmo.IntegrationTests\Design\ArmazemRecentMovementsGuardTests.cs` |
| IntegrationTests | Design | `PesoComparisonGuardTests` | Static source-text guard | Peso comparison contract (`Pages/Peso/*`, `peso.js`, `PesoSingleFilePdfRenderer`, `PesoService`) | `BA.Dmo.IntegrationTests\Design\PesoComparisonGuardTests.cs` |
| IntegrationTests | Ferramentas | `FerramentasWebApiTests` | Integration (WAF `FerrFixture`) | `/api/ferramentas/*`, `IFerramentasRepository`, `IFerramentasRuleLookup` | `BA.Dmo.IntegrationTests\Ferramentas\FerramentasWebApiTests.cs` |
| IntegrationTests | Identity | `SupabaseAuthAdapterTests` | Integration (HTTP adapter, `FakeHttpMessageHandler`) | `SupabaseAuthAdapter` | `BA.Dmo.IntegrationTests\Identity\SupabaseAuthAdapterTests.cs` |
| IntegrationTests | Identity | `SupabaseAdminProvisioningAdapterTests` | Integration (HTTP adapter, `FakeHttpMessageHandler`) | `SupabaseAdminProvisioningAdapter` | `BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs` |
| IntegrationTests | Identity | `IdentitySecurityGuardTests` | Reflection guard | `Program` assembly, `SessionClaims`, Application assembly | `BA.Dmo.IntegrationTests\Identity\IdentitySecurityGuardTests.cs` |
| IntegrationTests | Identity | `WebAuthSessionTests` | Integration (WAF `AuthTestFixture`) | `/login`, `/logout`, session cookie, `ISupabaseAuthAdapter` | `BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` |
| IntegrationTests | Identity | `IdentityAmbiguityLandingTests` | Integration (WAF `AmbiguityFixture`) | `/login`, `/no-access`, `IInternalUserRepository` | `BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` |
| IntegrationTests | Identity | `FakeHttpMessageHandler` | Fake (HTTP handler) | `HttpMessageHandler` | `BA.Dmo.IntegrationTests\Identity\FakeHttpMessageHandler.cs` |
| IntegrationTests | Integrity | `RemediationGuardTests` | Database integration (real PostgreSQL, env-guarded) | N25_remediation.sql schema (constraints/triggers/RLS/indexes) | `BA.Dmo.IntegrationTests\Integrity\RemediationGuardTests.cs` |
| IntegrationTests | JobOn | `JobOnLandingTests` | Integration (WAF `LandingFixture`) | `/jobon` landing, `IJobOnRepository.GetHistoricalProductionsAsync` | `BA.Dmo.IntegrationTests\JobOn\JobOnLandingTests.cs` |
| IntegrationTests | JobOn | `JobOnLineColorMappingTests` | Unit test class | `JobOnLineColor` | `BA.Dmo.IntegrationTests\JobOn\JobOnLineColorMappingTests.cs` |
| IntegrationTests | JobOn | `JobOnImageWebApiTests` | Integration (WAF `ImageFixture`) | `/api/jobon/{id}/image/attach|remove`, `IArticleReferenceImageRepository` (no revision created) | `BA.Dmo.IntegrationTests\JobOn\JobOnImageWebApiTests.cs` |
| IntegrationTests | JobOn | `JobOnPdfRendererTests` | Integration test class | `JobOnPdfRenderer` (`BA.Dmo.Infrastructure.Access`) image embedding | `BA.Dmo.IntegrationTests\JobOn\JobOnPdfRendererTests.cs` |
| IntegrationTests | Migrations | `MigrationRunnerTests`, `MigrationDiscoveryTests`, `MigrationChecksumTests` | Integration (temp dir; `IDisposable`) | `MigrationRunner`, `MigrationDiscovery`, `MigrationChecksum`, `IMigrationScriptGateway` | `BA.Dmo.IntegrationTests\Migrations\` |
| IntegrationTests | Migrations | `MigrationArchitectureGuardTests` | Reflection guard | `MigrationRunner` assembly, `Program` assembly, `MigrateCommand` | `BA.Dmo.IntegrationTests\Migrations\MigrationArchitectureGuardTests.cs` |
| IntegrationTests | Migrations | `FakeMigrationGateway` | Fake | `IMigrationScriptGateway` | `BA.Dmo.IntegrationTests\Migrations\FakeMigrationGateway.cs` |
| IntegrationTests | Pegamentos | `PegamentoPdfRendererTests` | Integration test class | `PegamentoPdfRenderer` | `BA.Dmo.IntegrationTests\Pegamentos\PegamentoPdfRendererTests.cs` |
| IntegrationTests | Pegamentos | `PegamentoWebApiTests` | Integration (WAF `PegFixture`) | `/api/pegamentos/*`, `IPegamentoRepository`, `IJobOnProductionFolderResolver`, `IAppSettingsReader`, `IJobOnProductionContextLookup` | `BA.Dmo.IntegrationTests\Pegamentos\PegamentoWebApiTests.cs` |
| IntegrationTests | Persistence | `DbConnectionFactoryTests`, `DapperUnitOfWorkTests`, `PersistenceMappingsTests`, `PersistenceArchitectureGuardTests` | Integration + reflection guard | `DbConnectionFactory`/`DatabaseConnectionSettings`, `DapperUnitOfWork`/`IDbConnectionFactory`, `PersistenceMappings`/`DefaultTypeMap`, assembly dependency graph | `BA.Dmo.IntegrationTests\Persistence\` |
| IntegrationTests | Persistence | `FakeDbConnection` (+ `FakeDbTransaction`, `FakeConnectionFactory`) | Fakes | `IDbConnection`, `IDbTransaction`, `IDbConnectionFactory` | `BA.Dmo.IntegrationTests\Persistence\FakeDbConnection.cs` |
| IntegrationTests | Peso | `PesoPdfVisualCheck` | Integration (PDF-to-file visual check) | `PesoSingleFilePdfRenderer.RenderPesoFolha` | `BA.Dmo.IntegrationTests\Peso\PesoPdfVisualCheck.cs` |
| IntegrationTests | ReparacaoExterna | `ReparacaoExternaWebApiTests` | Integration (WAF `RepExtFixture`) | `/api/reparacao-externa/*`, `IRepairRepository`, `IToolPieceResolver`, `IArmazemRepairMovementPort` | `BA.Dmo.IntegrationTests\ReparacaoExterna\ReparacaoExternaWebApiTests.cs` |
| IntegrationTests | ReparacaoInterna | `ReparacaoInternaWebApiTests` | Integration (WAF `RepIntFixture`) | `/api/reparacao-interna/*`, `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, `IFerramentasPieceLookup` | `BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs` |
| IntegrationTests | Security | `NoDebugBypassGuardTests` | Reflection/file guard | Production assemblies, `Program` entry point, `Pages/Auth/Login.cshtml.cs` | `BA.Dmo.IntegrationTests\Security\NoDebugBypassGuardTests.cs` |
| IntegrationTests | Tampoes | `TampaoWebApiTests` | Integration (WAF `TampoesFixture`) | `/api/tampoes/*`, `ITampaoRepository`, `ITampoesUnitOfWorkFactory` | `BA.Dmo.IntegrationTests\Tampoes\TampaoWebApiTests.cs` |
| IntegrationTests | (root) | `IntegrationTestEnvironment` | Module initializer (env setup) | sets `ASPNETCORE_ENVIRONMENT=Testing`, `Logging__EventLog__LogLevel__Default=None` | `BA.Dmo.IntegrationTests\IntegrationTestEnvironment.cs` |

---

## 4. Unit Test Project

`AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\` — Domain + Application layer unit tests (no I/O, no DB). Targets map to [19_APPLICATION.md](19_APPLICATION.md) and the module maps.

### 4.1 Job On (module map [06_JOB_ON.md](06_JOB_ON.md))

- **`JobOnServiceTests`** — Target: `JobOnService`, `JobOnAuthorizationGate`. Groups: `Create_*`, `Duplicate_*`, `SaveRevision_*`, `Transition_*`, `Resolve_*`, `ConfirmVerification_*`, `DuplicateJobOn_*`, capability-gate denials; **reference-image path** `AttachImage_*`, `ReplaceImage_*`, `RemoveImage_*`, `AttachImage_WithUnsafeOrNonImageAsset_IsRejected`, `AttachImage_WithoutReadableReference_IsRejected`, `ImageAction_WithoutEditCapability_IsDenied`, `DuplicateJobOn_DoesNotCopyLegacyRevisionImageOwnership`, `SaveRevision_DoesNotPersistLegacyPerRevisionImageAssetId`. Uses: `FakeJobOnRepository`, `FakeJobOnUserContextRepository`, **`FakeArticleReferenceImageRepository`**, `FakeCurrentUserAccessor` (nested), `FixedClock` (nested).
- **`JobOnDomainTests`** — Target: domain `JobOn`, `JobOnLifecycleStateCodec`. `Transition_*`, `Close_*`, `Cancel_*`, `DuplicateFrom_*`, `CloneWithChanges_*`, `SaveRevision_*`, `Codec_*` (Theories).
- **`JobOnPdfTests`** — Target: `JobOnPdfService`, `JobOnService`. `GenerateAsync_*` (4 pages, reference data, sections/drop count, family grouping, calibre rows, PT characters, 404/403), notes/dates/builder mapping, `ImageProvider_ResolvesNull_WhenNoImage`, **`GenerateAsync_ConsumesReferenceImageProvider_IntoPrintProjection`** (new; uses `StubJobOnImageProvider`), `BuildFileName_ProducesCorrectFormat`. Doubles: `TestPdfRenderer`, `PdfTestIdentityAccessor`, `PdfTestClock`, `NullJobOnImageProvider`, `StubJobOnImageProvider`.
- **`JobOnVerificationGeneratorTests`** — `JobOnVerificationGenerator` occurrence generation.
- **`JobOnActivityResolverTests`** — interval/ambiguity resolution edge cases.
- **`JobOnUserContextTests`** — current-open context service methods + `JobOnLineCatalog` six-line constant.
- **`JobOnRevisionImmutabilityIntegrationTests`** — cross-module (real services over fakes): revision B never moves/reinterprets revision A Peso/Pegamento/tool context.
- Fakes: `FakeJobOnRepository` (full in-memory `IJobOnRepository` with revision-graph/duplicate support), `FakeJobOnUserContextRepository`, **`FakeArticleReferenceImageRepository`** (in-memory `IArticleReferenceImageRepository` with `Associations` dict + `AuditFacts` list; normalizes via `ArticleReferenceImageRules.NormalizeReferenceCode`).

### 4.2 Peso

- **`PesoServiceTests`** — `Approve_*`, operator permission split, `SaveReference_*`, `CreateLote_*`, `CreateControl_*`, submit/approve day-registration, hard blocks (submit-without-reading, reject-without-note), reopen revision increments, `Delete_*`, `CreateComparison_*`, `ConfirmComparisonDecisions_*`, settings future-only density, PDF filename convention, `GenerateDocument_RequiresApprovedControl`, `PrepareEmail_*`.
- **`PesoDomainTests`** — `PesoValidator`, codecs (`PesoProcesso`, `PesoRecordType`, `PesoControlState`), `ReportPathValidator`.
- **`WeightCalculatorTests`** — density lookup (Theory, 31 rows), glass-weight/volume, calote exclusion, deltas, averages.
- **`PesoControlWorkflowTests`** — `PesoControl` state machine, comparison uses approved base, `PesoCmDecisionCodec`.
- **`FakePesoRepository`** — `IPesoRepository` in-memory.

### 4.3 Armazém (module map [09_ARMAZEM.md](09_ARMAZEM.md))

- **`ArmazemServiceTests`** — `Entrada_*`, `Saida_*`, `Substituir_*`, `Consulta_*`, `Repor_*`, atomic occupation guards, failure cases. Uses `FakeArmazemRepository` (`FailAtomicWrite` switch), `FakeToolIdentityResolver`, support doubles.
- **`WarehouseStockRulesTests`** — `WarehouseLocation`/`WarehouseStockRules` position/occupancy/conflict rules.
- **`ArmazemAuthorizationGateTests`**, **`FerramentasArmazemToolIdentityResolverTests`** (CM/MF accepted, others empty, warehouse-identity mapping).
- **`ArmazemTestSupport`** — `ArmazemFixedClock`, `ArmazemFakeAuthorship`, `ArmazemCurrentUser`, `FakeFerramentasIdentityLookup`.

### 4.4 Boquilhas (module map [10_BOQUILHAS.md](10_BOQUILHAS.md))

- **`BoquilhasServiceTests`** — atomic create-with-trace, duplicate/invalid reference blocks, entrada 20→25 return + discrepancy, saida exceeding production block, closed-trace movement block, close/reopen lifecycle, audits.
- **`BqAuthorizationGateTests`**, **`BqInventoryCalculatorTests`** — reconciliation, full trace lifecycle, dispatch/repair blocks, line-change neutrality, physical inventory.
- **`BqTestSupport`** — fixed clock/authorship/user, `FakeBqUnitOfWork(Factory)`, `FakeBoquilhasRepository` + seed helpers (`SeedLote`, `SeedActiveTrace`, `SeedRepairer`).

### 4.5 Controlo (module map [07_CONTROLO.md](07_CONTROLO.md))

- **`ControloSheetServiceTests`** — production-context creation, item updates, submit/review/reopen flows, capability gating, free mode.
- **`ControloFolhaTests`** — folha creation snapshot + revision pin, submit/decide/reopen, append-only events.
- **`ControloTestSupport`** — `ControloFixedClock`, `ControloFakeAuthorship`, `FakeControloUow(Factory)`, `ControloCurrentUser`, `FakeControloSheetRepository`, `FakeControloProductionContextLookup`, `ControloTestBuilder`.

### 4.6 Ferramentas (module map [08_FERRAMENTAS.md](08_FERRAMENTAS.md))

- **`FerramentasServiceTests`** — create reference + first lote, processo-on-lote rule, duplicate blocks, no-lines validation, module gating, duplicate lote config-only, check rules, piece/condition facts, rule lookup.
- **`FerramentasDomainTests`** — `ToolReference`, `ToolLote`, `ToolCheckRule`, `PhysicalPiece`, `FerramentasToolTypeCodec` (CM/MF distinct).
- **`FerramentasUtilisationServiceTests`** — append-only readings, no-formula negative cumulative rejection.
- **`FakeFerramentasRepository`** (`FailAtomicCreate`), **`FerramentasTestSupport`** (`FixedClock`, `FakeAuthorshipAccessor`, `FakeCurrentUser`, `FakeRuleLookup`).

### 4.7 História (module map [14_HISTORIA.md](14_HISTORIA.md))

- **`HistoriaServiceTests`** — query authorization + scope forwarding, audit-view ordering/grouping, page-size validation, module gating. Contains `FakeHistoriaRepository`.
- **`HistoriaAuthorizationGateTests`** — origin-scope resolution, audit-view admin inclusion, fail-closed cases. Contains `HistoriaCurrentUser`.

### 4.8 Pegamentos

- **`PegamentoServiceTests`** — create with context (derives JobOn id), missing-components block, historical context detail, revision-scoped listing, revision anchor immutability, authorization, server-side ovalização/média.
- **`PegamentoPdfTests`** — PDF bytes + canonical filename (`PegamentoPdfFilename.Compute` asserted: `Pegamentos_202601_5447T173_B1_relatorio.pdf`), no persistence, 404/403.
- **`PegamentoMeasurementCalculatorTests`** — ovalização/média/tolerance corridor.
- **`PegamentoHistoricalRelationshipTests`** — revision-id persistence proofs 1–5.
- **`PegamentoDocumentConfirmationTests`** — server-derived final metadata, output-root/folder failure atomicity, closed-control guard, one-to-one upsert.
- **`JobOnProductionFolderResolverTests`** — configured folder resolution, confirmation attribution, no reinterpretation across revisions.
- Support: `FakeSettings`, `FixedClock`, `FakeAuthorshipAccessor`, `FakeJobOnProductionContextLookup`, `FakePegamentoPdfRenderer`, `PegamentoContextBuilder`, `FakePegamentoRepository`, `FakeJobOnProductionFolderResolver`.

### 4.9 Reparação Externa (module map [12_REPARACAO_EXTERNA.md](12_REPARACAO_EXTERNA.md))

- **`ReparacaoExternaServiceTests`** — create exit, remove-after-disposicionado block, pickup/return, repairer deactivation, line-default with inactive repairer.
- **`RepairExitStatusMachineTests`**, **`RepairerCapabilityTests`** (capability separate from line default), **`ReparacaoExternaAuthorizationGateTests`** (fail-closed).
- **`FakeRepairRepository`** (`FailItemWrite`), **`ReparacaoExternaTestSupport`** (clocks/current-user, `FakeRepairUnitOfWorkFactory`/`FakeUnitOfWork`, `FakeArmazemRepairMovementPort`, `FakeToolPieceResolver` with `Seed`).

### 4.10 Reparação Interna (module map [11_REPARACAO_INTERNA.md](11_REPARACAO_INTERNA.md))

- **`ReparacaoInternaServiceTests`** — register/line cards/corrigir/history/detail chain.
- **`ReparacaoInternaDomainTests`** — internal record/rules/tool-type codec, structural rejection, operator capture.
- **`ReparacaoInternaProductionProjectionTests`** — activation UTC / effective records.
- **`ReparacaoInternaTestSupport`** — clocks/user, `FakeReparacaoInternaUowFactory`, `FakeReparacaoInternaRepository`, `FakeJobOnActiveContextLookup` (SeedSingle/None/Ambiguous), `FakeFerramentasPieceLookup`.

### 4.11 Tampões (module map [13_TAMPOES.md](13_TAMPOES.md))

- **`TampaoServiceTests`** — adicionar/remover/estado/configuração, planning without stock reservation, cancel plan preserves balances, options deactivation, fail-closed consulta, movements filter.
- **`TampaoDomainTests`** — `TampaoConfigurationKey`, `TampaoRules` (normalization, balance transfers, transform validation).
- **`TampaoMachineTests`** — multi-machine assignment/removal, filters, detail sheet.
- **`TampaoTestSupport`** — `FakeTampoesUnitOfWorkFactory`/`FakeTampaoUnitOfWork`, `FakeTampaoRepository`, `SeedConfiguration`.

### 4.12 Shared — Access ([16_USERS_ACCESS.md](16_USERS_ACCESS.md))

`AccessResolverTests` (landing resolution, capability-constrained pages, Controlo area children, fallback/no-access), `CurrentUserTests`, `ModuleCatalogTests`, `CanonicalModuleCatalogTests`, `CanonicalPageCatalogTests` (route grammar Theories), `CapabilityAndModuleDefinitionTests`, `CatalogValidatorTests` (all violations reported), `GrantNormalizerTests`, `NavigationServiceTests`, `ModuleCatalogMirrorSynchronizerTests`.

### 4.13 Shared — Admin ([15_ADMIN.md](15_ADMIN.md))

`AdminUserServiceTests` (capability/identity gating, CRUD, templates, privileged password reset), `AdminAuditAndMirrorTests` (audit view/export, mirror save), `AdminTemplateServiceTests` (canonical JSON, invalid-grant Theories, conflict). `FakeAdminRepository`.

### 4.14 Shared — Identity ([18_LOGIN.md](18_LOGIN.md), [16_USERS_ACCESS.md](16_USERS_ACCESS.md))

`IdentityResolutionServiceTests` (authoritative resolution, fail-closed matrix), `AccessTemplateGrantsParserTests`, `BootstrapAdminServiceTests` (idempotency, recovery-link semantics, failure atomicity).

### 4.15 Shared — Kernel

`ClockTests`, `ResultTests`, `DomainErrorTests` (all eight categories, factories).

### 4.16 Shared — Persistence ([04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md))

`ConcurrencyGuardTests`, `PersistenceAuthorshipTests`.

---

## 5. Integration Test Project

`AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\` — Web + Infrastructure contract tests. Uses `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) for web hosts, and exercises Infrastructure types (Dapper, migrations, Supabase adapters, persistence) with in-memory/fake collaborators. No live Supabase or production DB except the env-guarded `RemediationGuardTests`. `IntegrationTestEnvironment` (ModuleInitializer) sets `ASPNETCORE_ENVIRONMENT=Testing` before any test runs.

### 5.1 Access ([16_USERS_ACCESS.md](16_USERS_ACCESS.md), [20_WEB.md](20_WEB.md))

- **`AdminSecurityGuardTests` / `CatalogCompositionGuardTests`** — reflection/architecture guards: privileged provisioning not reachable from admin pages, canonical capability policies not role names, no provider-specific deps in Application; canonical catalog wired at startup, catalog not in Web assembly, single landing policy, mirror port implemented in Infrastructure with U03 factory contract.
- **`AdminFormAntiforgeryTests`** (WAF `AfFixture`, antiforgery **enforced**) — token rendered, tokenless/cross-session posts rejected 400 and write nothing, tokened create/edit/apply succeed, anonymous/operator posts denied.
- **`AdminWebAuthorizationTests`** (WAF `AdminFixture`) — unauthenticated redirect, capability denial (forged post writes nothing), admin landing for `admin.gerir`, audit-view required.
- **`AdminUserListResetTests`** (WAF `ResetFixture`) — reset uses the existing service path, audits, banner; unknown-user error path.
- **`BoquilhasWebAuthorizationTests`** (WAF `BoquilhasFixture`) — anon redirect, module denial, page render, create lot + 20→25 return discrepancy, dispatch-exceeding bad request. Uses `FakeBoquilhasWebRepository`.
- **`HistoriaWebAuthorizationTests`** (WAF `HistoriaFixture`) — anon redirect, module denial, grant-scoped projection, admin events excluded without audit view.
- **`DapperAdminRepositoryProjectionTests`** — real `DapperAdminRepository` over ADO.NET doubles: `UserColumns_MaterializesAdminUserRow_WithAuthEmailNull_BeforeEnrichment` (captures SQL/disposal).
- **`ShellRoutingTests`** (WAF `ShellFixture`, 13 `UserProfile`s) — **18 tests**: `Scenario1_JobOnAndBoquilhas_LandsOnJobOn_WithDerivedHistoria`, `Scenario2_PesoOperador_CannotReachResponsavelRoutes`, `Scenario3_PesoResponsavel_IsRedirectedFromOperadorRoute`, `Scenarios4To6_ControloGrantShowsOneGlobalEntry` (Theory), `Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn`, `Scenario9_NoInternalIdentity_NoAccessSafeState_NoLoop`, `Scenario10_DeepLinkDenied_RedirectsToAuthorizedAreaWithFeedback`, `Scenario11_GrantsRemovedMidSession_ReResolvedPerRequest`, `Scenario12_TemplateDeactivated_SessionAuthenticatedWithoutAccess`, `Unauthenticated_ModuleRoutes_RedirectToLogin`, `JobOnPage_RendersTheU13Surface_InsideTheAuthorizedShell`, `JobOnPage_WithoutEditOrConfigure_HidesPrivilegedControls`, `JobOnResponsible_OpenedProduction_RendersAuthorizedIdentityPreservingReadLinks`, `JobOnUser_WithoutTargetModules_DoesNotReceiveCrossModuleReadLinks`, `Armazem_Substituir_IsAbsentFromRenderedSurface_AndHasNoEndpoint` (asserts `POST /api/armazem/substituir` → 404), `Armazem_CreateNew_IsVisibleOnlyWithFerramentasMasterAccess`, `Armazem_ConsultaWithoutFilters_ReturnsSeededCmMfBqRows`, `Armazem_MovementFeed_IsNewestFirstAndKeepsRawLotValues`. Fixture replaces `ISupabaseAuthAdapter`, `IInternalUserRepository`, `IJobOnRepository`, `IJobOnUserContextRepository`, `IPesoRepository`, `IArmazemRepository`, `IToolIdentityResolver`.

### 5.2 Cli

CLI tests invoke `Run` directly with injected environment resolvers and `StringWriter` stdout/stderr; no web server.

`BootstrapAdminCliTests` (exit-code 2 for missing config, partial-config listing, DB-config fails before provisioning), `CliCommandContractTests` (file `CliCommandPlaceholderTests.cs`), `CliRoutingTests` (operational-verb Theory ×5, non-verb fallback Theory ×3), `MigrateCliTests` (missing env fails non-zero, unusable connection, `DATABASE_URL` fallback).

### 5.3 Design ([17_DESIGN_LABORATORIO.md](17_DESIGN_LABORATORIO.md))

WAF + static-file guards. `DesignSystemGuardTests` (token files, reduced motion, semantics, canonical layout order, no legacy CSS, button state machine, no inline design CSS, laboratory page session), `ShellAndCalendarGuardTests` (single calendar, shell composition, laboratory consumes canonical calendar), `JobOnScriptSafetyGuardTests` (jobon.js `CatalogLabel` escaping via `EscHelper`).

**Static source-text guards (new in this revision):**
- `ArmazemBqGuardTests` — all five Armazém type selectors expose BQ, never PU/CS.
- `ArmazemCorrectionGuardTests` — correction card surface + dedicated auditable `/api/armazem/corrigir-localizacao` endpoint and `ArmazemService.CorrectLocationAsync` path.
- `ArmazemCreateGuardTests` — two-owner create (Ferramentas master first, Armazém Entrada second, partial-failure recovery in `armazem.js`).
- `ArmazemRecentMovementsGuardTests` — Registo/Consulta/Histórico movement-backed surfaces, no filename-only `L`-prefix leakage, `Programadas` dormant, responsive/print CSS.
- `PesoComparisonGuardTests` — comparison contract (explicit pairing/submit, per-CM glass-weight comparison, no global water/capacity comparison, `L`-prefix reserved for filename).

### 5.4 Ferramentas

`FerramentasWebApiTests` (WAF `FerrFixture`) — anonymous denial Theory ×3, search admitted, module denial, rules endpoint gated by `ferramentas.configure`.

### 5.5 Identity ([18_LOGIN.md](18_LOGIN.md))

- **`SupabaseAuthAdapterTests`** — response scenarios via `FakeHttpMessageHandler`: success, invalid credentials, 429/401/403 never "invalid credentials", 503, network failure, unconfigured/blank fail closed; asserts endpoint + `apikey` header, no secret leaks.
- **`SupabaseAdminProvisioningAdapterTests`** — email pagination, idempotent create (409/422), service-role Bearer only server-side, missing-config/network/hard failures.
- **`IdentitySecurityGuardTests`** — provisioning adapter never held by Web types, session cookie carries only auth user id, Application has no provider-specific deps.
- **`WebAuthSessionTests`** (WAF `AuthTestFixture`) — login/logout/safe-state coverage, open-redirect negative asserts, generic error no session, backend-unavailable state.
- **`IdentityAmbiguityLandingTests`** (WAF `AmbiguityFixture`) — ambiguous identity lands on plain `/no-access`, never "indisponível"; genuine repo failure still "indisponível".
- **`FakeHttpMessageHandler`** — scriptable HTTP responses.

### 5.6 Integrity (Database)

`RemediationGuardTests` — real PostgreSQL (env `BA_DMO_TEST_DATABASE`; each test skips with `[SKIP]` when absent). 10 tests: duplicate/null auth-user-id, canceled JobOn pair reissue, second-active-trace-per-lote, revision append-only, approved Peso immutability + consistency, invalid status values, late-tables RLS/grants per N12 convention, `audit_events.module_time` index. Asserts SQLSTATE `23505/23502/23514`, trigger text, `pg_trigger`/`pg_policy`/`pg_indexes` counts. Class doc assumes schema migrated N01–N25 (see §5.8 — migration family now extends to N31).
> Cross-ref: [03_MIGRATIONS.md](03_MIGRATIONS.md).

### 5.7 JobOn

- **`JobOnLandingTests`** (WAF `LandingFixture`) — landing defaults to Planeamento, calendar markers per distinct line key, list row fields.
- **`JobOnLineColorMappingTests`** — six-line stable keys (Theory ×6), unknown line null/invalid, canonical set.
- **`JobOnImageWebApiTests`** (WAF `ImageFixture`, **new**) — `AttachAndRemove_ChangeReferenceAssociation_WithoutAddingRevision`, `UnsafePath_IsRejected_AndWritesNothing` (`..\outside.jpg` → 400). Replaces `IArticleReferenceImageRepository`/`IJobOnRepository` with file-local fakes; asserts revision count unchanged.
- **`JobOnPdfRendererTests`** (**new**) — `JobOnPdfRenderer` byte-level: draws reference image exactly once (`/Im1 Do`), no image object when absent, PNG embedded with `/FlateDecode`, JPEG with `/DCTDecode`.

### 5.8 Migrations ([03_MIGRATIONS.md](03_MIGRATIONS.md))

- **`MigrationRunnerTests`** — whole-script execution, record-after-success, same-checksum skip, checksum-mismatch fail, failure stops run, canonical order, semicolons-in-strings never split.
- **`MigrationDiscoveryTests`** — canonical ordinal, determinism, family-pattern/duplicate-version/missing-dir/non-SQL rejection, **`ShippedFreshBuildFamily_IsComplete_N01ThroughN31`** (family N01_identity … N31_template_profiles_single_assignment), plus per-migration closure guards `N28_FailsClosedAndNarrowsInternalRepairTypeToCmMf`, `N29_FailsClosedAndCreatesReferenceOwnedImageAssociation`, `N30_AddsCoveringIndexForReferenceImageUpdaterForeignKey`, `N31_EnforcesSingleTemplateAndClosedProfile`.
- **`MigrationChecksumTests`** — SHA-256 FIPS vector, exact bytes, change detection.
- **`MigrationArchitectureGuardTests`** — no migration/HTTP surface in production assemblies; Web has no migration hook beyond the CLI verb.
- **`FakeMigrationGateway`** — `IMigrationScriptGateway` double with failure injection.

### 5.9 Pegamentos

`PegamentoPdfRendererTests` (`PegamentoPdfRenderer` — valid PDF header, identity/component data, no HTML/print artifacts), `PegamentoWebApiTests` (WAF `PegFixture` — anonymous Theory ×3, search admitted, module denial).

### 5.10 Persistence ([04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md))

`DbConnectionFactoryTests` (env resolution, uri/unparseable rejection without leaks, open-failure translation, cancellation), `DapperUnitOfWorkTests` (begin/commit/rollback/dispose lifecycle via `FakeDbConnection`, cancellation, scope independence), `PersistenceMappingsTests` (snake_case↔PascalCase idempotent), `PersistenceArchitectureGuardTests` (no EF/ORM, no global/static connection state, no ambient transactions, Web does not reference Npgsql, dependency graph per Plan-V3, Infrastructure does not leak into Domain). `FakeDbConnection`/`FakeDbTransaction`/`FakeConnectionFactory` doubles.

### 5.11 Peso

`PesoPdfVisualCheck` — renders a sample `PesoFolhaPdf` (comparison + CM rows) with `PesoSingleFilePdfRenderer` to `bin/Debug/net10.0/sample_peso.pdf` for manual visual check; asserts `%PDF` + length. **Data-flow web coverage for `/peso` does not exist (see §17).**

### 5.12 Reparação Externa

`ReparacaoExternaWebApiTests` (WAF `RepExtFixture`) — anonymous Theory ×4, tool search admitted, module denial.

### 5.13 Reparação Interna

`ReparacaoInternaWebApiTests` (WAF `RepIntFixture`) — anonymous Theory ×3, line cards admitted, module denial, `reparacao_interna.corrigir` capability gate.

### 5.14 Security

`NoDebugBypassGuardTests` — production assemblies contain no debug auth-bypass types; entry point is the real Program composition root; auth-path sources have no debug blocks and exactly one sign-in call site (no-op when source tree absent next to build output).

### 5.15 Tampões

`TampaoWebApiTests` (WAF `TampoesFixture`) — anonymous Theory ×3, consulta admitted, module denial.

### 5.16 Controlo (**new folder**)

`ControloProjectionGuardTests` — static source-text guard on `src\BA.Dmo.Infrastructure\Access\DapperControloProductionContextLookup.cs`: `c.family IN ('MP_CM','MF','BQ','PU','CS')` present, three-family variant absent. **Functional coverage of the lookup does not exist (see §17).**

---

## 6. Fixtures / Shared Test Infrastructure

### 6.1 `WebApplicationFactory<Program>` test-host fixtures (17)

All override `ConfigureWebHost`/`ConfigureTestServices`; clients use `AllowAutoRedirect=false` + `HandleCookies=true`. Antiforgery disabled via `IgnoreAntiforgeryTokenAttribute` except `AfFixture` (enforced).

| Fixture | File (under `BA.Dmo.IntegrationTests\`) | Replaces | Antiforgery |
|---|---|---|---|
| `AfFixture` | `Access\AdminFormAntiforgeryTests.cs` | auth/identity/`IAdminRepository`/`IModuleCatalogMirrorRepository`/`IAdminProvisioningAdapter` | **enforced** |
| `AdminFixture` | `Access\AdminWebAuthorizationTests.cs` | auth/identity/`IAdminRepository`/`IModuleCatalogMirrorRepository` | disabled |
| `ResetFixture` | `Access\AdminUserListResetTests.cs` | auth/identity/admin/provisioning/mirror | disabled |
| `BoquilhasFixture` | `Access\BoquilhasWebAuthorizationTests.cs` | auth/identity/`IBoquilhasRepository`/`IBoquilhasUnitOfWorkFactory` | disabled |
| `HistoriaFixture` | `Access\HistoriaWebAuthorizationTests.cs` | auth/identity/`IHistoriaRepository`/`IAdminRepository`/`IJobOnRepository` | disabled |
| `ShellFixture` | `Access\ShellRoutingTests.cs` | auth/identity/`IJobOnRepository`/`IJobOnUserContextRepository`/`IPesoRepository`/`IArmazemRepository`/`IToolIdentityResolver` | disabled |
| `LabFixture` | `Design\ShellAndCalendarGuardTests.cs` | auth/identity | disabled |
| `DesignFixture` | `Design\DesignSystemGuardTests.cs` | auth/identity/`IJobOnRepository` | disabled |
| `FerrFixture` | `Ferramentas\FerramentasWebApiTests.cs` | auth/identity/`IFerramentasRepository` (scoped)/`IFerramentasRuleLookup` (scoped) | disabled |
| `LandingFixture` | `JobOn\JobOnLandingTests.cs` | auth/identity/`IJobOnRepository` | disabled |
| `ImageFixture` | `JobOn\JobOnImageWebApiTests.cs` | auth/identity/`IJobOnRepository`/`IArticleReferenceImageRepository` | disabled |
| `AuthTestFixture` | `Identity\WebAuthSessionTests.cs` | `ISupabaseAuthAdapter`, `IInternalUserRepository` | disabled |
| `AmbiguityFixture` | `Identity\IdentityAmbiguityLandingTests.cs` | auth/identity | disabled |
| `PegFixture` | `Pegamentos\PegamentoWebApiTests.cs` | auth/identity/`IPegamentoRepository`/`IJobOnProductionFolderResolver`/`IAppSettingsReader`/`IJobOnProductionContextLookup` | disabled |
| `RepExtFixture` | `ReparacaoExterna\ReparacaoExternaWebApiTests.cs` | auth/identity/`IRepairRepository`/`IToolPieceResolver`/`IArmazemRepairMovementPort`/`IRepairUnitOfWorkFactory` | disabled |
| `RepIntFixture` | `ReparacaoInterna\ReparacaoInternaWebApiTests.cs` | auth/identity/`IReparacaoInternaRepository`/`IJobOnActiveContextLookup`/`IFerramentasPieceLookup`/`IRepairUnitOfWorkFactory` | disabled |
| `TampoesFixture` | `Tampoes\TampaoWebApiTests.cs` | auth/identity/`ITampaoRepository`/`ITampoesUnitOfWorkFactory` | disabled |

`ShellFixture` is profile-switchable (`UserProfile`: `BoquilhasOnly`, `JobOnResponsible`, `PesoOperador`, `PesoResponsavel`, `PegamentosOnly`, `PesoPlusPegamentos`, `AdminOnly`, `ArmazemOnly`, `ArmazemWithFerramentas`, `ReparacaoInternaOnly`, `TampoesOnly`, `NoInternalUser`, `TemplateInactive`) and is reused by `BA.Dmo.VisualHost`.

### 6.2 ADO.NET / HTTP / infra doubles

- `FakeDbConnection` / `FakeDbTransaction` / `FakeConnectionFactory` — `Persistence\FakeDbConnection.cs`.
- `DataReaderDbConnection` / `DataReaderDbCommand` / `NoParameterCollection` / `FixedReaderConnectionFactory` — `Access\DapperAdminRepositoryProjectionTests.cs` (capture `IssuedSql`, `WasDisposed`).
- `FakeHttpMessageHandler` — `Identity\FakeHttpMessageHandler.cs`.
- `FakeMigrationGateway` — `Migrations\FakeMigrationGateway.cs`.

### 6.3 Environment initializer

`IntegrationTestEnvironment.cs` — `[ModuleInitializer]` sets `ASPNETCORE_ENVIRONMENT=Testing` and `Logging__EventLog__LogLevel__Default=None` at assembly load, before any test.

### 6.4 Collection / parallelization config

No `[CollectionDefinition]`/`[Collection]`, no `xunit.runner.json`, `.runsettings`, or assembly-level parallelization configuration found. The only shared build file is `Directory.Build.props`. xUnit default parallelism therefore applies (no explicit control).

---

## 7. Test Doubles

### 7.1 In-memory repository fakes (application repository ports)

| Fake | Implements | Path (under `BA.Dmo.UnitTests\` unless noted) |
|---|---|---|
| `FakeJobOnRepository` | `IJobOnRepository` | `Modules\JobOn\FakeJobOnRepository.cs` |
| `FakeJobOnUserContextRepository` | `IJobOnUserContextRepository` | `Modules\JobOn\FakeJobOnUserContextRepository.cs` |
| `FakeArticleReferenceImageRepository` | `IArticleReferenceImageRepository` | `Modules\JobOn\FakeArticleReferenceImageRepository.cs` |
| `FakePesoRepository` | `IPesoRepository` | `Modules\Peso\FakePesoRepository.cs` |
| `FakeArmazemRepository` | `IArmazemRepository` | `Modules\Armazem\FakeArmazemRepository.cs` |
| `FakeBoquilhasRepository` | `IBoquilhasRepository` | `Modules\Boquilhas\BqTestSupport.cs` |
| `FakeFerramentasRepository` | `IFerramentasRepository` | `Modules\Ferramentas\FakeFerramentasRepository.cs` |
| `FakeHistoriaRepository` | `IHistoriaRepository` | `Modules\Historia\HistoriaServiceTests.cs` |
| `FakePegamentoRepository` | `IPegamentoRepository` | `Modules\Pegamentos\FakePegamentoRepository.cs` |
| `FakeRepairRepository` | `IRepairRepository` | `Modules\ReparacaoExterna\FakeRepairRepository.cs` |
| `FakeReparacaoInternaRepository` | `IReparacaoInternaRepository` | `Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| `FakeTampaoRepository` | `ITampaoRepository` | `Modules\Tampoes\TampaoTestSupport.cs` |
| `FakeControloSheetRepository` | `IControloSheetRepository` | `Modules\Controlo\ControloTestSupport.cs` |
| `FakeAdminRepository` | `IAdminRepository` | `Shared\Admin\FakeAdminRepository.cs` |
| `FakeBoquilhasWebRepository` | `IBoquilhasRepository` (web) | `BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` |

### 7.2 Resolver / lookup / port fakes

`FakeToolIdentityResolver` (`IToolIdentityResolver`, Armazém), `FakeFerramentasIdentityLookup`, `FakeRuleLookup` (`IFerramentasRuleLookup`), `FakeJobOnProductionContextLookup`, `FakeJobOnProductionFolderResolver` (`IJobOnProductionFolderResolver`), `FakePegamentoPdfRenderer`, `FakeToolPieceResolver` (`IToolPieceResolver`), `FakeArmazemRepairMovementPort`, `FakeJobOnActiveContextLookup`, `FakeFerramentasPieceLookup`, `FakeControloProductionContextLookup`, `FakeSettings` (`IAppSettingsReader`).

### 7.3 Unit-of-work factory / `IDbUnitOfWork` no-op doubles

`FakeBqUnitOfWork`/`FakeBqUnitOfWorkFactory`, `FakeControloUow`/`FakeControloUowFactory`, `FakeUnitOfWork`/`FakeRepairUnitOfWorkFactory` (RepExt), `FakeReparacaoInternaUnitOfWork`/`FakeReparacaoInternaUowFactory`, `FakeTampaoUnitOfWork`/`FakeTampoesUnitOfWorkFactory`, `FakeBqWebUnitOfWork`/`FakeBqWebUnitOfWorkFactory` (Boquilhas WAF).

### 7.4 Fixed clocks / authorship / current-user accessors

Per-module names in the module `*TestSupport.cs` files: Armazém (`ArmazemFixedClock`/`ArmazemFakeAuthorship`/`ArmazemCurrentUser`), Boquilhas (`Bq*`), Controlo (`Controlo*`), Ferramentas (`FixedClock`/`FakeAuthorshipAccessor`/`FakeCurrentUser`), JobOn (`FixedClock`/`PdfTestClock`/`TestClock`/`LocalFixedClock`, `FakeCurrentUserAccessor`/`PdfTestIdentityAccessor`/`LocalFakeCurrentUserAccessor`), Pegamentos (`FixedClock`/`FakeAuthorshipAccessor`), Peso (`FixedClock` nested + `FakeCurrentUserAccessor` with grant variants), RepExt (`ReparacaoExterna*`), RepInt (`ReparacaoInterna*`), Tampões (`Tampao*`), História (`HistoriaCurrentUser`), Admin/Identity (nested `FakeCurrentUserAccessor`/`FixedClock`).

### 7.5 Other test doubles

- `NoopPdfRenderer` (`IPdfRenderer`, Peso), `TestPdfRenderer` (`IJobOnPdfRenderer`), `NullJobOnImageProvider`/`StubJobOnImageProvider` (`IJobOnImageProvider`, JobOnPdfTests).
- `NoopMirror`/`FakeMirrorRepository` (`IModuleCatalogMirrorRepository`), `RecordingProvisioningAdapter`/`FakeProvisioning`/`FakeProvisioningAdapter` (`IAdminProvisioningAdapter`).
- `FakeInternalUserRepository`/`FakeIdentityRepository` (`IInternalUserRepository`), `FakeAuthAdapter` (`ISupabaseAuthAdapter` with `AuthMode`).
- `JobOnLineCatalog` (canonical six-line constant), `PegamentoContextBuilder`, `ControloTestBuilder`.
- File-local integration fakes in `JobOnImageWebApiTests`: `FakeArticleImageRepository`, `FakeJobOnRepository`, `FakeAuthAdapter`, `FakeIdentityRepository`.

> **POTENTIAL OVERLAP — NEEDS AUDIT (evidence):** near-identical nested `FakeIdentityRepository` (`IInternalUserRepository`) implementations are redeclared per WAF file — `FerramentasWebApiTests`, `TampaoWebApiTests`, `PegamentoWebApiTests`, `IdentityAmbiguityLandingTests`, `WebAuthSessionTests`, `ShellAndCalendarGuardTests`, `ReparacaoInternaWebApiTests`, `ReparacaoExternaWebApiTests`, `DesignSystemGuardTests`, `JobOnImageWebApiTests`. Likewise `FakeJobOnRepository` and `IArticleReferenceImageRepository` fakes exist both as UnitTests fakes and as file-local copies inside `JobOnImageWebApiTests`; `FakeBoquilhasWebRepository` (integration) parallels the unit `FakeBoquilhasRepository`. Same pattern, separate copies — no duplication cleanup recommended here, owner decision required.

---

## 8. Builders / Test Data Helpers

| Type | Produces | Main members | Path (under `BA.Dmo.UnitTests\`) |
|---|---|---|---|
| `PegamentoContextBuilder` | `PegamentoProductionContext` | `Complete(jobOnId, revisionId, ...)` | `Modules\Pegamentos\PegamentoTestSupport.cs` |
| `ControloTestBuilder` | `ControloSheetService` + fakes | `Build(user, now)` | `Modules\Controlo\ControloTestSupport.cs` |
| `FakeJobOnActiveContextLookup` seed helpers | `InternalRepairContext` | `SeedSingle`, `SeedNone`, `SeedAmbiguous`, `Context(...)` | `Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| `FakeBoquilhasRepository` seed helpers | `BqLote`/`BqTrace`/`BqRepairer` | `SeedLote`, `SeedActiveTrace`, `SeedRepairer` | `Modules\Boquilhas\BqTestSupport.cs` |
| `FakeTampaoRepository` seed helper | `TampaoConfiguration` | `SeedConfiguration` | `Modules\Tampoes\TampaoTestSupport.cs` |
| `FakeToolPieceResolver` seed helper | `RepairToolIdentity` | `Seed(reference, lot, number, type)` | `Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs` |

---

## 9. Database / Integration Test Mechanics

### 9.1 Single database-backed suite (`RemediationGuardTests`)

File: `Integrity\RemediationGuardTests.cs`. Direct Npgsql connections from env var `BA_DMO_TEST_DATABASE`; absent → each test prints `[SKIP]` and returns. Assumes schema migrated N01–N25 per class doc (family now extends to N31 — see §9.2). Fresh GUID keys, `ON CONFLICT DO NOTHING` seeding, no destructive teardown. Helpers: `Exec`, `CaptureSqlState`, `CaptureMessage`, `EnsureTemplateAsync`, `SeedJobWithRevisionAsync`, `SeedPesoControloAsync`. Asserts SQLSTATE `23505/23502/23514`, trigger text, `pg_trigger`/`pg_indexes`/`pg_policy` counts.

### 9.2 Migration tests (SQL execution via gateway double — no DB)

`Migrations\MigrationRunnerTests.cs`, `MigrationDiscoveryTests.cs`, `MigrationChecksumTests.cs` — temp-directory based (`Directory.CreateTempSubdirectory` + `IDisposable`). Whole-script execution, checksum (`MigrationChecksum.ComputeSha256File`) record-after-success, mismatch fail, failure stops run, canonical ordering, semicolons-in-strings safe. Discovery rejects non-family files, duplicate versions, missing directory, non-SQL files. **Shipped family bound: N01_identity … N31_template_profiles_single_assignment** (`ShippedFreshBuildFamily_IsComplete_N01ThroughN31`), with per-migration closure guards for N28–N31.

### 9.3 Persistence / connection tests (no live DB)

`Persistence\DbConnectionFactoryTests.cs` (unreachable endpoint `Host=127.0.0.1;Port=9` for failure translation + no credential leaks + cancellation), `DapperUnitOfWorkTests.cs` (transaction lifecycle via `FakeDbConnection`), `PersistenceMappingsTests.cs` (Dapper `DefaultTypeMap` snake_case↔PascalCase, idempotent).

---

## 10. Web / HTTP Test Mechanics

### 10.1 Test host

`WebApplicationFactory<Program>` (17 fixtures, §6.1); `CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true })`; services replaced in `ConfigureTestServices` (scoped with `AddScoped`, singletons with `AddSingleton`). Environment preset to `Testing` by `IntegrationTestEnvironment` ModuleInitializer. Antiforgery disabled via `IgnoreAntiforgeryTokenAttribute` except `AfFixture`.

### 10.2 Form / antiforgery mechanics

`PostFormAsync` extract-and-post helper; `AdminFormAntiforgeryTests` asserts token rendered, tokenless/cross-session posts rejected 400 writing nothing, anonymous/operator denial.

### 10.3 Directly requested HTTP targets (endpoints/routes)

Razor pages: `/login`, `/logout`, `/`, `/no-access`, `/access-denied`, `/jobon`, `/boquilhas`, `/historia`, `/peso`, `/peso/responsavel`, `/pegamentos`, `/ferramentas`, `/armazem`, `/reparacao-interna`, `/reparacao-externa`, `/tampoes`, `/controlo`, `/admin`, `/admin/users`, `/admin/users/create`, `/admin/users/edit`, `/admin/templates/edit`, `/admin/applications`, `/admin/audit`, `/design-laboratorio`.

API routes: `/api/boquilhas/lotes`, `/api/boquilhas/movements`, `/api/boquilhas/discrepancies`, `/api/ferramentas/references`, `/api/ferramentas/lotes/{id}/rules`, `/api/pegamentos/search`, `/api/pegamentos/context/{id}`, `/api/pegamentos/revision/{id}`, `/api/reparacao-externa*`, `/api/reparacao-interna*`, `/api/tampoes/consulta|movimentos|opcoes/fields`, `/api/jobon/{id}/image/attach`, `/api/jobon/{id}/image/remove`, `/api/armazem/consulta`, `/api/armazem/movimentos` (GET, functional), `/api/armazem/substituir` (asserted **absent** → 404). Armazém mutation endpoints (`/api/armazem/entrada`, `/api/armazem/saida`, `/api/armazem/corrigir-localizacao`, …) are referenced by static source guards only — no functional WAF call (see §17).

### 10.4 Response assertions

`HttpStatusCode` checks (302/403/400/200/404), trusted redirect-location asserts (open-redirect negatives), `Set-Cookie` session presence with cookie round-trip, `HtmlDecode` body/banner checks, `System.Text.Json` JSON decoding.

### 10.5 HTTP adapter tests (Supabase)

`SupabaseAuthAdapterTests`/`SupabaseAdminProvisioningAdapterTests` drive adapters via `FakeHttpMessageHandler`; assert exact URIs, `apikey`/Bearer headers, status→`ErrorCategory` mapping, idempotent duplicates, pagination, no secret leaks.

### 10.6 Visual host

`BA.Dmo.VisualHost\Program.cs` — Kestrel host over `ShellFixture`, profile + port CLI arguments, test-only login, `await Task.Delay(Timeout.Infinite)`.

---

## 11. Assertion / Mocking Patterns

- **xUnit** (`Assert.*`) throughout; no FluentAssertions/NUnit/MSTest.
- **No mocking library** — all doubles hand-written (see §7). Only dynamic-object use: `ExpandoObject` inside `FakeJobOnRepository.DuplicateAtomicallyAsync` (row hydration, not mocking).
- Exceptions asserted directly: `ConcurrencyConflictException`, `SchemaMigrationRequiredException`, `AmbiguousIdentityException`, `MigrationChecksumMismatchException`, `MigrationDiscoveryException`, `MigrationExecutionException`, `DatabaseConnectionException`, `ArmazemLocationOccupiedException`.
- Result/error-code pattern: `result.IsSuccess/IsFailure` + `result.Error.Category` (`Forbidden`, `ValidationError`, `DomainConflict`, `NotFound`, `BackendUnavailable`, `ConcurrencyConflict`, `Unauthorized`) / `Code` (`JOBON_NOT_FOUND`, `PESO_CONTROL_NO_READING`, `BQ_DUPLICATE_LOT`, `FERRAMENTAS_DUPLICATE_REFERENCE`, `REPEXT_TYPE_SCOPE`, `REPINT_OPERATOR_REQUIRED`, `TAMPAO_NEGATIVE_BALANCE`, `ARMZ_LOCATION_CODE`, `ADMIN_SELF_LOCKOUT`, `PEGAMENTO_OUTPUT_ROOT_MISSING`).

---

## 12. Parameterized / Conditional Tests

### 12.1 Parameterized tests (`[Theory]` + `[InlineData]` only; no `[MemberData]`/`[ClassData]`)

Codec/dictionary round-trips (`JobOnDomainTests.Codec_*`, `PesoDomainTests`, `WeightCalculatorTests.LookupDensity` 31 rows, `DomainErrorTests`, `CapabilityAndModuleDefinitionTests`, `CanonicalPageCatalogTests.RouteGrammar_*`, `AccessTemplateGrantsParserTests`, `TampaoDomainTests`, `ReparacaoInternaDomainTests.Create_StructurallyInvalid_IsARejection`), per-endpoint anonymous-denial rows (Ferramentas/Pegamentos/RepExt/RepInt/Tampões WAF), CLI routing (`OperationalVerbs_AreDistinguished` ×5, `NonVerbLeadingArgument_FallsBackToWebStartup` ×3), admin template validation (×3), antiforgery tokenless posts (×3), `Scenarios4To6_ControloGrantShowsOneGlobalEntry`, `JobOnLineColorMappingTests` (×6 rows).

### 12.2 Conditional / skipped tests

No `[Fact(Skip=…)]`, `[Trait]`, `[Category]`, `[Explicit]`, or conditional-skip attributes (verified: zero matches). Environment-guarded behavior: `RemediationGuardTests` (skip when `BA_DMO_TEST_DATABASE` absent), `NoDebugBypassGuardTests.AuthPath_Sources_HaveNoDebugBlocks_AndExactlyOneSignInCallSite` (no-op when source tree absent).

---

## 13. Target-to-Test Index

| Production Target | Test Class(es) | Test Project |
|---|---|---|
| `JobOnService` | `JobOnServiceTests`, `JobOnUserContextTests`, `JobOnPdfTests`, `JobOnRevisionImmutabilityIntegrationTests` | UnitTests |
| `JobOn` (domain) / `JobOnVerificationGenerator` / `JobOnActivityResolver` | `JobOnDomainTests`, `JobOnVerificationGeneratorTests`, `JobOnActivityResolverTests` | UnitTests |
| `JobOnPdfService` / `IJobOnImageProvider` | `JobOnPdfTests` | UnitTests |
| `JobOnPdfRenderer` | `JobOnPdfRendererTests` (+ `JobOnPdfTests` via service, renderer double) | IntegrationTests / UnitTests |
| `IArticleReferenceImageRepository` flow | `JobOnServiceTests`, `JobOnImageWebApiTests` (in-memory fakes) | UnitTests + IntegrationTests |
| `PesoService` / `PesoControl` / weight rules | `PesoServiceTests`, `PesoControlWorkflowTests`, `PesoDomainTests`, `WeightCalculatorTests` | UnitTests |
| `PesoSingleFilePdfRenderer` | `PesoPdfVisualCheck`, `PesoComparisonGuardTests` (static) | IntegrationTests |
| `ArmazemService` / rules / resolver | `ArmazemServiceTests`, `WarehouseStockRulesTests`, `ArmazemAuthorizationGateTests`, `FerramentasArmazemToolIdentityResolverTests` | UnitTests |
| Armazém web surface (GET) | `ShellRoutingTests` (+ `ArmazemRecentMovementsGuardTests`/`ArmazemCreateGuardTests`/`ArmazemCorrectionGuardTests`/`ArmazemBqGuardTests` static) | IntegrationTests |
| `BoquilhasService` / `BqRules` | `BoquilhasServiceTests`, `BqAuthorizationGateTests`, `BqInventoryCalculatorTests`; web: `BoquilhasWebAuthorizationTests` | UnitTests + IntegrationTests |
| `ControloSheetService` / `ControloFolha` | `ControloSheetServiceTests`, `ControloFolhaTests` | UnitTests |
| `DapperControloProductionContextLookup` | `ControloProjectionGuardTests` (static only) | IntegrationTests |
| `FerramentasService` / domain | `FerramentasServiceTests`, `FerramentasUtilisationServiceTests`, `FerramentasDomainTests`; web: `FerramentasWebApiTests` | UnitTests + IntegrationTests |
| `HistoriaService` / gate | `HistoriaServiceTests`, `HistoriaAuthorizationGateTests`; web: `HistoriaWebAuthorizationTests` | UnitTests + IntegrationTests |
| `PegamentoService` / PDF / calculator | `PegamentoServiceTests`, `PegamentoPdfTests`, `PegamentoHistoricalRelationshipTests`, `PegamentoDocumentConfirmationTests`, `JobOnProductionFolderResolverTests`, `PegamentoMeasurementCalculatorTests`; web: `PegamentoWebApiTests`, `PegamentoPdfRendererTests` | UnitTests + IntegrationTests |
| `ReparacaoExternaService` / machine / gate | `ReparacaoExternaServiceTests`, `RepairExitStatusMachineTests`, `RepairerCapabilityTests`, `ReparacaoExternaAuthorizationGateTests`; web: `ReparacaoExternaWebApiTests` | UnitTests + IntegrationTests |
| `ReparacaoInternaService` / domain / projection | `ReparacaoInternaServiceTests`, `ReparacaoInternaDomainTests`, `ReparacaoInternaProductionProjectionTests`; web: `ReparacaoInternaWebApiTests` | UnitTests + IntegrationTests |
| `TampaoService` / domain | `TampaoServiceTests`, `TampaoDomainTests`, `TampaoMachineTests`; web: `TampaoWebApiTests` | UnitTests + IntegrationTests |
| Access catalog / resolver / nav | `AccessResolverTests`, `CurrentUserTests`, `ModuleCatalogTests`, `CanonicalModuleCatalogTests`, `CanonicalPageCatalogTests`, `CapabilityAndModuleDefinitionTests`, `CatalogValidatorTests`, `GrantNormalizerTests`, `NavigationServiceTests`, `ModuleCatalogMirrorSynchronizerTests` | UnitTests |
| Admin services | `AdminUserServiceTests`, `AdminAuditAndMirrorTests`, `AdminTemplateServiceTests`; web: `AdminSecurityGuardTests`, `AdminFormAntiforgeryTests`, `AdminWebAuthorizationTests`, `AdminUserListResetTests`, `CatalogCompositionGuardTests` | UnitTests + IntegrationTests |
| Identity services / adapters / session | `IdentityResolutionServiceTests`, `AccessTemplateGrantsParserTests`, `BootstrapAdminServiceTests`; `SupabaseAuthAdapterTests`, `SupabaseAdminProvisioningAdapterTests`, `IdentitySecurityGuardTests`, `WebAuthSessionTests`, `IdentityAmbiguityLandingTests` | UnitTests + IntegrationTests |
| Kernel / Persistence unit | `ClockTests`, `ResultTests`, `DomainErrorTests`, `ConcurrencyGuardTests`, `PersistenceAuthorshipTests` | UnitTests |
| `DapperAdminRepository` | `DapperAdminRepositoryProjectionTests` | IntegrationTests |
| Migration engine / family | `MigrationRunnerTests`, `MigrationDiscoveryTests`, `MigrationChecksumTests`, `MigrationArchitectureGuardTests` | IntegrationTests |
| N25_remediation.sql schema | `RemediationGuardTests` | IntegrationTests |
| `DbConnectionFactory` / `DapperUnitOfWork` / `PersistenceMappings` / dependency graph | `DbConnectionFactoryTests`, `DapperUnitOfWorkTests`, `PersistenceMappingsTests`, `PersistenceArchitectureGuardTests` | IntegrationTests |
| CLI commands | `BootstrapAdminCliTests`, `CliCommandContractTests`, `CliRoutingTests`, `MigrateCliTests` | IntegrationTests |
| Design-system static assets / scripts | `DesignSystemGuardTests`, `ShellAndCalendarGuardTests`, `JobOnScriptSafetyGuardTests` | IntegrationTests |
| Shell / module routes (WAF) | `ShellRoutingTests` (+ visual reuse via `BA.Dmo.VisualHost`) | IntegrationTests |
| Production auth path (no debug bypass) | `NoDebugBypassGuardTests` | IntegrationTests |

---

## 14. Module / Area Test Index

| Module / Area | Test Classes |
|---|---|
| Job On | `JobOnServiceTests`, `JobOnDomainTests`, `JobOnPdfTests`, `JobOnVerificationGeneratorTests`, `JobOnActivityResolverTests`, `JobOnUserContextTests`, `JobOnRevisionImmutabilityIntegrationTests` (Unit); `JobOnLandingTests`, `JobOnLineColorMappingTests`, `JobOnImageWebApiTests`, `JobOnPdfRendererTests` (Integration) |
| Peso | `PesoServiceTests`, `PesoDomainTests`, `WeightCalculatorTests`, `PesoControlWorkflowTests` (Unit); `PesoPdfVisualCheck`, `PesoComparisonGuardTests` (Integration) |
| Armazém | `ArmazemServiceTests`, `WarehouseStockRulesTests`, `ArmazemAuthorizationGateTests`, `FerramentasArmazemToolIdentityResolverTests` (Unit); `ArmazemBqGuardTests`, `ArmazemCorrectionGuardTests`, `ArmazemCreateGuardTests`, `ArmazemRecentMovementsGuardTests` (Integration, static) |
| Boquilhas | `BoquilhasServiceTests`, `BqAuthorizationGateTests`, `BqInventoryCalculatorTests` (Unit); `BoquilhasWebAuthorizationTests` (Integration) |
| Controlo | `ControloSheetServiceTests`, `ControloFolhaTests` (Unit); `ControloProjectionGuardTests` (Integration, static) |
| Ferramentas | `FerramentasServiceTests`, `FerramentasDomainTests`, `FerramentasUtilisationServiceTests` (Unit); `FerramentasWebApiTests` (Integration) |
| História | `HistoriaServiceTests`, `HistoriaAuthorizationGateTests` (Unit); `HistoriaWebAuthorizationTests` (Integration) |
| Pegamentos | `PegamentoServiceTests`, `PegamentoPdfTests`, `PegamentoMeasurementCalculatorTests`, `PegamentoHistoricalRelationshipTests`, `PegamentoDocumentConfirmationTests`, `JobOnProductionFolderResolverTests` (Unit); `PegamentoWebApiTests`, `PegamentoPdfRendererTests` (Integration) |
| Reparação Externa | `ReparacaoExternaServiceTests`, `RepairExitStatusMachineTests`, `RepairerCapabilityTests`, `ReparacaoExternaAuthorizationGateTests` (Unit); `ReparacaoExternaWebApiTests` (Integration) |
| Reparação Interna | `ReparacaoInternaServiceTests`, `ReparacaoInternaDomainTests`, `ReparacaoInternaProductionProjectionTests` (Unit); `ReparacaoInternaWebApiTests` (Integration) |
| Tampões | `TampaoServiceTests`, `TampaoDomainTests`, `TampaoMachineTests` (Unit); `TampaoWebApiTests` (Integration) |
| Admin / Access | `AccessResolverTests`, `ModuleCatalogTests`, `CanonicalModuleCatalogTests`, `CanonicalPageCatalogTests`, `CapabilityAndModuleDefinitionTests`, `CatalogValidatorTests`, `GrantNormalizerTests`, `NavigationServiceTests`, `ModuleCatalogMirrorSynchronizerTests`, `CurrentUserTests`; `AdminUserServiceTests`, `AdminAuditAndMirrorTests`, `AdminTemplateServiceTests` (Unit); `AdminSecurityGuardTests`, `CatalogCompositionGuardTests`, `AdminFormAntiforgeryTests`, `AdminWebAuthorizationTests`, `AdminUserListResetTests`, `ShellRoutingTests` (Integration) |
| Identity / Auth | `IdentityResolutionServiceTests`, `AccessTemplateGrantsParserTests`, `BootstrapAdminServiceTests` (Unit); `SupabaseAuthAdapterTests`, `SupabaseAdminProvisioningAdapterTests`, `IdentitySecurityGuardTests`, `WebAuthSessionTests`, `IdentityAmbiguityLandingTests` (Integration) |
| Kernel | `ClockTests`, `ResultTests`, `DomainErrorTests` |
| Persistence | `ConcurrencyGuardTests`, `PersistenceAuthorshipTests` (Unit); `DbConnectionFactoryTests`, `DapperUnitOfWorkTests`, `PersistenceMappingsTests`, `PersistenceArchitectureGuardTests`, `DapperAdminRepositoryProjectionTests` (Integration) |
| Migrations | `MigrationRunnerTests`, `MigrationDiscoveryTests`, `MigrationChecksumTests`, `MigrationArchitectureGuardTests` |
| Database / Integrity | `RemediationGuardTests` |
| Web / Shell / Design | `ShellRoutingTests`, `BoquilhasWebAuthorizationTests`, `HistoriaWebAuthorizationTests`, `AdminWebAuthorizationTests`, `AdminFormAntiforgeryTests`, `AdminUserListResetTests`, `FerramentasWebApiTests`, `ReparacaoExternaWebApiTests`, `ReparacaoInternaWebApiTests`, `TampaoWebApiTests`, `PegamentoWebApiTests`, `JobOnLandingTests`, `JobOnImageWebApiTests` (Integration); `DesignSystemGuardTests`, `ShellAndCalendarGuardTests`, `JobOnScriptSafetyGuardTests`, `ArmazemBqGuardTests`, `ArmazemCorrectionGuardTests`, `ArmazemCreateGuardTests`, `ArmazemRecentMovementsGuardTests`, `PesoComparisonGuardTests` (static) |
| CLI | `BootstrapAdminCliTests`, `CliCommandContractTests` (`CliCommandPlaceholderTests.cs`), `CliRoutingTests`, `MigrateCliTests` |
| PDF / documents | `JobOnPdfTests`, `JobOnPdfRendererTests`, `PegamentoPdfTests`, `PegamentoPdfRendererTests`, `PesoPdfVisualCheck` |
| Visual / manual host | `BA.Dmo.VisualHost\Program.cs` (Kestrel host over `ShellFixture`) |

---

## 15. Count Summary by Project

Counting rules:

- **Test classes** = classes containing at least one `[Fact]`/`[Theory]` (fixture/DTO/helper-only classes excluded).
- **Test methods** = one per `[Fact]`/`[Theory]` attribute occurrence (`[InlineData]` rows are not separate methods).
- **Fixtures** = the 17 `WebApplicationFactory<Program>` test-host fixture classes.
- **Helpers / Test Doubles** = distinct hand-written fake/stub/builder/current-user/clock/authorship/UoW/HTTP/ADO.NET double classes (approximate).

| Project | Source Files | Test Classes | Test Methods | Fixtures/Helpers |
|---|---|---|:---:|:---:|---:|
| `BA.Dmo.UnitTests` | 81 | 62 | 547 (523 Fact + 24 Theory) | fakes/helpers/doubles (no web fixtures) |
| `BA.Dmo.IntegrationTests` | 53 | 48 | 248 (238 Fact + 10 Theory) | 17 web fixtures + fakes/infra doubles |
| `BA.Dmo.VisualHost` | 1 | — (not a test project) | — | 1 executable host |
| **Total** | **135** | **110** | **795** | **17 web fixtures + ~120 helpers/doubles** |

Helper/double estimate (~120) counts fake/stub/builder/current-user/clock/authorship/UoW/HTTP/ADO.NET/seed classes across both projects (nested doubles counted once per name; DTOs and web fixtures excluded). Figures re-derived this revision from an attribute scan of the current tree.

---

## 16. Count Summary by Area

| Area | Test Classes | Test Methods |
|---|---|---|
| Job On (unit, incl. revision immutability + image path) | 7 | ~95 |
| Job On (integration: landing, line-color, images, PDF renderer) | 4 | ~14 |
| Peso (unit) | 4 | ~50 |
| Peso (integration: visual check + static comparison guard) | 2 | ~4 |
| Armazém (unit) | 4 | ~25 |
| Armazém (integration: static Design guards) | 4 | ~11 |
| Boquilhas (unit + web) | 4 | ~25 |
| Controlo (unit + static guard) | 3 | ~13 |
| Ferramentas (unit + web) | 4 | ~30 |
| História (unit + web) | 3 | ~13 |
| Pegamentos (unit + web) | 8 | ~45 |
| Reparação Externa (unit + web) | 5 | ~35 |
| Reparação Interna (unit + web) | 4 | ~30 |
| Tampões (unit + web) | 4 | ~35 |
| Shared (Access/Admin/Identity/Kernel/Persistence unit) | 21 | ~200 |
| Integration — Web/Shell/Auth-z (routes, antiforgery, admin, shell) | 8 | ~45 |
| Integration — Design/static guards | 8 | ~24 |
| Integration — Identity/Auth (Supabase + session) | 5 | ~40 |
| Integration — Migrations | 4 | ~23 |
| Integration — Persistence | 4 | ~27 |
| Integration — Database/Integrity | 1 | 10 |
| Integration — CLI | 4 | ~12 |
| Integration — API endpoints (Ferramentas/RepExt/RepInt/Tampões/Pegamentos) | 5 | ~25 |

Area counts are approximate translations of the verified test files/methods; exact totals are the project totals in §15.

---

## 17. COVERAGE GAP — NEEDS REVIEW

Mandated evidence-based records: production paths with no visible test coverage (or only static text guarding). Nothing below is fixed here and no deletion is implied.

| # | Production path | Evidence of absence / limited coverage |
|---|---|---|
| 1 | `FerramentasRepairToolPieceResolver` — `src\BA.Dmo.Application\Modules\ReparacaoExterna\FerramentasRepairToolPieceResolver.cs` (real `IToolPieceResolver`: CM/MF type mapping, delegation to `IFerramentasPieceLookup`) | **Zero references in the entire test tree** (grep `FerramentasRepairToolPieceResolver` → no match). RepExt unit + WAF tests inject `FakeToolPieceResolver` only. The CM/MF/BQ mapping and resolver delegation are never exercised. |
| 2 | `DapperArticleReferenceImageRepository` — `src\BA.Dmo.Infrastructure\Access\DapperArticleReferenceImageRepository.cs` (upsert + audit-insert + delete with affected-count guard for `article_reference_images`, N29/N30 migrations) | **Zero references in the test tree** (grep → no match). Image association tests (`JobOnServiceTests`, `JobOnImageWebApiTests`) use in-memory fakes; the Dapper SQL, atomicity and audit-snapshot path is never executed by a test. |
| 3 | `DapperControloProductionContextLookup` — `src\BA.Dmo.Infrastructure\Access\DapperControloProductionContextLookup.cs` (5-family resumo projection SQL + hydration) | Only `ControloProjectionGuardTests` (static `File.ReadAllText` string asserts on the SQL family list). No functional test executes the lookup against any connection/double; no hydration test. |
| 4 | Peso web data flows (`/peso`, `/peso/responsavel`, Peso API) | No dedicated WAF/functional test drives Peso flows (approve, comparison, documents, settings). Coverage today: shell-layout GETs in `ShellRoutingTests` (Scenarios 2/3) and `WebAuthSessionTests`, extensive `PesoService*` unit tests, static `PesoComparisonGuardTests`, and `PesoPdfVisualCheck` (renderer only). No Peso WAF fixture with `IPesoRepository` data assertions exists. |
| 5 | Controlo web surface | No WAF test at all for `/controlo` or any Controlo API/page flow; only `ControloSheetServiceTests`/`ControloFolhaTests` (unit) + static `ControloProjectionGuardTests`. Integration tree has no other Controlo entry. |
| 6 | Armazém mutation endpoints (`/api/armazem/entrada`, `/api/armazem/saida`, `/api/armazem/corrigir-localizacao`, …) | No functional WAF call; only static source guards (`ArmazemCreateGuardTests`, `ArmazemCorrectionGuardTests` assert Program.cs/armazem.js strings) plus service-level unit tests. `ShellRoutingTests` covers only Armazém **GET** (`/api/armazem/consulta`, `/api/armazem/movimentos`) and asserts `/api/armazem/substituir` → 404. |

Notes:

- `PegamentoPdfFilename.Compute` is **covered indirectly** (filename asserted verbatim in `PegamentoPdfTests` and `PegamentoDocumentConfirmationTests`) — not a gap.
- `ArticleReferenceImageRules` is covered indirectly through service-layer image tests (unsafe-path rejection, reference extraction) — no dedicated unit class, acceptable engagement noted for owner awareness.
- **POTENTIAL OVERLAP — NEEDS AUDIT:** per-file duplicated nested fakes (see §7.5); distinct but parallel `FakeBoquilhasWebRepository` vs `FakeBoquilhasRepository`, and Unit-vs-file-local `FakeJobOnRepository`/`IArticleReferenceImageRepository` fakes.
- **UNKNOWN / OWNER DECISION REQUIRED:** nothing else flagged; the rest of the inventory matches the current tree (`CONFIRMED CURRENT`).

---

## 18. Source Locations

All exact paths are listed in §3, §4 and §5. The three project files are:

- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\BA.Dmo.UnitTests.csproj`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\BA.Dmo.IntegrationTests.csproj`
- `AI-CONTEXT\docs\tests\BA.Dmo.VisualHost\BA.Dmo.VisualHost.csproj`

Shared build settings: `D:\BA-DMO\Directory.Build.props` (no `global.json` in the repository). Solution references: `BA-DMO.sln`, `tests` solution folder `{0AB3BF05-4346-4AA6-1389-037BE0695223}` containing the three projects above.

---

## Sources Verified

Primary evidence (all current test source, read/audited from disk this revision):

- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\` (81 `.cs` files — full class inventory + attribute scan; full reads of new/updated JobOn image + PDF files)
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\` (53 `.cs` files — full class inventory + attribute scan; full reads of all new files: `ControloProjectionGuardTests`, `Design\{ArmazemBq,ArmazemCorrection,ArmazemCreate,ArmazemRecentMovements,PesoComparison}GuardTests`, `JobOn\{JobOnImageWebApiTests,JobOnPdfRendererTests}`, `IntegrationTestEnvironment.cs`, `ShellRoutingTests`)
- `AI-CONTEXT\docs\tests\BA.Dmo.VisualHost\Program.cs` + `BA.Dmo.VisualHost.csproj`
- Test `.csproj` files (`BA.Dmo.UnitTests.csproj`, `BA.Dmo.IntegrationTests.csproj`), `Directory.Build.props`; `global.json` — **not present** in the repository (old map's claim removed)
- `BA-DMO.sln` (tests solution-folder references)
- Production cross-checks for gap evidence: `src\BA.Dmo.Application\Modules\ReparacaoExterna\FerramentasRepairToolPieceResolver.cs`, `src\BA.Dmo.Infrastructure\Access\DapperArticleReferenceImageRepository.cs`, `DapperControloProductionContextLookup.cs`, `src\BA.Dmo.Application\Modules\JobOn\ArticleReferenceImage.cs`, `src\BA.Dmo.Application\Modules\Pegamentos\PegamentoPdfFilename.cs`, `src\BA.Dmo.Application\Shared\Shell\IShellService.cs`

Project targets referenced in tests: `src\BA.Dmo.Domain`, `src\BA.Dmo.Application`, `src\BA.Dmo.Infrastructure`, `src\BA.Dmo.Web`.

Registry reference: [00_INDEX.md](00_INDEX.md). Not used as test evidence: other domain/design maps (01/02), Design/SOT, historical pass logs. Related maps used for cross-references only: [03_MIGRATIONS.md](03_MIGRATIONS.md), [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md), [19_APPLICATION.md](19_APPLICATION.md), [20_WEB.md](20_WEB.md), module maps 06–18.

**Scope disclaimer:** inventory + location, with evidence-based `COVERAGE GAP — NEEDS REVIEW` records (§17). No coverage-quality judgment beyond those records; no fixes or deletions are proposed.