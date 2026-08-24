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
17. [Source Locations](#17-source-locations)
18. [Sources Verified](#sources-verified)

---

## 1. Purpose

This is the pure technical **TESTS** transversal map (MAP-05) of the BA DMO codebase.

Mapped:

- test projects and `.csproj` definitions;
- test source files, test classes, test methods;
- fixtures, shared test infrastructure;
- fakes / stubs / test doubles;
- test data builders / helpers;
- database and web/HTTP test mechanics;
- direct test-to-target references visible in test code;
- exact source locations.

Per the mapping contract, this document is **inventory + location only**. It does not judge coverage quality, identify missing tests, recommend tests, explain business workflows, or reconcile against Design/SOT.

---

## 2. Test Project Structure

### 2.1 Project level facts

| Fact | Value |
|---|---|
| Root | `D:\BA-DMO-CODEX-CLEAN` |
| SLN | `BA-DMO.sln` |
| Central build settings | `D:\BA-DMO-CODEX-CLEAN\Directory.Build.props` |
| Target framework (all projects) | `net10.0` (from `Directory.Build.props`) |
| SDK / roll-forward | `10.0.400`, `latestPatch` (`global.json`) |
| Shared compile settings | `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `NeutralLanguage=pt-PT` |

Both test projects set `<IsPackable>false</IsPackable>` and declare a global `<Using Include="Xunit" />`, so xUnit assert helpers are available file-wide without explicit usings.

### 2.2 Test projects

| Test Project | Path | Framework | Source Files | Main Areas |
|---|---|---|---:|---|
| `BA.Dmo.UnitTests` | `tests\BA.Dmo.UnitTests\` | net10.0 | 80 | Domain + Application unit tests, no I/O |
| `BA.Dmo.IntegrationTests` | `tests\BA.Dmo.IntegrationTests\` | net10.0 | 44 | Web + Infrastructure contract tests |

**Total test source files:** 124.

Excluded from all counts: `bin\`, `obj\`, build/test-results and coverage output (not source-controlled test code).

### 2.3 Project references — `BA.Dmo.UnitTests.csproj`

`D:\BA-DMO-CODEX-CLEAN\tests\BA.Dmo.UnitTests\BA.Dmo.UnitTests.csproj`

- Package references:
  - `coverlet.collector` 6.0.4
  - `Microsoft.NET.Test.Sdk` 17.14.1
  - `xunit` 2.9.3
  - `xunit.runner.visualstudio` 3.1.4
- Project references:
  - `src\BA.Dmo.Domain\BA.Dmo.Domain.csproj`
  - `src\BA.Dmo.Application\BA.Dmo.Application.csproj`

### 2.4 Project references — `BA.Dmo.IntegrationTests.csproj`

`D:\BA-DMO-CODEX-CLEAN\tests\BA.Dmo.IntegrationTests\BA.Dmo.IntegrationTests.csproj`

- Package references:
  - `coverlet.collector` 6.0.4
  - `Microsoft.AspNetCore.Mvc.Testing` 10.0.11
  - `Microsoft.NET.Test.Sdk` 17.14.1
  - `xunit` 2.9.3
  - `xunit.runner.visualstudio` 3.1.4
- Project references:
  - `src\BA.Dmo.Web\BA.Dmo.Web.csproj`
  - `src\BA.Dmo.Infrastructure\BA.Dmo.Infrastructure.csproj`

### 2.5 Test folders (top-level)

`BA.Dmo.UnitTests`:

- `Modules\Armazem`
- `Modules\Boquilhas`
- `Modules\Controlo`
- `Modules\Ferramentas`
- `Modules\Historia`
- `Modules\JobOn`
- `Modules\Pegamentos`
- `Modules\Peso`
- `Modules\ReparacaoExterna`
- `Modules\ReparacaoInterna`
- `Modules\Tampoes`
- `Shared\Access`
- `Shared\Admin`
- `Shared\Identity`
- `Shared\Kernel`
- `Shared\Persistence`

`BA.Dmo.IntegrationTests`:

- `Access`
- `Cli`
- `Design`
- `Ferramentas`
- `Identity`
- `Integrity`
- `JobOn`
- `Migrations`
- `Pegamentos`
- `Persistence`
- `Peso`
- `ReparacaoExterna`
- `ReparacaoInterna`
- `Security`
- `Tampoes`

---

## 3. Global Test Inventory

Direct targets named below are the production types imported and exercised by each test class.

| Project | Area / Folder | File / Class | Kind | Direct Target | Path |
|---|---|---|---|---|---|
| UnitTests | Modules/JobOn | `JobOnServiceTests` | Unit test class | `JobOnService`, `JobOnAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnServiceTests.cs` |
| UnitTests | Modules/JobOn | `JobOnDomainTests` | Unit test class | `BA.Dmo.Domain.Modules.JobOn.JobOn`, `JobOnLifecycleStateCodec` | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnDomainTests.cs` |
| UnitTests | Modules/JobOn | `JobOnPdfTests` | Unit test class | `JobOnPdfService`, `JobOnService` | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnPdfTests.cs` |
| UnitTests | Modules/JobOn | `JobOnVerificationGeneratorTests` | Unit test class | `JobOnVerificationGenerator` | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnVerificationGeneratorTests.cs` |
| UnitTests | Modules/JobOn | `JobOnActivityResolverTests` | Unit test class | `JobOnActivityResolver` | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnActivityResolverTests.cs` |
| UnitTests | Modules/JobOn | `JobOnUserContextTests` | Unit test class | `JobOnService` (current-open context) | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnUserContextTests.cs` |
| UnitTests | Modules/JobOn | `JobOnRevisionImmutabilityIntegrationTests` | Unit-project integration test class | `JobOnService`, `PesoService`, `PegamentoService` | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnRevisionImmutabilityIntegrationTests.cs` |
| UnitTests | Modules/JobOn | `FakeJobOnRepository` | Fake | `IJobOnRepository` | `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnRepository.cs` |
| UnitTests | Modules/JobOn | `FakeJobOnUserContextRepository` | Fake | `IJobOnUserContextRepository` | `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnUserContextRepository.cs` |
| UnitTests | Modules/Peso | `PesoServiceTests` | Unit test class | `PesoService`, `PesoAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Peso\PesoServiceTests.cs` |
| UnitTests | Modules/Peso | `PesoDomainTests` | Unit test class | `PesoValidator`, `PesoProcessoCodec`, `PesoRecordTypeCodec`, `PesoControlStateCodec`, `ReportPathValidator` | `tests\BA.Dmo.UnitTests\Modules\Peso\PesoDomainTests.cs` |
| UnitTests | Modules/Peso | `WeightCalculatorTests` | Unit test class | `WeightCalculator`, `PesoModuleCatalog` | `tests\BA.Dmo.UnitTests\Modules\Peso\WeightCalculatorTests.cs` |
| UnitTests | Modules/Peso | `PesoControlWorkflowTests` | Unit test class | `PesoControl`, `PesoValidator`, `PesoCmDecisionCodec` | `tests\BA.Dmo.UnitTests\Modules\Peso\PesoControlWorkflowTests.cs` |
| UnitTests | Modules/Peso | `FakePesoRepository` | Fake | `IPesoRepository` | `tests\BA.Dmo.UnitTests\Modules\Peso\FakePesoRepository.cs` |
| UnitTests | Modules/Armazem | `ArmazemServiceTests` | Unit test class | `ArmazemService`, `ArmazemAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Armazem\ArmazemServiceTests.cs` |
| UnitTests | Modules/Armazem | `WarehouseStockRulesTests` | Unit test class | `WarehouseLocation`, `WarehouseStockRules` | `tests\BA.Dmo.UnitTests\Modules\Armazem\WarehouseStockRulesTests.cs` |
| UnitTests | Modules/Armazem | `ArmazemAuthorizationGateTests` | Unit test class | `ArmazemAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Armazem\ArmazemAuthorizationGateTests.cs` |
| UnitTests | Modules/Armazem | `FerramentasArmazemToolIdentityResolverTests` | Unit test class | `FerramentasArmazemToolIdentityResolver` | `tests\BA.Dmo.UnitTests\Modules\Armazem\FerramentasArmazemToolIdentityResolverTests.cs` |
| UnitTests | Modules/Armazem | `FakeArmazemRepository` | Fake | `IArmazemRepository` | `tests\BA.Dmo.UnitTests\Modules\Armazem\FakeArmazemRepository.cs` |
| UnitTests | Modules/Armazem | `FakeToolIdentityResolver` | Fake | `IToolIdentityResolver` | `tests\BA.Dmo.UnitTests\Modules\Armazem\FakeToolIdentityResolver.cs` |
| UnitTests | Modules/Armazem | (in `ArmazemTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `ICurrentUserAccessor`, `IFerramentasIdentityLookup` | `tests\BA.Dmo.UnitTests\Modules\Armazem\ArmazemTestSupport.cs` |
| UnitTests | Modules/Boquilhas | `BoquilhasServiceTests` | Unit test class | `BoquilhasService`, `BqAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BoquilhasServiceTests.cs` |
| UnitTests | Modules/Boquilhas | `BqAuthorizationGateTests` | Unit test class | `BqAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqAuthorizationGateTests.cs` |
| UnitTests | Modules/Boquilhas | `BqInventoryCalculatorTests` | Unit test class | `BqInventoryCalculator`, `BqRules` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqInventoryCalculatorTests.cs` |
| UnitTests | Modules/Boquilhas | (in `BqTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `ICurrentUserAccessor`, `IBoquilhasUnitOfWorkFactory`, `IBoquilhasRepository`, `IDbUnitOfWork` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs` |
| UnitTests | Modules/Controlo | `ControloSheetServiceTests` | Unit test class | `ControloSheetService`, `ControloSheetAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloSheetServiceTests.cs` |
| UnitTests | Modules/Controlo | `ControloFolhaTests` | Unit test class | `ControloFolha` | `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloFolhaTests.cs` |
| UnitTests | Modules/Controlo | (in `ControloTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `IRepairUnitOfWorkFactory`, `IControloSheetRepository`, `IControloProductionContextLookup`, `ControloSheetService` | `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs` |
| UnitTests | Modules/Ferramentas | `FerramentasServiceTests` | Unit test class | `FerramentasService`, `FerramentasAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasServiceTests.cs` |
| UnitTests | Modules/Ferramentas | `FerramentasDomainTests` | Unit test class | `ToolReference`, `ToolLote`, `ToolCheckRule`, `PhysicalPiece`, `FerramentasToolTypeCodec` | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasDomainTests.cs` |
| UnitTests | Modules/Ferramentas | `FerramentasUtilisationServiceTests` | Unit test class | `FerramentasService` (utilisation commands) | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasUtilisationServiceTests.cs` |
| UnitTests | Modules/Ferramentas | `FakeFerramentasRepository` | Fake | `IFerramentasRepository` | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FakeFerramentasRepository.cs` |
| UnitTests | Modules/Ferramentas | (in `FerramentasTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `ICurrentUserAccessor`, `IFerramentasRuleLookup` | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasTestSupport.cs` |
| UnitTests | Modules/Historia | `HistoriaServiceTests` | Unit test class | `HistoriaService`, `HistoriaAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs` |
| UnitTests | Modules/Historia | `HistoriaAuthorizationGateTests` | Unit test class | `HistoriaAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaAuthorizationGateTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoServiceTests` | Unit test class | `PegamentoService`, `PegamentoAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoServiceTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoPdfTests` | Unit test class | `PegamentoPdfService` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoPdfTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoMeasurementCalculatorTests` | Unit test class | `PegamentoMeasurementCalculator` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoMeasurementCalculatorTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoHistoricalRelationshipTests` | Unit test class | `PegamentoService` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoHistoricalRelationshipTests.cs` |
| UnitTests | Modules/Pegamentos | `PegamentoDocumentConfirmationTests` | Unit test class | `PegamentoService` (document-confirmation commands) | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoDocumentConfirmationTests.cs` |
| UnitTests | Modules/Pegamentos | `JobOnProductionFolderResolverTests` | Unit test class | `FakeJobOnProductionFolderResolver`, `PegamentoService` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\JobOnProductionFolderResolverTests.cs` |
| UnitTests | Modules/Pegamentos | `FakePegamentoRepository` | Fake | `IPegamentoRepository` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakePegamentoRepository.cs` |
| UnitTests | Modules/Pegamentos | (in `PegamentoTestSupport`) | Test helper | `IAppSettingsReader`, `IClock`, `IPersistenceAuthorshipAccessor`, `IJobOnProductionContextLookup`, `IPegamentoPdfRenderer`, `PegamentoProductionContext` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoTestSupport.cs` |
| UnitTests | Modules/Pegamentos | `FakeJobOnProductionFolderResolver` | Fake | `IJobOnProductionFolderResolver` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakeJobOnProductionFolderResolver.cs` |
| UnitTests | Modules/ReparacaoExterna | `ReparacaoExternaServiceTests` | Unit test class | `ReparacaoExternaService`, `ReparacaoExternaAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaServiceTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `RepairExitStatusMachineTests` | Unit test class | `RepairExitStatusMachine` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairExitStatusMachineTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `RepairerCapabilityTests` | Unit test class | `ReparacaoExternaService` (repairer capability commands) | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairerCapabilityTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `ReparacaoExternaAuthorizationGateTests` | Unit test class | `ReparacaoExternaAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaAuthorizationGateTests.cs` |
| UnitTests | Modules/ReparacaoExterna | `FakeRepairRepository` | Fake | `IRepairRepository` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\FakeRepairRepository.cs` |
| UnitTests | Modules/ReparacaoExterna | (in `ReparacaoExternaTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `ICurrentUserAccessor`, `IRepairUnitOfWorkFactory`, `IArmazemRepairMovementPort`, `IToolPieceResolver`, `IDbUnitOfWork` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs` |
| UnitTests | Modules/ReparacaoInterna | `ReparacaoInternaServiceTests` | Unit test class | `ReparacaoInternaService`, `ReparacaoInternaAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaServiceTests.cs` |
| UnitTests | Modules/ReparacaoInterna | `ReparacaoInternaDomainTests` | Unit test class | `InternalRepairRecord`, `InternalRepairRules`, `InternalRepairToolTypeCodec` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaDomainTests.cs` |
| UnitTests | Modules/ReparacaoInterna | `ReparacaoInternaProductionProjectionTests` | Unit test class | `ReparacaoInternaProductionProjection` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaProductionProjectionTests.cs` |
| UnitTests | Modules/ReparacaoInterna | (in `ReparacaoInternaTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `ICurrentUserAccessor`, `IRepairUnitOfWorkFactory`, `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, `IFerramentasPieceLookup`, `IDbUnitOfWork` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| UnitTests | Modules/Tampoes | `TampaoServiceTests` | Unit test class | `TampaoService`, `TampaoAuthorizationGate` | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoServiceTests.cs` |
| UnitTests | Modules/Tampoes | `TampaoDomainTests` | Unit test class | `TampaoConfigurationKey`, `TampaoRules` | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoDomainTests.cs` |
| UnitTests | Modules/Tampoes | `TampaoMachineTests` | Unit test class | `TampaoService` (multi-machine commands) | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoMachineTests.cs` |
| UnitTests | Modules/Tampoes | (in `TampaoTestSupport`) | Test helper | `IClock`, `IPersistenceAuthorshipAccessor`, `ICurrentUserAccessor`, `ITampoesUnitOfWorkFactory`, `ITampaoRepository`, `IDbUnitOfWork` | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoTestSupport.cs` |
| UnitTests | Shared/Access | `AccessResolverTests` | Unit test class | `AccessResolver`, `EffectiveAccess`, `CatalogValidator` | `tests\BA.Dmo.UnitTests\Shared\Access\AccessResolverTests.cs` |
| UnitTests | Shared/Access | `CurrentUserTests` | Unit test class | `CurrentUser`, `ICurrentUserAccessor` | `tests\BA.Dmo.UnitTests\Shared\Access\CurrentUserTests.cs` |
| UnitTests | Shared/Access | `ModuleCatalogTests` | Unit test class | `ModuleCatalog`, `ModuleDefinition` | `tests\BA.Dmo.UnitTests\Shared\Access\ModuleCatalogTests.cs` |
| UnitTests | Shared/Access | `CanonicalModuleCatalogTests` | Unit test class | `CanonicalModuleCatalog` | `tests\BA.Dmo.UnitTests\Shared\Access\CanonicalModuleCatalogTests.cs` |
| UnitTests | Shared/Access | `CanonicalPageCatalogTests` | Unit test class | `CanonicalPageCatalog`, `PageDefinition` | `tests\BA.Dmo.UnitTests\Shared\Access\CanonicalPageCatalogTests.cs` |
| UnitTests | Shared/Access | `CapabilityAndModuleDefinitionTests` | Unit test class | `Capability`, `ModuleDefinition` | `tests\BA.Dmo.UnitTests\Shared\Access\CapabilityAndModuleDefinitionTests.cs` |
| UnitTests | Shared/Access | `CatalogValidatorTests` | Unit test class | `CatalogValidator` | `tests\BA.Dmo.UnitTests\Shared\Access\CatalogValidatorTests.cs` |
| UnitTests | Shared/Access | `GrantNormalizerTests` | Unit test class | `GrantNormalizer` | `tests\BA.Dmo.UnitTests\Shared\Access\GrantNormalizerTests.cs` |
| UnitTests | Shared/Access | `NavigationServiceTests` | Unit test class | `NavigationService`, `AccessResolver` | `tests\BA.Dmo.UnitTests\Shared\Access\NavigationServiceTests.cs` |
| UnitTests | Shared/Access | `ModuleCatalogMirrorSynchronizerTests` | Unit test class | `ModuleCatalogMirrorSynchronizer` | `tests\BA.Dmo.UnitTests\Shared\Access\ModuleCatalogMirrorSynchronizerTests.cs` |
| UnitTests | Shared/Admin | `AdminUserServiceTests` | Unit test class | `AdminUserService`, `AdminAuthorizationGate` | `tests\BA.Dmo.UnitTests\Shared\Admin\AdminUserServiceTests.cs` |
| UnitTests | Shared/Admin | `AdminAuditAndMirrorTests` | Unit test class | `AdminAuditService`, `AdminMirrorService` | `tests\BA.Dmo.UnitTests\Shared\Admin\AdminAuditAndMirrorTests.cs` |
| UnitTests | Shared/Admin | `AdminTemplateServiceTests` | Unit test class | `AdminTemplateService`, `GrantNormalizer` | `tests\BA.Dmo.UnitTests\Shared\Admin\AdminTemplateServiceTests.cs` |
| UnitTests | Shared/Admin | `FakeAdminRepository` | Fake | `IAdminRepository` | `tests\BA.Dmo.UnitTests\Shared\Admin\FakeAdminRepository.cs` |
| UnitTests | Shared/Identity | `IdentityResolutionServiceTests` | Unit test class | `IdentityResolutionService`, `AccessResolver` | `tests\BA.Dmo.UnitTests\Shared\Identity\IdentityResolutionServiceTests.cs` |
| UnitTests | Shared/Identity | `AccessTemplateGrantsParserTests` | Unit test class | `AccessTemplateGrantsParser` | `tests\BA.Dmo.UnitTests\Shared\Identity\AccessTemplateGrantsParserTests.cs` |
| UnitTests | Shared/Identity | `BootstrapAdminServiceTests` | Unit test class | `BootstrapAdminService` | `tests\BA.Dmo.UnitTests\Shared\Identity\BootstrapAdminServiceTests.cs` |
| UnitTests | Shared/Kernel | `ClockTests` | Unit test class | `SystemClock`, `IClock` | `tests\BA.Dmo.UnitTests\Shared\Kernel\ClockTests.cs` |
| UnitTests | Shared/Kernel | `ResultTests` | Unit test class | `Result` | `tests\BA.Dmo.UnitTests\Shared\Kernel\ResultTests.cs` |
| UnitTests | Shared/Kernel | `DomainErrorTests` | Unit test class | `DomainError`, `ErrorCategory` | `tests\BA.Dmo.UnitTests\Shared\Kernel\DomainErrorTests.cs` |
| UnitTests | Shared/Persistence | `ConcurrencyGuardTests` | Unit test class | `ConcurrencyGuard`, `ConcurrencyConflictException` | `tests\BA.Dmo.UnitTests\Shared\Persistence\ConcurrencyGuardTests.cs` |
| UnitTests | Shared/Persistence | `PersistenceAuthorshipTests` | Unit test class | `PersistenceAuthorship`, `IPersistenceAuthorshipAccessor` | `tests\BA.Dmo.UnitTests\Shared\Persistence\PersistenceAuthorshipTests.cs` |
| IntegrationTests | Access | `AdminSecurityGuardTests` | Integration test class | `Program` assembly, `AdminUserService`, `IAdminProvisioningAdapter`, `SupabaseAdminProvisioningAdapter` | `tests\BA.Dmo.IntegrationTests\Access\AdminSecurityGuardTests.cs` |
| IntegrationTests | Access | `CatalogCompositionGuardTests` | Integration test class | `CatalogValidator`, `CanonicalModuleCatalog`, `CanonicalPageCatalog`, `DapperModuleCatalogMirrorRepository`, `Program` | `tests\BA.Dmo.IntegrationTests\Access\CatalogCompositionGuardTests.cs` |
| IntegrationTests | Access | `BoquilhasWebAuthorizationTests` | Integration test class (WAF) | `/boquilhas`, `Boquilhas` API, `IBoquilhasRepository` | `tests\BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs` |
| IntegrationTests | Access | `FakeBoquilhasWebRepository` | Fake | `IBoquilhasRepository` | `tests\BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` |
| IntegrationTests | Access | `AdminFormAntiforgeryTests` | Integration test class (WAF) | `Program`, /admin Razor pages, antiforgery pipeline | `tests\BA.Dmo.IntegrationTests\Access\AdminFormAntiforgeryTests.cs` |
| IntegrationTests | Access | `AdminWebAuthorizationTests` | Integration test class (WAF) | `Program`, /admin pages, admin policy | `tests\BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs` |
| IntegrationTests | Access | `DapperAdminRepositoryProjectionTests` | Integration test class | `DapperAdminRepository`, `AdminUserRow`, `IDbConnectionFactory` | `tests\BA.Dmo.IntegrationTests\Access\DapperAdminRepositoryProjectionTests.cs` |
| IntegrationTests | Access | `AdminUserListResetTests` | Integration test class (WAF) | `Program`, /admin/users reset, `AdminUserService` | `tests\BA.Dmo.IntegrationTests\Access\AdminUserListResetTests.cs` |
| IntegrationTests | Access | `ShellRoutingTests` | Integration test class (WAF) | `Program`, module routes, `/jobon`, `/boquilhas`, `/peso`… | `tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` |
| IntegrationTests | Access | `HistoriaWebAuthorizationTests` | Integration test class (WAF) | `Program`, `/historia`, `IHistoriaRepository` | `tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs` |
| IntegrationTests | Cli | `BootstrapAdminCliTests` | Unit test class (CLI) | `BootstrapAdminCommand`, `SupabaseSettings` | `tests\BA.Dmo.IntegrationTests\Cli\BootstrapAdminCliTests.cs` |
| IntegrationTests | Cli | `CliCommandPlaceholderTests` (`CliCommandContractTests`) | Unit test class (CLI) | `BootstrapAdminCommand` | `tests\BA.Dmo.IntegrationTests\Cli\CliCommandPlaceholderTests.cs` |
| IntegrationTests | Cli | `CliRoutingTests` | Unit test class (CLI) | `CliModeResolver`, `CliMode` | `tests\BA.Dmo.IntegrationTests\Cli\CliRoutingTests.cs` |
| IntegrationTests | Cli | `MigrateCliTests` | Unit test class (CLI) | `MigrateCommand`, connection-string env vars | `tests\BA.Dmo.IntegrationTests\Cli\MigrateCliTests.cs` |
| IntegrationTests | Design | `DesignSystemGuardTests` | Integration test class (WAF) | `src/BA.Dmo.Web/wwwroot/styles`, `_Layout.cshtml`, `/design-laboratorio` | `tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` |
| IntegrationTests | Design | `ShellAndCalendarGuardTests` | Integration test class (WAF) | `wwwroot/styles|scripts|Pages/_Layout*`, `/design-laboratorio` | `tests\BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs` |
| IntegrationTests | Design | `JobOnScriptSafetyGuardTests` | Unit test class (file guard) | `wwwroot/scripts/jobon.js` | `tests\BA.Dmo.IntegrationTests\Design\JobOnScriptSafetyGuardTests.cs` |
| IntegrationTests | Ferramentas | `FerramentasWebApiTests` | Integration test class (WAF) | `/api/ferramentas/*`, `IFerramentasRepository`, `IFerramentasRuleLookup` | `tests\BA.Dmo.IntegrationTests\Ferramentas\FerramentasWebApiTests.cs` |
| IntegrationTests | Identity | `SupabaseAuthAdapterTests` | Integration test class | `SupabaseAuthAdapter` | `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAuthAdapterTests.cs` |
| IntegrationTests | Identity | `SupabaseAdminProvisioningAdapterTests` | Integration test class | `SupabaseAdminProvisioningAdapter` | `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs` |
| IntegrationTests | Identity | `IdentitySecurityGuardTests` | Integration test class (reflection guard) | `Program` assembly, `SessionClaims`, Application assembly | `tests\BA.Dmo.IntegrationTests\Identity\IdentitySecurityGuardTests.cs` |
| IntegrationTests | Identity | `WebAuthSessionTests` | Integration test class (WAF) | `/login`, `/logout`, session cookie, `ISupabaseAuthAdapter` | `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` |
| IntegrationTests | Identity | `IdentityAmbiguityLandingTests` | Integration test class (WAF) | `/login`, `/no-access`, `IInternalUserRepository` | `tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` |
| IntegrationTests | Identity | `FakeHttpMessageHandler` | Fake (HTTP handler) | `HttpMessageHandler` | `tests\BA.Dmo.IntegrationTests\Identity\FakeHttpMessageHandler.cs` |
| IntegrationTests | Integrity | `RemediationGuardTests` | Database integration test class | N25_remediation.sql schema, PostgreSQL | `tests\BA.Dmo.IntegrationTests\Integrity\RemediationGuardTests.cs` |
| IntegrationTests | JobOn | `JobOnLandingTests` | Integration test class (WAF) | `/jobon` landing, `IJobOnRepository` | `tests\BA.Dmo.IntegrationTests\JobOn\JobOnLandingTests.cs` |
| IntegrationTests | JobOn | `JobOnLineColorMappingTests` | Unit test class | `JobOnLineColor` | `tests\BA.Dmo.IntegrationTests\JobOn\JobOnLineColorMappingTests.cs` |
| IntegrationTests | Migrations | `MigrationRunnerTests` | Integration test class | `MigrationRunner`, `IMigrationScriptGateway` | `tests\BA.Dmo.IntegrationTests\Migrations\MigrationRunnerTests.cs` |
| IntegrationTests | Migrations | `MigrationDiscoveryTests` | Integration test class | `MigrationDiscovery` | `tests\BA.Dmo.IntegrationTests\Migrations\MigrationDiscoveryTests.cs` |
| IntegrationTests | Migrations | `MigrationChecksumTests` | Integration test class | `MigrationChecksum` | `tests\BA.Dmo.IntegrationTests\Migrations\MigrationChecksumTests.cs` |
| IntegrationTests | Migrations | `MigrationArchitectureGuardTests` | Integration test class (reflection guard) | `MigrationRunner`, `Program` assembly | `tests\BA.Dmo.IntegrationTests\Migrations\MigrationArchitectureGuardTests.cs` |
| IntegrationTests | Migrations | `FakeMigrationGateway` | Fake | `IMigrationScriptGateway` | `tests\BA.Dmo.IntegrationTests\Migrations\FakeMigrationGateway.cs` |
| IntegrationTests | Pegamentos | `PegamentoPdfRendererTests` | Integration test class | `PegamentoPdfRenderer` | `tests\BA.Dmo.IntegrationTests\Pegamentos\PegamentoPdfRendererTests.cs` |
| IntegrationTests | Pegamentos | `PegamentoWebApiTests` | Integration test class (WAF) | `/api/pegamentos/*`, `IPegamentoRepository` | `tests\BA.Dmo.IntegrationTests\Pegamentos\PegamentoWebApiTests.cs` |
| IntegrationTests | Persistence | `DbConnectionFactoryTests` | Integration test class | `DbConnectionFactory`, `DatabaseConnectionSettings` | `tests\BA.Dmo.IntegrationTests\Persistence\DbConnectionFactoryTests.cs` |
| IntegrationTests | Persistence | `DapperUnitOfWorkTests` | Integration test class | `DapperUnitOfWork`, `IDbConnectionFactory` | `tests\BA.Dmo.IntegrationTests\Persistence\DapperUnitOfWorkTests.cs` |
| IntegrationTests | Persistence | `PersistenceMappingsTests` | Integration test class | `PersistenceMappings`, `DefaultTypeMap` | `tests\BA.Dmo.IntegrationTests\Persistence\PersistenceMappingsTests.cs` |
| IntegrationTests | Persistence | `PersistenceArchitectureGuardTests` | Integration test class (reflection guard) | Domain/Application/Infrastructure/Web assemblies | `tests\BA.Dmo.IntegrationTests\Persistence\PersistenceArchitectureGuardTests.cs` |
| IntegrationTests | Persistence | `FakeDbConnection` (+ `FakeDbTransaction`, `FakeConnectionFactory`) | Fake | `IDbConnection`, `IDbTransaction`, `IDbConnectionFactory` | `tests\BA.Dmo.IntegrationTests\Persistence\FakeDbConnection.cs` |
| IntegrationTests | Peso | `PesoPdfVisualCheck` | Integration test class | `PesoSingleFilePdfRenderer` | `tests\BA.Dmo.IntegrationTests\Peso\PesoPdfVisualCheck.cs` |
| IntegrationTests | ReparacaoExterna | `ReparacaoExternaWebApiTests` | Integration test class (WAF) | `/api/reparacao-externa/*`, `IRepairRepository` | `tests\BA.Dmo.IntegrationTests\ReparacaoExterna\ReparacaoExternaWebApiTests.cs` |
| IntegrationTests | ReparacaoInterna | `ReparacaoInternaWebApiTests` | Integration test class (WAF) | `/api/reparacao-interna/*`, `IReparacaoInternaRepository` | `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs` |
| IntegrationTests | Security | `NoDebugBypassGuardTests` | Integration test class (reflection/file guard) | Production assemblies, `/Pages/Auth/Login.cshtml.cs` | `tests\BA.Dmo.IntegrationTests\Security\NoDebugBypassGuardTests.cs` |
| IntegrationTests | Tampoes | `TampaoWebApiTests` | Integration test class (WAF) | `/api/tampoes/*`, `ITampaoRepository` | `tests\BA.Dmo.IntegrationTests\Tampoes\TampaoWebApiTests.cs` |

---

## 4. Unit Test Project

`tests\BA.Dmo.UnitTests\` — Domain + Application layer unit tests (no I/O, no DB).

### 4.1 Job On

#### `JobOnServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnServiceTests.cs`
Target: `JobOnService`, `JobOnAuthorizationGate`
Tests (method groups): `Create_*`, `Duplicate_*`, `SaveRevision_*`, `Transition_*`, `Resolve_*`, `ConfirmVerification_*`, `AttachImage_*/ReplaceImage_*/RemoveImage_*`, `DuplicateJobOn_*`, capability-gate denials.
Uses: `FakeJobOnRepository`, `FakeJobOnUserContextRepository`, `FakeCurrentUserAccessor` (nested), `FixedClock` (nested).
Asserts: `Assert.True/False/Equal/Empty/Single/Contains`, `Assert.Single`, JSON `JsonDocument`.

#### `JobOnDomainTests`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnDomainTests.cs`
Target: `BA.Dmo.Domain.Modules.JobOn.JobOn`, `JobOnLifecycleStateCodec`
Tests: `Transition_*`, `Close_*`, `Cancel_*`, `DuplicateFrom_*`, `CloneWithChanges_*`, `SaveRevision_*`, `Codec_*`.
Asserts: `Assert.Throws`, `Assert.Equal`, parameterized `[Theory]/[InlineData]` for codec.

#### `JobOnPdfTests`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnPdfTests.cs`
Target: `JobOnPdfService`, `JobOnService`
Tests: `GenerateAsync_ReturnsValidPdf_WithFourPages`, `GenerateAsync_IncludesReferenceInData`, `GenerateAsync_MapsSectionsAndDropCount`, `GenerateAsync_GroupsComponentsByFamily`, `GenerateAsync_MapsCalibreRows`, `GenerateAsync_PreservesPortugueseCharacters`, `GenerateAsync_ReturnsNotFound_ForMissingJobOn`, `GenerateAsync_ReturnsForbidden_WhenUnauthorized`, `GenerateAsync_IncludesGeneralNotes`, `GenerateAsync_MapsPlannedDates`, `GenerateAsync_ComponentFieldsAccessible`, `GenerateAsync_EmptyComponentsAreNull`, `ImageProvider_ResolvesNull_WhenNoImage`, `BuildFileName_ProducesCorrectFormat`.
Uses: `TestPdfRenderer` (`IJobOnPdfRenderer`), `PdfTestIdentityAccessor`, `PdfTestClock`, `NullJobOnImageProvider` (nested doubles).
Asserts: `Assert.True/NotEmpty/Equal/StartsWith/EndsWith/Contains`, PDF-byte header check.

#### `JobOnVerificationGeneratorTests`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnVerificationGeneratorTests.cs`
Target: `JobOnVerificationGenerator`
Tests: `Generate_OneRule_YieldsOnePendenteOccurrence`, `Generate_MultipleRules_YieldsOnePerRule`, `Generate_EmptyRules_YieldsNone`, `Generate_NullRules_YieldsNone`, `Generate_EmptyRuleId_IsSkipped`, `Generate_RecordsCreationTimestamp`.
Asserts: `Assert.Single`, `Assert.Equal`, `Assert.Empty`, `Assert.All`.

#### `JobOnActivityResolverTests`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnActivityResolverTests.cs`
Target: `JobOnActivityResolver`
Tests: `Resolve_SingleCandidateInsideInterval_ReturnsSingle`, `Resolve_NoCandidate_ReturnsNone`, `Resolve_EmptyCandidates_ReturnsNone`, `Resolve_AtBeforeStart_ReturnsNone`, `Resolve_AtOnEndBoundary_IsExcluded`, `Resolve_TwoOverlappingCandidates_ReturnsAmbiguous`, `Resolve_NullEnd_UsesNextPlannedStartAsUpperBound`, `Resolve_LastCandidateWithNullEnd_IsUnbounded`, `Resolve_NonActiveStates_AreExcluded`.

#### `JobOnUserContextTests`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnUserContextTests.cs`
Target: `JobOnService` (current-open context methods), `JobOnLineCatalog` (local helper)
Tests: `SetCurrentOpen_*`, `GetCurrentOpen_*`, `AUser_WithoutEdit_CanStillOpenAndReadPlanningContext`, `CanonicalSixLines_AreSupported_AndDistinct`.

#### `JobOnRevisionImmutabilityIntegrationTests`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnRevisionImmutabilityIntegrationTests.cs`
Kind: Unit-project cross-module integration test class (real services over in-memory repositories).
Target: `JobOnService`, `PesoService`, `PegamentoService`
Test: `RevB_DoesNotMoveOrReinterpret_RevA_Peso_Pegamento_OrToolContext`.
Uses: `FakeJobOnRepository`, `FakePesoRepository`, `FakePegamentoRepository`, `FakeJobOnProductionContextLookup`, `PegamentoContextBuilder`; file-local fakes `JobOnActor`, `PesoOperador`, `PegFakeAuthorship`, `TestClock`.

#### `FakeJobOnRepository`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnRepository.cs`
Implements: `IJobOnRepository`. In-memory stores for job-ons, revisions, components, fields, rows, verifications, audit events; records lifecycle/revision/verification updates; exposes `DuplicateAtomicallyAsync`, `SaveRevisionGraphAsync`, historical production summary.

#### `FakeJobOnUserContextRepository`
File: `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnUserContextRepository.cs`
Implements: `IJobOnUserContextRepository`. Records actor id + `JobOnUserCurrent`.

### 4.2 Peso

#### `PesoServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Peso\PesoServiceTests.cs`
Target: `PesoService`, `PesoAuthorizationGate`
Tests: `Approve_*`, `Operador_CanManageNonApprovalOps_ButNotApprove`, `SaveReference_*`, `CreateLote_*`, `CreateControl_InheritsJobOnContext_AndPinsRevision`, `SubmitThenApprove_RegistersDayApproval`, `Submit_WithoutReading_IsHardBlocked`, `Reject_WithoutNote_IsHardBlocked`, `Reopen_Approved_IncrementsRevision`, `Delete_*`, `CreateComparison_*`, `ConfirmComparisonDecisions_*`, `SaveSettings_ChangesDensity_ForFutureOnly_NotHistorical`, `PdfFilenameConvention_MatchesConfirmedReference`, `GenerateDocument_RequiresApprovedControl`, `PrepareEmail_*`.
Uses: `FakePesoRepository`, `FakeJobOnRepository`, `FakeCurrentUserAccessor` (nested), `NoopPdfRenderer`, `FixedClock`.
Asserts: `Assert.Equal/True/False`, error-code matching.

#### `PesoDomainTests`
File: `tests\BA.Dmo.UnitTests\Modules\Peso\PesoDomainTests.cs`
Target: `PesoValidator`, `PesoProcessoCodec`, `PesoRecordTypeCodec`, `PesoControlStateCodec`, `ReportPathValidator`
Tests: `ValidateReference_*`, `ValidateLote_*`, `ProcessoCodec_RoundTrips`, `ReportPath_*`, `RecordType_Codec_RoundTrips`, `Status_Codec_RoundTrips`.

#### `WeightCalculatorTests`
File: `tests\BA.Dmo.UnitTests\Modules\Peso\WeightCalculatorTests.cs`
Target: `WeightCalculator`, `PesoModuleCatalog`
Tests: `LookupDensity_IntTemperature5To35_ReturnsExactDensity` (Theory, 31 InlineData rows), `LookupDensity_RoundsToNearestInteger_AwayFromZero`, `LookupDensity_BelowMinimum_IsDomainError`, `LookupDensity_AboveMaximum_IsDomainError`, `EstimateGlassWeight_*`, `VolumeFromWeight_*`, `CaloteVolume_DoesNotInfluenceGlassWeight`, `DeltasVs_*`, `GlassAverage_*`.

#### `PesoControlWorkflowTests`
File: `tests\BA.Dmo.UnitTests\Modules\Peso\PesoControlWorkflowTests.cs`
Target: `PesoControl`, `PesoValidator`, `PesoCmDecisionCodec`
Tests: `Submit_*`, `Reject_*`, `Approve_*`, `ValidateEditable_*`, `Reopen_*`, `DeleteEligibility_*`, `Comparison_UsesApprovedBase_AndBaseStaysImmutable`, `CmDecisionCodec_RoundTrips`.

#### `FakePesoRepository`
File: `tests\BA.Dmo.UnitTests\Modules\Peso\FakePesoRepository.cs`
Implements: `IPesoRepository`. In-memory references, lotes, controls, day approvals, settings, audit events; approved-base lookups.

### 4.3 Armazém

#### `ArmazemServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Armazem\ArmazemServiceTests.cs`
Target: `ArmazemService`, `ArmazemAuthorizationGate`
Tests: `Entrada_*`, `Saida_*`, `Substituir_*`, `Consulta_*`, `Repor_*`, `Entrada_TwoDifferentToolsAtSamePosition_OnlyOneOccupiesAtomically`, `Entrada_ReEntrySameToolOnOccupiedPosition_IsConflict`, atomic-failure tests.
Uses: `FakeArmazemRepository`, `FakeToolIdentityResolver`, `ArmazemCurrentUser`, `ArmazemFakeAuthorship`, `ArmazemFixedClock`.

#### `WarehouseStockRulesTests`
File: `tests\BA.Dmo.UnitTests\Modules\Armazem\WarehouseStockRulesTests.cs`
Target: `WarehouseLocation`, `WarehouseStockRules`
Tests: `PositionCode_*`, `IsPositionOccupied_*`, `IsFora_*`, `HasReferenceConflict_*`.

#### `ArmazemAuthorizationGateTests`
File: `tests\BA.Dmo.UnitTests\Modules\Armazem\ArmazemAuthorizationGateTests.cs`
Target: `ArmazemAuthorizationGate`
Tests: `Require_WithModule_SucceedsAndReturnsCanonicalActor`, `Require_WithoutModule_IsForbidden`.

#### `FerramentasArmazemToolIdentityResolverTests`
File: `tests\BA.Dmo.UnitTests\Modules\Armazem\FerramentasArmazemToolIdentityResolverTests.cs`
Target: `FerramentasArmazemToolIdentityResolver`
Tests: `Search_CMAndMF_AreAccepted`, `Search_UnsupportedTypes_ReturnEmpty`, `Resolve_MapsToWarehouseOwnedIdentity`, `Resolve_Missing_ReturnsNull`.
Uses: `FakeFerramentasIdentityLookup`.

#### `FakeArmazemRepository`
File: `tests\BA.Dmo.UnitTests\Modules\Armazem\FakeArmazemRepository.cs`
Implements: `IArmazemRepository`. In-memory locations/stocks/movements/audit; `FailAtomicWrite` switch; atomic occupation guard.

#### `FakeToolIdentityResolver`
File: `tests\BA.Dmo.UnitTests\Modules\Armazem\FakeToolIdentityResolver.cs`
Implements: `IToolIdentityResolver`. Preset identities; search counter.

#### `ArmazemTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\Armazem\ArmazemTestSupport.cs`
Declares: `ArmazemFixedClock` (`IClock`), `ArmazemFakeAuthorship` (`IPersistenceAuthorshipAccessor`), `ArmazemCurrentUser` (`ICurrentUserAccessor`), `FakeFerramentasIdentityLookup` (`IFerramentasIdentityLookup`).

### 4.4 Boquilhas

#### `BoquilhasServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BoquilhasServiceTests.cs`
Target: `BoquilhasService`, `BqAuthorizationGate`
Tests: `CreateLoteWithTrace_IsOneAtomicCreation`, `DuplicateReferenceBatch_IsBlocked`, `InvalidReference_IsRejected`, `RegisterEntrada_20To25_AcceptsFullReturnAndOpensDiscrepancy`, `RegisterEntrada_Exact_NoDiscrepancy`, `RegisterSaida_ExceedingProduction_IsBlocked`, `Movement_OnClosedTrace_IsBlocked`, `CloseTrace_MarksClosed_AndAudits`, `Reopen_LastClosedTrace_Works_WhenNoActive`, `Lifecycle_*`, `ListMovements_*`.
Uses: `FakeBoquilhasRepository`, `FakeBqUnitOfWorkFactory`, `BqCurrentUser`, `BqFakeAuthorship`, `BqFixedClock`.

#### `BqAuthorizationGateTests`
File: `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqAuthorizationGateTests.cs`
Target: `BqAuthorizationGate`
Tests: `Require_WithModule_IsAuthorized`, `Require_WithoutModule_IsForbidden`.

#### `BqInventoryCalculatorTests`
File: `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqInventoryCalculatorTests.cs`
Target: `BqInventoryCalculator`, `BqRules`
Tests: `ReconcileReturn_*`, `CalculateTrace_20To25_FullLifecycle`, `Dispatch_ExceedingProduction_IsBlocked`, `NonRepairable_ExceedingRepair_IsBlocked`, `LineChange_DoesNotChangeBalances`, `PhysicalInventory_IncludesExceptional`.

#### `BqTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs`
Declares: `BqFixedClock`, `BqFakeAuthorship`, `BqCurrentUser`, `FakeBqUnitOfWork`/`FakeBqUnitOfWorkFactory` (`IBoquilhasUnitOfWorkFactory`, `IDbUnitOfWork`), `FakeBoquilhasRepository` (`IBoquilhasRepository`), seed helpers `SeedLote`, `SeedActiveTrace`, `SeedRepairer`.

### 4.5 Controlo

#### `ControloSheetServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloSheetServiceTests.cs`
Target: `ControloSheetService`, `ControloSheetAuthorizationGate`
Tests: `GetForProduction_NoExistingSheet_CreatesOneFromProductionContext`, `UpdateItems_AppliesControlAndLeavesState`, `Submit_ThenReview_Flow`, `Reopen_AfterSubmission_ReturnsToDraft`, `Create_WithoutEditCapability_Forbidden`, `GetForProductionByContext_ResolvesAndCreatesWithoutReSelection`, `ListSheets_WorksInFreeMode_NoCardRequired`.
Uses: `ControloTestBuilder`, `FakeControloSheetRepository`, `FakeControloProductionContextLookup`, `FakeControloUowFactory`, `ControloCurrentUser`, `ControloFakeAuthorship`.

#### `ControloFolhaTests`
File: `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloFolhaTests.cs`
Target: `ControloFolha`
Tests: `Create_SnapshotsComponentsAndPinsRevision`, `Create_WithoutContext_Fails`, `Submit_ThenDecide_Flow_Approved`, `Decide_WithoutSubmission_Fails`, `Submit_AfterDecision_IsRejected_ReopenAllowsResubmit`, `EditItemsAfterSubmission_IsAllowed_AndUpdatesResults`, `RecordEvent_IsAppendOnly`.

#### `ControloTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs`
Declares: `ControloFixedClock`, `ControloFakeAuthorship`, `FakeControloUowFactory`/`FakeControloUow`, `ControloCurrentUser`, `FakeControloSheetRepository` (`IControloSheetRepository`), `FakeControloProductionContextLookup` (`IControloProductionContextLookup`), `ControloTestBuilder` (Build helper).

### 4.6 Ferramentas

#### `FerramentasServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasServiceTests.cs`
Target: `FerramentasService`, `FerramentasAuthorizationGate`
Tests: `CreateReferenceWithFirstLote_*`, `Reference_DoesNotCarryProcesso_ProcessoLivesOnLote`, `Create_DuplicateReference_IsHardBlocked`, `Create_NoLinesSelected_IsValidationError`, `Create_WithoutModule_IsForbidden`, `DuplicateLote_IsConfigurationOnly_MasterIdentityReadOnly`, `Duplicate_ExistingLoteInSameReference_IsHardBlocked`, `AddCheckRule_*`, `RegisterPiece_AndSetCondition_AreExplicitFacts`, `SetCondition_WithoutReason_IsValidationError`, `ListReferences_MapsProcessoAndLinesFromLotes`, `ResolveActiveRules_ReturnsRuleLookupResult`.

#### `FerramentasDomainTests`
File: `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasDomainTests.cs`
Target: `ToolReference`, `ToolLote`, `ToolCheckRule`, `PhysicalPiece`, `FerramentasToolTypeCodec`
Tests: `ToolReference_*`, `ToolLote_*`, `CM_And_MF_AreDistinctTypes`, `ToolCheckRule_RequiresText`, `PhysicalPiece_*`, `ConditionChanges_RequireReason`.

#### `FerramentasUtilisationServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasUtilisationServiceTests.cs`
Target: `FerramentasService` (utilisation commands)
Tests: `RecordReading_Appends_AndNeverOverwrites`, `GetUtilisation_ReturnsRecordedManualPercent_NoFormula`, `RecordReading_InvalidPercent_IsRejected`, `RecordReading_NoFormula_StoresNegativeCumulative_Rejected`.

#### `FakeFerramentasRepository`
File: `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FakeFerramentasRepository.cs`
Implements: `IFerramentasRepository`. In-memory references, lotes, pieces, check rules, audit, utilisation readings; `FailAtomicCreate`.

#### `FerramentasTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasTestSupport.cs`
Declares: `FixedClock`, `FakeAuthorshipAccessor`, `FakeCurrentUser`, `FakeRuleLookup` (`IFerramentasRuleLookup`).

### 4.7 História

#### `HistoriaServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs`
Target: `HistoriaService`, `HistoriaAuthorizationGate`
Tests: `QueryAsync_AuthorizesAndForwardsScopeToRepository`, `QueryAsync_WithAuditView_OrdersChronologicallyStableAndGroupsByEntity`, `QueryAsync_InvalidPageSize_IsValidationError`, `QueryAsync_WithoutHistoriaModule_IsForbidden`.
Contains: `FakeHistoriaRepository` (`IHistoriaRepository`).

#### `HistoriaAuthorizationGateTests`
File: `tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaAuthorizationGateTests.cs`
Target: `HistoriaAuthorizationGate`
Tests: `Require_WithHistoriaAndOrigins_ResolvesGrantedOriginsOnly`, `Require_WithAuditView_IncludesAdmin`, `Require_WithNoOriginModules_IsAuthorizedWithEmptyScope`, `Require_WithoutHistoriaModule_IsForbidden`, `Require_WithNoIdentity_IsForbidden`.
Contains: `HistoriaCurrentUser` (test-double accessor factory).

### 4.8 Pegamentos

#### `PegamentoServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoServiceTests.cs`
Target: `PegamentoService`, `PegamentoAuthorizationGate`
Tests: `Create_WithCompleteContext_SucceedsAndDerivesJobOnId`, `Create_WithMissingComponents_IsBlocked`, `GetControlDetail_ResolvesHistoricalProductionContext`, `ListByRevision_ReturnsOnlyThatRevisionsRecords`, `Update_DoesNotRewriteRevisionAnchor`, `Create_WithoutAuthorizedIdentity_IsForbidden`, `AddMeasurement_ComputesOvalizacaoAndMediaServerSide`.

#### `PegamentoPdfTests`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoPdfTests.cs`
Target: `PegamentoPdfService`
Tests: `Generate_ReturnsPdfBytesAndHumanReadableFilename`, `Generate_DoesNotPersistDocumentRow`, `Generate_UnknownControl_IsNotFound`, `Generate_Unauthorized_IsForbidden`.
Uses: `FakePegamentoPdfRenderer`.

#### `PegamentoMeasurementCalculatorTests`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoMeasurementCalculatorTests.cs`
Target: `PegamentoMeasurementCalculator`
Tests: `Ovalizacao_*`, `Media_*`, `Tolerance_InsideCorridor_IsOk`, `Tolerance_OnBoundary_IsExceeded`, `Tolerance_BeyondCorridor_IsExceeded`.

#### `PegamentoHistoricalRelationshipTests`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoHistoricalRelationshipTests.cs`
Target: `PegamentoService`
Tests: `Proof1_Creating_Persists_TheExactRevisionId`, `Proof2_History_ResolvesOriginalCmBqMfFromThatRevision`, `Proof3_QueryingRevision_ReturnsItsPegamentos`, `Proof4_LaterRevision_DoesNotMoveOldPegamentos`, `Proof5_TwoRevisionsOfSameProduction_EachHaveOwnHistoricallyCorrectRows`.

#### `PegamentoDocumentConfirmationTests`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoDocumentConfirmationTests.cs`
Target: `PegamentoService` (document-confirmation commands)
Tests: `Confirm_PersistsServerDerivedFinalMetadata`, `Confirm_MissingOutputRoot_IsFailureAndDoesNotPersist`, `Confirm_MissingProductionFolder_IsFailureAndDoesNotPersist`, `Confirm_ClosedControl_CannotSilentlyReplaceFinalDocument`, `Confirm_Aberto_OneToOne_UpsertKeepsSingleRow`.

#### `JobOnProductionFolderResolverTests`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\JobOnProductionFolderResolverTests.cs`
Target: `FakeJobOnProductionFolderResolver`, `PegamentoService`
Tests: `Resolver_ResolvesConfiguredFolder_OrNullWhenAbsent`, `Confirm_UsesResolvedJobOnFolder_AndNotAnIndependentOne`, `Confirm_LaterRevisionDoesNotReinterpretExistingPdfAttribution`.

#### `FakePegamentoRepository`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakePegamentoRepository.cs`
Implements: `IPegamentoRepository`. In-memory controls, measurements, documents; recomputes ovalização/média/tolerance on hydration.

#### `PegamentoTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoTestSupport.cs`
Declares: `FakeSettings` (`IAppSettingsReader`), `FixedClock`, `FakeAuthorshipAccessor`, `FakeJobOnProductionContextLookup` (`IJobOnProductionContextLookup`), `FakePegamentoPdfRenderer` (`IPegamentoPdfRenderer`), `PegamentoContextBuilder` (produces `PegamentoProductionContext`).

#### `FakeJobOnProductionFolderResolver`
File: `tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakeJobOnProductionFolderResolver.cs`
Implements: `IJobOnProductionFolderResolver`. DefaultFolder + per-job-on folder map.

### 4.9 Reparação Externa

#### `ReparacaoExternaServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaServiceTests.cs`
Target: `ReparacaoExternaService`, `ReparacaoExternaAuthorizationGate`
Tests: `CreateExit_*`, `RemoveItem_AfterDisposicionado_IsRejected`, `Pickup_*`, `Return_*`, `DeactivateRepairer_SetsInactiveNotDeleted`, `UpsertLineDefault_WithInactiveRepairer_IsRejected`.
Uses: `FakeRepairRepository`, `FakeToolPieceResolver`, `FakeArmazemRepairMovementPort`, `FakeRepairUnitOfWorkFactory`, `ReparacaoExternaCurrentUser`, `ReparacaoExternaFakeAuthorship`, `ReparacaoExternaFixedClock`.

#### `RepairExitStatusMachineTests`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairExitStatusMachineTests.cs`
Target: `RepairExitStatusMachine`
Tests: `Pickup_*`, `Return_*` (transition table).

#### `RepairerCapabilityTests`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairerCapabilityTests.cs`
Target: `ReparacaoExternaService` (repairer capability commands)
Tests: `CreateRepairer_WithMultipleTypes_SupportsAll`, `CreateRepairer_InvalidType_IsRejected`, `UpdateRepairer_ChangesSupportedTypes`, `ListRepairers_ReturnsSupportedTypes`, `Capability_IsSeparate_FromLineDefault`.

#### `ReparacaoExternaAuthorizationGateTests`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaAuthorizationGateTests.cs`
Target: `ReparacaoExternaAuthorizationGate`
Tests: `Require_WithModuleGrant_Succeeds`, `Require_WithoutIdentity_FailsClosed`, `Require_WithoutModuleGrant_FailsClosed`.

#### `FakeRepairRepository`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\FakeRepairRepository.cs`
Implements: `IRepairRepository`. In-memory exits, items, repairers, line defaults, audit, coordinated-write records; `FailItemWrite`.

#### `ReparacaoExternaTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs`
Declares: `ReparacaoExternaFixedClock`, `ReparacaoExternaFakeAuthorship`, `ReparacaoExternaCurrentUser`, `FakeRepairUnitOfWorkFactory`/`FakeUnitOfWork`, `FakeArmazemRepairMovementPort` (`IArmazemRepairMovementPort`), `FakeToolPieceResolver` (`IToolPieceResolver`).

### 4.10 Reparação Interna

#### `ReparacaoInternaServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaServiceTests.cs`
Target: `ReparacaoInternaService`, `ReparacaoInternaAuthorizationGate`
Tests: `Register_*`, `ListLineCards_ShowsActiveReferenceOrNone`, `Corrigir_*`, `ListHistory_*`, `GetDetail_ReturnsChain`.
Uses: `FakeReparacaoInternaRepository`, `FakeJobOnActiveContextLookup`, `FakeFerramentasPieceLookup`, `FakeReparacaoInternaUowFactory`, `ReparacaoInternaCurrentUser`.

#### `ReparacaoInternaDomainTests`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaDomainTests.cs`
Target: `InternalRepairRecord`, `InternalRepairRules`, `InternalRepairToolTypeCodec`
Tests: `Create_*`, `Create_StructurallyInvalid_IsARejection`, `Create_WithoutOperator_Fails`, `Create_CapturesServerSideOperatorAndTime`, `CreateCorrection_*`, `Rules_*`.

#### `ReparacaoInternaProductionProjectionTests`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaProductionProjectionTests.cs`
Target: `ReparacaoInternaProductionProjection`
Tests: `ActivationUtc_Is0920Local_OnTheStartDate`, `SelectEffective_*`.

#### `ReparacaoInternaTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs`
Declares: `ReparacaoInternaFixedClock`, `ReparacaoInternaFakeAuthorship`, `FakeReparacaoInternaUowFactory`/`FakeReparacaoInternaUnitOfWork`, `ReparacaoInternaCurrentUser`, `FakeReparacaoInternaRepository` (`IReparacaoInternaRepository`), `FakeJobOnActiveContextLookup` (`IJobOnActiveContextLookup`), `FakeFerramentasPieceLookup` (`IFerramentasPieceLookup`).

### 4.11 Tampões

#### `TampaoServiceTests`
File: `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoServiceTests.cs`
Target: `TampaoService`, `TampaoAuthorizationGate`
Tests: `Adicionar_*`, `Remover_*`, `AlterarEstado_*`, `AlterarConfiguracao_*`, `Planear_DoesNotAlterOrReserveStock`, `CancelarPlano_PreservesBalances`, `Opcoes_DeactivateValue_DoesNotDeleteConfigurationsOrHistory`, `Consulta_WithoutModule_FailsClosed`, `ListMovimentos_FiltersByType`.

#### `TampaoDomainTests`
File: `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoDomainTests.cs`
Target: `TampaoConfigurationKey`, `TampaoRules`
Tests: `ConfigurationKey_*`, `NormalizeValue_CollapsesLegacyVariants`, `ValidateQuantity_PositiveIntegerOnly`, `ApplySingleBalanceChange_NeverNegative`, `ResolveStateOrigin_OriginIsOpposite_AndBlocksInsufficient`, `ApplyBalanceTransfer_BlocksDestinationEqualsOrigin`, `ValidateConfigurationTransform_RequiresDifferentIdAndAChangedCharacteristic`.

#### `TampaoMachineTests`
File: `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoMachineTests.cs`
Target: `TampaoService` (multi-machine commands)
Tests: `AssignMachineB1`, `AssignMultipleMachines_B1_B2_C1`, `RemoveMachineB2_KeepsOthers_AndAuditsRemoval`, `InvalidMachine_IsRejected`, `Comments_Persist_AndHistoryKept`, `MachineFilter_ReturnsMultiAssociatedRecord_Once`, `NoConfigurationDuplication_ForMultipleMachines`, `DetailSheet_ReturnsMachinesNotesAndEvents`, `InvalidMachineFilter_IsRejected`.

#### `TampaoTestSupport`
File: `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoTestSupport.cs`
Declares: `TampaoFixedClock`, `TampaoFakeAuthorship`, `TampaoCurrentUser`, `FakeTampoesUnitOfWorkFactory`/`FakeTampaoUnitOfWork`, `FakeTampaoRepository` (`ITampaoRepository`), `SeedConfiguration` helper.

### 4.12 Shared — Access

#### `AccessResolverTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\AccessResolverTests.cs`
Target: `AccessResolver`, `EffectiveAccess`, `CatalogValidator`
Tests: `InactiveTemplate_*`, `Landing_IsJobOn_ForABoquilhasOnlyUser`, `Landing_IsAdmin_ForAnAdminOnlyTemplate`, `Landing_IsJobOn_EvenWithZeroOperationalGrants`, `Landing_DoesNotDependOnRoleNames`, `PreferredFirstPageId_IsNotUsedInV1`, `Fallback_WhenLandingGenuinelyUnavailable_IsFirstAccessibleInCanonicalOrder`, `NoAccessiblePage_YieldsExplicitNoAccess`, `NavigationModules_FollowCanonicalOrder`, `ControloArea_VisibleOnlyWithAuthorizedChildren`, `AreaFirstPage_IsFirstAuthorizedChild_InCanonicalOrder`, `PesoExperience_IsResolvedByCapability_NotByRole`, `Capabilities_ConstrainPageAccess`, `UnauthorizedModule_PagesAreNotAccessible`, `NewCatalogModule_RequiresNoNavigationChanges_Acceptance`, `InactivePage_IsNeverAccessible`.

#### `CurrentUserTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\CurrentUserTests.cs`
Target: `CurrentUser`, `ICurrentUserAccessor`
Tests: `CurrentUser_NormalizesGrants_AndAnswersQueries`, `CurrentUser_EmptyIdOrBlankName_AreRejected`, `Accessor_ReturnsNull_WhenNoUserIsResolved`, `Accessor_ReturnsResolvedUser_WhenPresent`.

#### `ModuleCatalogTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\ModuleCatalogTests.cs`
Target: `ModuleCatalog`, `ModuleDefinition`
Tests: `EmptyCatalog_IsValid`, `EmptyCatalog_NullOrBlankQueries_NeverThrow`, `Catalog_ExposesEntriesInCanonicalOrder`, `Catalog_SameCanonicalOrder_FallsBackToModuleIdOrder`, `Catalog_LookupFindsRegisteredModule_AndItsCapabilities`, `Catalog_DuplicateModuleId_IsRejected`, `Catalog_NullEntries_AreRejected`, `FunctionalAreaKind_IsRepresented`.

#### `CanonicalModuleCatalogTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\CanonicalModuleCatalogTests.cs`
Target: `CanonicalModuleCatalog`
Tests: `Catalog_ContainsExactlyTheCanonicalModules`, `Catalog_CanonicalOrder_MatchesModules00`, `Catalog_InitialRoutes_MatchModules00`, `Controlo_IsAFunctionalArea_WithFolhaControloCapabilities`, `AllOtherEntries_AreModules`, `Capabilities_AreExactlyTheCanonicalSet_WithExactOwnership`, `CapabilityIds_AreUnique_AcrossTheCatalog`, `ModulesWithoutCapabilities_HaveNoneDeclared`.

#### `CanonicalPageCatalogTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\CanonicalPageCatalogTests.cs`
Target: `CanonicalPageCatalog`, `PageDefinition`
Tests: `PageCatalog_*`, `Routes_MatchTheCanonicalGrammar`, `RouteGrammar_AcceptsCanonicalShapes` (Theory), `RouteGrammar_RejectsInvalidShapes` (Theory), `PageDefinition_Constructor_RejectsInvalidRoute`, `EveryPage_ReferencesAKnownModule`, `RequiredCapabilities_AreKnownAndOwnedByThePageModule`, `CapabilityGatedPages_AreExactlyTheCanonicalOnes`, `ExactlyOneLandingPage_AndItIsJobOn`, `DuplicatePageIds_AndDuplicateRoutes_AreRejected`.

#### `CapabilityAndModuleDefinitionTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\CapabilityAndModuleDefinitionTests.cs`
Target: `Capability`, `ModuleDefinition`
Tests: `Capability_ParsesModuleSegment` (Theory), `Capability_InvalidFormat_IsRejected` (Theory), `ModuleDefinition_*` (Theories), `ModuleDefinition_TrimsAndFreezesCapabilities`.

#### `CatalogValidatorTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\CatalogValidatorTests.cs`
Target: `CatalogValidator`
Tests: `CanonicalConfiguration_IsValid`, `PageReferencingUnknownModule_Fails`, `PageRequiringUnknownCapability_Fails`, `PageRequiringCapabilityOfAnotherModule_Fails`, `CapabilityDeclaredByTwoModules_Fails`, `MissingLandingPage_Fails`, `TwoLandingPages_Fail`, `InactiveLandingPage_Fails`, `DuplicateModuleInitialRoutes_Fail`, `AreaWithUnknownChild_Fails`, `AreaPointingAtNonFunctionalArea_Fails`, `AllViolations_AreReportedTogether`.

#### `GrantNormalizerTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\GrantNormalizerTests.cs`
Target: `GrantNormalizer`
Tests: `KnownModuleGrant_IsPreserved`, `UnknownModuleId_IsDiscarded_AndReported`, `CapabilityNotOwnedByTheGrantedModule_IsDiscarded`, `OwnedCapability_IsPreserved`, `AuditCapabilities_AreValidOnlyUnderTheAdminModule`, `DuplicateModuleEntries_FirstPrevails_AndLaterAreDiscarded`, `DuplicateCapabilities_AreDeduplicated`, `FunctionalArea_GrantIsDiscarded`, `BlankCapabilities_AreDiscarded`.

#### `NavigationServiceTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\NavigationServiceTests.cs`
Target: `NavigationService`, `AccessResolver`
Tests: `EmptyTemplate_ShowsOnlyJobOn_NoControlo_NoAdmin`, `MultipleModules_RenderInCanonicalOrder_OnlyAuthorized`, `ControloGroup_ShowsOnlyAuthorizedChildren` (Theory), `NoControloChildren_NoAreaEntry`, `PesoEntry_ResolvesTheExperienceByCapability`, `AdminEntry_RequiresAdminGerir_AndIsRightAligned`, `ActiveState_FollowsTheCurrentRoute`, `InactiveTemplate_ProducesNoNavigation`.

#### `ModuleCatalogMirrorSynchronizerTests`
File: `tests\BA.Dmo.UnitTests\Shared\Access\ModuleCatalogMirrorSynchronizerTests.cs`
Target: `ModuleCatalogMirrorSynchronizer`
Tests: `BuildSyncRows_MirrorsTheCanonicalCatalog_InCanonicalOrder`, `ValidateMirrorRows_*`, `MergeForDisplay_*`.

### 4.13 Shared — Admin

#### `AdminUserServiceTests`
File: `tests\BA.Dmo.UnitTests\Shared\Admin\AdminUserServiceTests.cs`
Target: `AdminUserService`, `AdminAuthorizationGate`
Tests: `Mutation_WithoutCapability_IsDenied_AndWritesNothing`, `Mutation_WithoutResolvedIdentity_IsDenied`, `CreateUser_*`, `UpdateUser_*`, `Deactivate*_*`, `ChangeTemplate_*`, `SaveUserModules_*`, `ListAsync_*`, `GetAsync_MissingSchema_IsAFailure_NotANotFound`, `PasswordReset_GoesThroughPrivilegedAdapter_AndAuditsWithoutSecrets`.
Uses: `FakeAdminRepository`, `FakeProvisioning` (nested `IAdminProvisioningAdapter`), `FakeCurrentUserAccessor`, `FixedClock`.

#### `AdminAuditAndMirrorTests`
File: `tests\BA.Dmo.UnitTests\Shared\Admin\AdminAuditAndMirrorTests.cs`
Target: `AdminAuditService`, `AdminMirrorService`
Tests: `AuditQuery_RequiresAuditView_AndUsesCanonicalPagination`, `AuditExport_RequiresAuditExport_AndNeverCarriesSecrets`, `AuditExport_FactualContentOnly`, `MirrorSave_UnknownModule_IsRejected_NothingPersisted`, `MirrorSave_CanonicalEntries_PersistAndAudit`.
Uses: `FakeAdminRepository`, nested `FakeMirrorRepository`, `FakeCurrentUserAccessor`, `FixedClock`.

#### `AdminTemplateServiceTests`
File: `tests\BA.Dmo.UnitTests\Shared\Admin\AdminTemplateServiceTests.cs`
Target: `AdminTemplateService`, `GrantNormalizer`
Tests: `CreateTemplate_ValidGrants_PersistsCanonicalJson`, `CreateTemplate_InvalidGrants_AreRejected_WithExplicitReport` (Theory), `CreateTemplate_DuplicateId_IsConflict`, `UpdateTemplate_*`, `Mutations_WithoutCapability_AreDenied`.
Uses: `FakeAdminRepository`, nested `FakeCurrentUserAccessor`, `FixedClock`.

#### `FakeAdminRepository`
File: `tests\BA.Dmo.UnitTests\Shared\Admin\FakeAdminRepository.cs`
Implements: `IAdminRepository`. In-memory users, templates, audits, writes; switches for lockout, concurrency, schema-migration-required, fail-internal-create.

### 4.14 Shared — Identity

#### `IdentityResolutionServiceTests`
File: `tests\BA.Dmo.UnitTests\Shared\Identity\IdentityResolutionServiceTests.cs`
Target: `IdentityResolutionService`, `AccessResolver`
Tests: `ValidActiveUserAndTemplate_ResolveAuthoritativeIdentity`, `Landing_IsJobOn_AfterSuccessfulResolution`, `MissingInternalUser_FailsClosed_WithInternalUserInactive`, `InactiveInternalUser_IsDenied`, `InactiveTemplate_IsDenied`, `MalformedTemplateGrants_FailClosed`, `InvalidGrantEntries_AreDiscarded_NotSilentlyRepaired`, `AdminGrants_LandOnAdmin_InsteadOfJobOn`, `TemplateNames_DoNotInfluenceResolution`, `RepositoryFailure_FailsClosed`, `AmbiguousIdentity_FailsClosed_AsIdentityAmbiguous_NotBackendUnavailable`, `EmptyAuthUserId_FailsClosed`.
Uses: nested `FakeInternalUserRepository`.

#### `AccessTemplateGrantsParserTests`
File: `tests\BA.Dmo.UnitTests\Shared\Identity\AccessTemplateGrantsParserTests.cs`
Target: `AccessTemplateGrantsParser`
Tests: `ValidModulesJson_ParsesGrants`, `EmptyModules_ParsesToNoGrants` (Theory), `MalformedJson_FailsExplicitly`, `EntriesWithoutModuleId_AreSkipped`, `BlankCapabilities_AreDropped`, `UnknownModulesAndCapabilities_AreLeftForNormalization`.

#### `BootstrapAdminServiceTests`
File: `tests\BA.Dmo.UnitTests\Shared\Identity\BootstrapAdminServiceTests.cs`
Target: `BootstrapAdminService`
Tests: `Success_CreatesMinimalAdminTemplateAndActiveUser`, `ExistingValidAdmin_IsIdempotent_NoWrites`, `MissingExplicitConfiguration_FailsValidation` (Theory), `FreshlyCreatedAccount_NoRecoveryLinkRequested`, `PreExistedAccount_AutoRecoveryLinkIssued_AndAdminCreated`, `PreExistedAccount_RecoveryFailure_FailsBeforeAnyWrite`, `ProvisioningFailure_Propagates_AndNothingIsPersisted`, `PersistenceFailure_Propagates`.
Uses: nested `FakeProvisioningAdapter`, `FakeInternalUserRepository`, `FixedClock`.

### 4.15 Shared — Kernel

#### `ClockTests`
File: `tests\BA.Dmo.UnitTests\Shared\Kernel\ClockTests.cs`
Target: `SystemClock`, `IClock`
Tests: `SystemClock_ReportsUtcCloseToNow`, `FixedFakeClock_SatisfiesContract_ForDeterministicTests`.

#### `ResultTests`
File: `tests\BA.Dmo.UnitTests\Shared\Kernel\ResultTests.cs`
Target: `Result`
Tests: `Success_CarriesValue_AndIsNotFailure`, `Failure_CarriesError_AndIsNotSuccess`, `Value_OnFailure_Throws`, `Error_OnSuccess_Throws`, `ConvenienceFactories_UseDomainErrorChannel`, `Failure_WithNullDomainError_Throws`, `Map_TransformsSuccessValue_AndPreservesFailure`, `Bind_ChainsSuccess_AndShortCircuitsFailure`, `GenericErrorChannel_IsNotLimitedToDomainError`.

#### `DomainErrorTests`
File: `tests\BA.Dmo.UnitTests\Shared\Kernel\DomainErrorTests.cs`
Target: `DomainError`, `ErrorCategory`
Tests: `EveryCategory_HasAFactory` (Theory), `AllEightCategories_AreCovered`, `EmptyCode_IsRejected` (Theory), `EmptyMessage_IsRejected` (Theory), `ToString_IncludesCategoryCodeAndMessage`.

### 4.16 Shared — Persistence

#### `ConcurrencyGuardTests`
File: `tests\BA.Dmo.UnitTests\Shared\Persistence\ConcurrencyGuardTests.cs`
Target: `ConcurrencyGuard`, `ConcurrencyConflictException`
Tests: `SingleRowUpdated_Passes`, `ZeroRows_ThrowsConcurrencyConflict_WithReloadMessage`, `MoreThanOneRow_AlsoConflicts`, `BlankDescription_IsRejectedOnConflict`.

#### `PersistenceAuthorshipTests`
File: `tests\BA.Dmo.UnitTests\Shared\Persistence\PersistenceAuthorshipTests.cs`
Target: `PersistenceAuthorship`, `IPersistenceAuthorshipAccessor`
Tests: `Authorship_CarriesActorAndUtcTimestamp`, `Authorship_AllowsNullActor_ForSystemOperations`, `NonUtcTimestamp_IsRejected`, `AccessorPort_ResolvesCurrentAuthorship`.

---

## 5. Integration Test Project

`tests\BA.Dmo.IntegrationTests\` — Web + Infrastructure contract tests. Uses `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) for web hosts, and tests Infrastructure types (Dapper, migrations, Supabase adapters, persistence) using in-memory/fake collaborators. No live Supabase or production DB is used by most tests; the single database-backed suite (`RemediationGuardTests`) is env-guarded.

### 5.1 Access

#### `AdminSecurityGuardTests` / `CatalogCompositionGuardTests`
Files: `tests\BA.Dmo.IntegrationTests\Access\AdminSecurityGuardTests.cs`, `CatalogCompositionGuardTests.cs`
Kind: Reflection/architecture guard test classes.
Targets: `Program` assembly; `AdminUserService`; `IAdminProvisioningAdapter`; `SupabaseAdminProvisioningAdapter` (asserted absent from page-model ctor params); `CatalogValidator`, `CanonicalModuleCatalog`, `CanonicalPageCatalog`, `DapperModuleCatalogMirrorRepository`, `IModuleCatalogMirrorRepository`, `IDbConnectionFactory`.
Tests (methods): `PrivilegedProvisioning_IsNotReachableFromAdminPages`, `AdminPages_AuthorizeViaCanonicalCapabilityPolicies_NotRoleNames`, `ApplicationAdminServices_HaveNoProviderSpecificDependencies`; `CanonicalConfiguration_WiredAtStartup_IsValid`, `CatalogDefinitions_DoNotLiveInTheWebAssembly`, `LandingPolicy_IsSingleAndGlobal`, `MirrorPort_IsImplementedInInfrastructure_WithU03FactoryContract`.

#### `BoquilhasWebAuthorizationTests`
File: `tests\BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs`
Kind: Integration test class (WAF: nested `BoquilhasFixture : WebApplicationFactory<Program>`).
Targets: `Program`; `/boquilhas` page; `/api/boquilhas/*` controllers; `IBoquilhasRepository`; `IBoquilhasUnitOfWorkFactory`.
Tests: `Unauth_BoquilhasPage_RedirectsToLogin`, `WithoutBoquilhasModule_IsDenied`, `WithModule_PageRenders`, `CreateLot_ThenReturn20To25_AcceptsFullReturnAndOpensDiscrepancy`, `DispatchExceedingProduction_IsBadRequest`.
Uses: `FakeBoquilhasWebRepository`, nested `FakeAuthAdapter`/`FakeIdentityRepository`/`FakeBqWebUnitOfWorkFactory`/`FakeBqWebUnitOfWork`. Antiforgery disabled via `IgnoreAntiforgeryTokenAttribute`.
Endpoints: `GET /boquilhas`, `POST /api/boquilhas/lotes`, `GET /api/boquilhas/lotes/{lotId}`, `POST /api/boquilhas/movements`, `GET /api/boquilhas/discrepancies?lotId=`, `POST /login`.

#### `FakeBoquilhasWebRepository`
File: `tests\BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs`
Implements: `IBoquilhasRepository`. In-memory lots/traces/movements/discrepancies/lifecycle/utilisation/repairers/line-defaults; `Reset()`.

#### `AdminFormAntiforgeryTests`
File: `tests\BA.Dmo.IntegrationTests\Access\AdminFormAntiforgeryTests.cs`
Kind: Integration test class (WAF: nested `AfFixture`; antiforgery intentionally enforced).
Targets: `Program`; /admin/users/create, /admin/templates/edit, /admin/applications; Razor antiforgery pipeline.
Tests: `AdminForms_RenderAnAntiForgeryToken`, `TokenlessPost_IsRejected400_AndWritesNothing` (Theory, 3 rows), `UserCreate_WithToken_CreatesTheUser`, `TemplateEdit_WithToken_CreatesTheTemplate`, `Applications_WithToken_SavesTheMirror`, `CrossSessionToken_IsRejected400`, `AnonymousPost_RedirectsToLogin_AndWritesNothing`, `OperatorSession_Post_IsDeniedByPolicy_AndWritesNothing`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeProvisioningAdapter`, `FakeAdminRepository`, `FakeMirrorRepository`.

#### `AdminWebAuthorizationTests`
File: `tests\BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs`
Kind: Integration test class (WAF: nested `AdminFixture`).
Targets: `Program`; /admin, /admin/users, /admin/templates, /admin/audit, /jobon; admin policy.
Tests: `Unauthenticated_AdminPage_RedirectsToLogin`, `AuthenticatedWithoutAdminCapability_IsDenied_AndForgedPostWritesNothing`, `AdminCapability_AllowsAdminPages_AndLoginLandsOnAdmin`, `AdminWithOnlyAdminGerir_LoginRedirectsToAdmin`, `AdminWithOnlyAdminGerir_DoesNotRequireJobOnAccess`, `AuditPage_RequiresAuditView`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeMirrorRepository`, `FakeAdminWritesRepository`.

#### `DapperAdminRepositoryProjectionTests`
File: `tests\BA.Dmo.IntegrationTests\Access\DapperAdminRepositoryProjectionTests.cs`
Kind: Integration test class driving the real `DapperAdminRepository` over ADO.NET doubles (no DB).
Targets: `DapperAdminRepository`, `AdminUserRow` (9-param projection), `IDbConnectionFactory`.
Test: `UserColumns_MaterializesAdminUserRow_WithAuthEmailNull_BeforeEnrichment`.
Uses: nested doubles `DataReaderDbConnection`, `DataReaderDbCommand`, `NoParameterCollection`, `FixedReaderConnectionFactory` (capture `IssuedSql`, `WasDisposed`).

#### `AdminUserListResetTests`
File: `tests\BA.Dmo.IntegrationTests\Access\AdminUserListResetTests.cs`
Kind: Integration test class (WAF: nested `ResetFixture`).
Targets: `Program`; `/admin/users` Reset handler; `AdminUserService.RequestPasswordResetAsync`; provisioning adapter.
Tests: `ListPageReset_UsesTheExistingServicePath_AuditsAndShowsBanner`, `ListPageReset_UnknownUser_ShowsError_NoProvisioningNoAudit`, `EditPageReset_StillUsesTheSamePath`.
Uses: nested `RecordingProvisioningAdapter`, `RecordingAdminRepository`.

#### `ShellRoutingTests`
File: `tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs`
Kind: Integration test class (WAF: nested `ShellFixture`, profile-switchable).
Targets: `Program`; module/route authorization; `/` landing; all module routes; `/access-denied`, `/no-access`.
Tests: `Scenario1_BoquilhasOnly_LandsOnJobOn_AllOtherModulesDenied`, `Scenario10_DeepLinkDenied_RedirectsToAuthorizedAreaWithFeedback`, `Scenario2_PesoOperador_CannotReachResponsavelRoutes`, `Scenario3_PesoResponsavel_IsRedirectedFromOperadorRoute`, `Scenarios4To6_ControloShowsOnlyAuthorizedChildren` (Theory), `Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn`, `JobOnPage_RendersTheU13Surface_InsideTheAuthorizedShell`, `JobOnPage_WithoutEditOrConfigure_HidesPrivilegedControls`, `Scenario9_NoInternalIdentity_NoAccessSafeState_NoLoop`, `Scenario12_TemplateDeactivated_SessionAuthenticatedWithoutAccess`, `Scenario11_GrantsRemovedMidSession_ReResolvedPerRequest`, `Unauthenticated_ModuleRoutes_RedirectToLogin`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeJobOnRepository`, `FakePesoRepository`.

#### `HistoriaWebAuthorizationTests`
File: `tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs`
Kind: Integration test class (WAF: nested `HistoriaFixture`).
Targets: `Program`; `/historia`; `IHistoriaRepository.QueryAsync`; ancestry `visibleModuleIds`/`includeAdminWithAuditView`.
Tests: `Unauth_HistoriaPage_RedirectsToLogin`, `WithoutHistoriaModule_IsDenied`, `WithHistoria_OnlyGrantedOriginModulesReachTheProjection`, `WithHistoria_AdminEventsExcludedWithoutAuditView`.
Uses: nested `FakeHistoriaReadRepository` (records scope), `FakeAdminRepo`, `FakeJobOnRepo`.

### 5.2 Cli

CLI command tests invoke command `Run` methods directly with injected environment resolvers and `StringWriter` stdout/stderr; no web server is started.

#### `BootstrapAdminCliTests`
File: `tests\BA.Dmo.IntegrationTests\Cli\BootstrapAdminCliTests.cs`
Target: `BootstrapAdminCommand.Run`, `ConfigurationErrorExitCode`.
Tests: `NoConfiguration_FailsExplicitly_WithExitCode2`, `PartialConfiguration_ListsOnlyTheMissingVariables`, `MissingDatabaseConfiguration_FailsBeforeAnyProvisioning`.

#### `CliCommandPlaceholderTests` (class `CliCommandContractTests`)
File: `tests\BA.Dmo.IntegrationTests\Cli\CliCommandPlaceholderTests.cs`
Target: `BootstrapAdminCommand.Run`.
Test: `BootstrapAdmin_MissingConfiguration_FailsExplicitly_UntilConfigured`.

#### `CliRoutingTests`
File: `tests\BA.Dmo.IntegrationTests\Cli\CliRoutingTests.cs`
Target: `CliModeResolver.Resolve`, `CliMode`.
Tests: `OperationalVerbs_AreDistinguished` (Theory, 5 rows), `NoArguments_MeansNormalWebStartup`, `BlankFirstArgument_MeansNormalWebStartup`, `NonVerbLeadingArgument_FallsBackToWebStartup` (Theory, 3 rows), `OnlyTheFirstArgument_SelectsTheMode`.

#### `MigrateCliTests`
File: `tests\BA.Dmo.IntegrationTests\Cli\MigrateCliTests.cs`
Target: `MigrateCommand.Run`; `ConnectionStringVariable`, `MigrationsDirectoryVariable`, `FallbackConnectionStringVariable`, exit codes.
Tests: `MissingConnectionConfiguration_FailsExplicitly_NonZero`, `MissingMigrationsDirectory_FailsExplicitly_NonZero`, `UnusableConnection_FailsNonZero_WithoutWebServer`, `DatabaseUrlFallback_IsHonored_WhenPrimaryVariableAbsent`.

### 5.3 Design

#### `DesignSystemGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs`
Kind: Integration test class (WAF: nested `DesignFixture`) + static file guards.
Targets: `src/BA.Dmo.Web/wwwroot/styles` CSS token files, `_Layout.cshtml` load order, `/design-laboratorio`, `/jobon`.
Tests: `TokenFile_DefinesAllRequiredTokenGroups`, `ReducedMotion_IsImplemented`, `SemanticTokens_MatchTheDesignReferenceExactly`, `Layout_WiresTheCanonicalLoadOrder_ExactlyOnce`, `SingleDesignSystem_NoCompetingLegacyCss`, `SharedComponentLayer_ConsumesTokensOnly`, `ButtonStateMachine_FilledRestInvertedHover`, `Pages_ContainNoLocalDesignCss`, `LaboratoryPage_RequiresASession_AndRendersTheCatalog`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeJobOnRepository`. Finds repo root via `BA-DMO.sln`.

#### `ShellAndCalendarGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs`
Kind: Integration test class (WAF: nested `LabFixture`) + static file guards.
Targets: `wwwroot/styles`, `wwwroot/scripts`, `Pages/Shared/_Layout.cshtml/_Header.cshtml/_Navigation.cshtml`, `/design-laboratorio`.
Tests: `SingleCanonicalCalendar_NoCompetingImplementations`, `ShellComposition_UsesTheDesignSystem`, `LaboratoryPage_ConsumesTheCanonicalCalendar`.

#### `JobOnScriptSafetyGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Design\JobOnScriptSafetyGuardTests.cs`
Kind: Static file-content guard test class.
Targets: `wwwroot/scripts/jobon.js`.
Tests: `CatalogLabel_IsEscaped_BeforeInsertAdjacentHtml`, `NoRawUnescapedCatalogLabel_IsInterpolatedIntoHtml`, `EscHelper_IsDefinedInTheScript`.

### 5.4 Ferramentas

#### `FerramentasWebApiTests`
File: `tests\BA.Dmo.IntegrationTests\Ferramentas\FerramentasWebApiTests.cs`
Kind: Integration test class (WAF: nested `FerrFixture`).
Targets: `/api/ferramentas/*`; `IFerramentasRepository`; `IFerramentasRuleLookup`; `ferramentas.configure` capability.
Tests: `Anonymous_IsDenied_RedirectsToLogin` (Theory, 3 rows), `AuthorizedFerramentasUser_Search_IsAdmitted`, `UserWithoutFerramentasModule_IsDenied`, `RulesEndpoint_WithoutConfigure_IsDenied`, `RulesEndpoint_WithConfigure_IsAdmitted`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeLookup`, `FakeRepo`. Scoped replacement for `IFerramentasRepository`/`IFerramentasRuleLookup`.

### 5.5 Identity

#### `SupabaseAuthAdapterTests`
File: `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAuthAdapterTests.cs`
Kind: Integration test class (HTTP adapter via `FakeHttpMessageHandler`).
Target: `SupabaseAuthAdapter`.
Tests (response scenarios): `ValidCredentials_ReturnTheAuthUserId`, `InvalidCredentials_FailWithProviderReason_ForServerSideLogging`, `RateLimited429_IsProviderUnavailable_NeverInvalidCredentials`, `ApiKeyRejected401_IsConfigSuspect_NeverInvalidCredentials`, `ApiKeyRejected403_IsConfigSuspect_NeverInvalidCredentials`, `ServerError503_IsProviderUnavailable`, `NetworkFailure_FailsClosed_AsBackendUnavailable`, `UnconfiguredAdapter_FailsClosed_WithoutHttpCalls`, `BlankCredentials_FailWithoutHttpCalls`.
Asserts endpoint `{SupabaseUrl}/auth/v1/token?grant_type=password`, `apikey` header, status mapping to `ErrorCategory`, no secret leaks.

#### `SupabaseAdminProvisioningAdapterTests`
File: `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs`
Kind: Integration test class (HTTP adapter via `FakeHttpMessageHandler`).
Target: `SupabaseAdminProvisioningAdapter`.
Tests: `GetUserEmails_*` (pagination: user-on-page-2, stops-when-all-found, stops-on-short-page, no-one-request-per-user, lookup-failure returns empty, missing-config returns empty), `CreateUser_SendsServiceRoleOnlyServerSide_AndReturnsTheUserId`, `ExistingAccount_IsResolvedIdempotently_ViaAdminLookup`, `MissingConfiguration_FailsClearly_WithoutHttpCalls`, `HardFailure_*`, `NetworkFailure_*`.
Asserts `/auth/v1/admin/users`, Bearer service-role header, idempotent 409/422 path, no secret leaks.

#### `IdentitySecurityGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Identity\IdentitySecurityGuardTests.cs`
Kind: Reflection/security guard test class.
Targets: `Program` assembly; `SessionClaims`; `IAdminProvisioningAdapter`; Application assembly.
Tests: `ProvisioningAdapter_IsNeverHeldByWebTypes_PagesAndHandlersIncluded`, `SessionCookieContract_CarriesOnlyTheAuthUserId_NoGrantsOrRoles`, `ApplicationLayer_HasNoProviderSpecificDependencies`.

#### `WebAuthSessionTests`
File: `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs`
Kind: Integration test class (WAF: nested `AuthTestFixture`).
Targets: `/login`, `/logout`, `/`, `/no-access`, `/access-denied`, `/jobon`; `ISupabaseAuthAdapter`; `IInternalUserRepository`.
Tests: `UnauthenticatedRequest_IsRedirectedToLogin`, `LoginPage_IsPublic`, `SafeStatePages_ArePublic`, `SuccessfulLogin_RedirectsToTheJobOnLanding_WithSessionCookie`, `ExternalOrSuppliedReturnUrl_CanNeverOverrideTrustedRouting`, `InvalidCredentials_ShowGenericError_AndNoSession`, `AuthenticatedWithoutInternalMapping_GoesToNoAccessSafeState`, `AuthenticatedWithInactiveTemplate_GoesToNoAccessSafeState`, `Logout_ClearsTheSession`, `ProviderFailure_ShowsGenericError_NoSession`, `FailedLogin_PreservesSubmittedEmail_AndDoesNotRenderPassword`, `ProviderUnavailable_PreservesSubmittedEmail`, `BlankPassword_ValidationFailure_PreservesSubmittedEmail`, `AuthenticatedUser_WhenIdentityDatabaseUnavailable_ShowsBackendUnavailableState`.
Uses: nested `FakeAuthAdapter` (AuthMode switchable), `FakeIdentityRepository`. Antiforgery disabled.

#### `IdentityAmbiguityLandingTests`
File: `tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs`
Kind: Integration test class (WAF: nested `AmbiguityFixture`).
Targets: `/login`, `/no-access`; `IInternalUserRepository`.
Tests: `AmbiguousIdentity_LoginLandsOnPlainNoAccess_NeverIndisponivel`, `GenuineRepositoryFailure_StillLandsOnIndisponivel`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository` (ThrowAmbiguous / ThrowOnFind).

#### `FakeHttpMessageHandler`
File: `tests\BA.Dmo.IntegrationTests\Identity\FakeHttpMessageHandler.cs`
Extends: `HttpMessageHandler`. Captures `Requests`, `RequestBodies`; scriptable `Responders` queue; optional `Throw`.

### 5.6 Integrity (Database)

#### `RemediationGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Integrity\RemediationGuardTests.cs`
Kind: Database integration test class against a real PostgreSQL instance.
Target: `N25_remediation.sql` schema (constraints, triggers, RLS, indexes).
Setup: reads env var `BA_DMO_TEST_DATABASE` (Npgsql connection string); when absent the tests return (skip); seeds fresh GUID keys; assumes schema migrated N01–N25.
Tests: `DuplicateAuthUserId_IsRejected`, `NullAuthUserId_IsRejected`, `JobOnIdentity_CanceledPairMayBeReissued_SecondActiveBlocked`, `SecondActiveTracePerLote_IsRejected`, `RevisionRows_AreAppendOnly`, `ApprovedPeso_IsImmutable_AtDatabaseLevel`, `PesoApprovedConsistency_IsEnforced`, `InvalidStatusValues_AreRejected`, `LateTables_RlsPolicyAndGrants_MatchN12Convention`, `AuditEventsModuleTime_IndexExists`.
Direct references: `internal_users`, `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `bq_lotes`, `bq_traces`, `peso_references`, `peso_lotes`, `peso_controlos`, `repair_exits`, `repair_exit_items`, `pegamento_controlos`, `job_on_verification_occurrence`, `access_templates`, `audit_events`, `pg_trigger`/`pg_policy`/`pg_class`/`information_schema`.
Assertions: PostgreSQL `SQLSTATE` codes `23505`, `23502`, `23514`; trigger/message text; index/RLS/privilege counts.

### 5.7 JobOn

#### `JobOnLandingTests`
File: `tests\BA.Dmo.IntegrationTests\JobOn\JobOnLandingTests.cs`
Kind: Integration test class (WAF: nested `LandingFixture`).
Targets: `/jobon` landing; `IJobOnRepository.GetHistoricalProductionsAsync`.
Tests: `Landing_ReturnsPlanningData_AndDefaultsToPlaneamento`, `CalendarMarkers_Represent_DistinctLineKeys_ForB1AndB2`, `ListRow_Contains_Date_Production_Reference_Machine_AndLineKey`, `SameLineProductions_UseTheSameKey`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeDataJobOnRepository`.

#### `JobOnLineColorMappingTests`
File: `tests\BA.Dmo.IntegrationTests\JobOn\JobOnLineColorMappingTests.cs`
Kind: Unit test class.
Target: `JobOnLineColor` (Web page helper).
Tests: `AllSixLines_ResolveTo_AStableKey` (Theory, 6 rows), `SameLine_AlwaysResolvesToTheSameKey`, `DifferentLines_ResolveToDifferentKeys`, `UnknownLine_ResolvesToNull_AndIsNotValid`, `CanonicalSixLineSet_MatchesThePlatformCatalog`.

### 5.8 Migrations

#### `MigrationRunnerTests`
File: `tests\BA.Dmo.IntegrationTests\Migrations\MigrationRunnerTests.cs`
Kind: Integration test class (temp dir + `FakeMigrationGateway`); `IDisposable`.
Target: `MigrationRunner` (`BA.Dmo.Infrastructure.Persistence.Migrations`).
Tests: `UnappliedMigration_IsExecutedWhole_AndRecordedAfterSuccess`, `AppliedMigrationWithSameChecksum_IsSkipped_NotReExecuted`, `AppliedMigrationWithDifferentChecksum_FailsExplicitly`, `FailedScript_IsNotRecorded_AndStopsTheRun`, `Migrations_ExecuteInCanonicalOrder_AndAllRecorded`, `EmptyFamily_SucceedsWithNothingToDo`, `ScriptsWithSemicolonsInsideStrings_AreNeverSplit`.
Setup: `Directory.CreateTempSubdirectory("ba_dmo_runner_")`; `Dispose` deletes it. Uses SHA-256 checksum comparison.

#### `MigrationDiscoveryTests`
File: `tests\BA.Dmo.IntegrationTests\Migrations\MigrationDiscoveryTests.cs`
Kind: Integration test class (temp dir); `IDisposable`.
Target: `MigrationDiscovery`.
Tests: `Discover_ReturnsCanonicalOrdinal_EvenWhenCreatedOutOfOrder`, `Discover_IsDeterministic_AcrossRepeatedCalls`, `Discover_RejectsFileOutsideTheFamilyPattern`, `Discover_RejectsDuplicateVersions`, `Discover_MissingDirectory_FailsExplicitly`, `Discover_IgnoresNonSqlFiles`, `ShippedFreshBuildFamily_IsComplete_N01ThroughN26`.

#### `MigrationChecksumTests`
File: `tests\BA.Dmo.IntegrationTests\Migrations\MigrationChecksumTests.cs`
Kind: Integration test class (temp dir); `IDisposable`.
Target: `MigrationChecksum`.
Tests: `ComputeSha256_MatchesKnownFipsVector`, `ComputeSha256File_HashesExactFileBytes`, `ComputeSha256_DetectsAnyContentChange`.

#### `MigrationArchitectureGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Migrations\MigrationArchitectureGuardTests.cs`
Kind: Reflection/architecture guard test class.
Targets: `MigrationRunner` assembly; `Program` assembly; `MigrateCommand`.
Tests: `ProductionAssemblies_ContainNoMigrationParsingOrHttpSurface`, `WebProgram_HasNoMigrationHookBeyondCliVerb`.

#### `FakeMigrationGateway`
File: `tests\BA.Dmo.IntegrationTests\Migrations\FakeMigrationGateway.cs`
Implements: `IMigrationScriptGateway`. In-memory applied-map, `ExecutedScripts`, `Records`, `EnsureTrackingTableCalled`, `FailOnScriptContaining`, `SeedApplied`.

### 5.9 Pegamentos

#### `PegamentoPdfRendererTests`
File: `tests\BA.Dmo.IntegrationTests\Pegamentos\PegamentoPdfRendererTests.cs`
Kind: Integration test class.
Target: `PegamentoPdfRenderer` (`BA.Dmo.Infrastructure.Access`).
Tests: `Render_ProducesValidPdfHeader`, `Render_IncludesProductionIdentityAndComponentData`, `Render_DoesNotEmitHtmlOrBrowserPrintArtifacts`.
Checks: PDF header/signature bytes, embedded text strings, absence of `file:///`/`.html`/page markers.

#### `PegamentoWebApiTests`
File: `tests\BA.Dmo.IntegrationTests\Pegamentos\PegamentoWebApiTests.cs`
Kind: Integration test class (WAF: nested `PegFixture`).
Targets: `/api/pegamentos/*`; `IPegamentoRepository`, `IJobOnProductionFolderResolver`, `IAppSettingsReader`, `IJobOnProductionContextLookup`.
Tests: `Anonymous_IsDenied_RedirectsToLogin` (Theory, 3 rows), `AuthorizedPegamentosUser_Search_IsAdmitted`, `UserWithoutPegamentosModule_IsDenied`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeResolver`, `FakeSettings`, `FakeContextLookup`, `FakePegRepo`.

### 5.10 Persistence

#### `DbConnectionFactoryTests`
File: `tests\BA.Dmo.IntegrationTests\Persistence\DbConnectionFactoryTests.cs`
Kind: Integration test class (no DB; unreachable endpoint for failure scenarios).
Target: `DbConnectionFactory`, `DatabaseConnectionSettings`.
Tests: `ResolveConnectionString_PrefersPrimaryVariable`, `ResolveConnectionString_FallsBackToDatabaseUrl`, `ResolveConnectionString_ReturnsNull_WhenUnconfigured`, `FromEnvironment_*`, `Constructor_RejectsEmptyConnectionString`, `Constructor_RejectsUriFormat_WithActionableMessage_AndNoLeak`, `Constructor_RejectsUnparseableString_WithConfigurationError`, `OpenFailure_IsTranslated_AndNeverLeaksCredentials`, `OpenAsync_HonorsCancellation`.
Environment: exercises `DatabaseConnectionSettings.ConnectionStringVariable` / `FallbackConnectionStringVariable`.

#### `DapperUnitOfWorkTests`
File: `tests\BA.Dmo.IntegrationTests\Persistence\DapperUnitOfWorkTests.cs`
Kind: Integration test class (lifecycle via `FakeDbConnection`).
Target: `DapperUnitOfWork`, `IDbConnectionFactory`.
Tests: `BeginAsync_OpensConnection_AndBeginsTransaction`, `RunAsync_CommitsAfterSuccess_AndReturnsResult`, `RunAsync_RollsBackOnFailure_AndRethrows`, `DisposeWithoutCommit_RollsBack_AndDisposesEverything`, `DisposeAfterCommit_DoesNotRollback`, `CommitTwice_IsRejected`, `Cancellation_PreventsCommit_AndRollsBack`, `CancellationDuringOperation_RollsBack_WithoutCommit`, `BeginTransactionFailure_DisposesTheOpenedConnection`, `ScopesAreIndependent_NoSharedStateBetweenRuns`.

#### `PersistenceMappingsTests`
File: `tests\BA.Dmo.IntegrationTests\Persistence\PersistenceMappingsTests.cs`
Kind: Integration test class.
Target: `PersistenceMappings`, `DefaultTypeMap` (Dapper).
Tests: `Configure_EnablesUnderscoreMatching_AndIsIdempotent`, `SnakeCaseColumns_MapToPascalCaseMembers`, `UnknownColumns_DoNotMap`.

#### `PersistenceArchitectureGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Persistence\PersistenceArchitectureGuardTests.cs`
Kind: Reflection/architecture guard test class.
Targets: Domain/Application/Infrastructure/Web assemblies; `DbConnectionFactory`.
Tests: `NoEfCoreOrOrmFramework_IsReferenced`, `NoGlobalOrStaticConnectionState_ExistsInProductionCode`, `NoAmbientTransactionScope_DependencyExists`, `WebLayer_DoesNotReferenceNpgsqlDirectly`, `DependencyGraph_MatchesPlanV3`, `Infrastructure_DoesNotLeakIntoDomain`.
Mechanic: reads `.csproj` `ProjectReference` entries; reflects assembly references.

#### `FakeDbConnection` (incl. `FakeDbTransaction`, `FakeConnectionFactory`)
File: `tests\BA.Dmo.IntegrationTests\Persistence\FakeDbConnection.cs`
Implements: `IDbConnection`/`IAsyncDisposable`, `IDbTransaction`, `IDbConnectionFactory`. Records `Committed`/`RollbackCount`/`Disposed`/`AsyncDisposed`; `BeginTransactionThrows`.

### 5.11 Peso

#### `PesoPdfVisualCheck`
File: `tests\BA.Dmo.IntegrationTests\Peso\PesoPdfVisualCheck.cs`
Kind: Integration test class (generates a sample PDF to file for manual inspection).
Target: `PesoSingleFilePdfRenderer` (`BA.Dmo.Infrastructure.Access`).
Test: `RenderSample_ToFile_ForManualInspection`.
Output: `<base>\sample_peso.pdf`. Checks byte length `>100` and `%PDF` header.

### 5.12 Reparação Externa

#### `ReparacaoExternaWebApiTests`
File: `tests\BA.Dmo.IntegrationTests\ReparacaoExterna\ReparacaoExternaWebApiTests.cs`
Kind: Integration test class (WAF: nested `RepExtFixture`).
Targets: `/api/reparacao-externa/*`; `IRepairRepository`, `IToolPieceResolver`, `IArmazemRepairMovementPort`.
Tests: `Anonymous_IsDenied_RedirectsToLogin` (Theory, 4 rows), `AuthorizedRepExtUser_SearchTools_IsAdmitted`, `UserWithoutRepExtModule_IsDenied`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeRepairRepo`, `FakeToolResolver`, `FakeArmazemRepair`, `FakeUowFactory`.

### 5.13 Reparação Interna

#### `ReparacaoInternaWebApiTests`
File: `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs`
Kind: Integration test class (WAF: nested `RepIntFixture`).
Targets: `/api/reparacao-interna/*`; `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, `IFerramentasPieceLookup`; `reparacao_interna.corrigir` capability.
Tests: `Anonymous_IsDenied_RedirectsToLogin` (Theory, 3 rows), `AuthorizedRepIntUser_LineCards_IsAdmitted`, `UserWithoutRepIntModule_IsDenied`, `Correcao_WithoutCorrigirCapability_IsForbidden`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeRepIntRepo`, `FakeContextLookup`, `FakePieceLookup`, `FakeUowFactory`.

### 5.14 Security

#### `NoDebugBypassGuardTests`
File: `tests\BA.Dmo.IntegrationTests\Security\NoDebugBypassGuardTests.cs`
Kind: Reflection/static-file guard test class.
Targets: production assemblies (`DomainError`, Application, Infrastructure, `Program`); `Program` entry point; `Pages/Auth/Login.cshtml.cs` + `Cli` sources.
Tests: `ProductionAssemblies_ContainNoDebugAuthBypassTypes`, `WebStartup_EntryPoint_IsTheRealProgram_CompositionRoot`, `AuthPath_Sources_HaveNoDebugBlocks_AndExactlyOneSignInCallSite` (source-level; no-op when source tree absent).

### 5.15 Tampões

#### `TampaoWebApiTests`
File: `tests\BA.Dmo.IntegrationTests\Tampoes\TampaoWebApiTests.cs`
Kind: Integration test class (WAF: nested `TampoesFixture`).
Targets: `/api/tampoes/*`; `ITampaoRepository`, `ITampoesUnitOfWorkFactory`.
Tests: `Anonymous_IsDenied_RedirectsToLogin` (Theory, 3 rows), `AuthorizedTampoesUser_Consulta_IsAdmitted`, `UserWithoutTampoesModule_IsDenied`.
Uses: nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeRepo`, `FakeUowFactory`.

---

## 6. Fixtures / Shared Test Infrastructure

### 6.1 `WebApplicationFactory<Program>` test-host fixtures

Each web integration test file defines one nested fixture extending `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>`. All override `ConfigureWebHost`/`ConfigureTestServices` to replace singletons/scoped services via local `ReplaceSingleton<T>`/`Replace<T>` helpers; clients use `AllowAutoRedirect=false` + `HandleCookies=true`.

| Fixture | File | Replaces | Antiforgery |
|---|---|---|---|
| `BoquilhasFixture` | `tests\BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs` | `ISupabaseAuthAdapter`, `IInternalUserRepository`, `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory` | disabled |
| `AfFixture` | `tests\BA.Dmo.IntegrationTests\Access\AdminFormAntiforgeryTests.cs` | auth/identity/`IAdminRepository`/`IModuleCatalogMirrorRepository`/`IAdminProvisioningAdapter` | **enforced** |
| `AdminFixture` | `tests\BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs` | auth/identity/`IAdminRepository`/`IModuleCatalogMirrorRepository` | disabled |
| `ResetFixture` | `tests\BA.Dmo.IntegrationTests\Access\AdminUserListResetTests.cs` | auth/identity/admin/provisioning/mirror | disabled |
| `ShellFixture` | `tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` | auth/identity/`IJobOnRepository`/`IPesoRepository` | disabled |
| `HistoriaFixture` | `tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs` | auth/identity/`IHistoriaRepository`/`IAdminRepository`/`IJobOnRepository` | disabled |
| `LabFixture` | `tests\BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs` | auth/identity | disabled |
| `DesignFixture` | `tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` | auth/identity/`IJobOnRepository` | disabled |
| `FerrFixture` | `tests\BA.Dmo.IntegrationTests\Ferramentas\FerramentasWebApiTests.cs` | auth/identity/`IFerramentasRepository` (scoped)/`IFerramentasRuleLookup` (scoped) | disabled |
| `LandingFixture` | `tests\BA.Dmo.IntegrationTests\JobOn\JobOnLandingTests.cs` | auth/identity/`IJobOnRepository` | disabled |
| `AuthTestFixture` | `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` | `ISupabaseAuthAdapter`, `IInternalUserRepository` | disabled |
| `AmbiguityFixture` | `tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` | auth/identity | disabled |
| `PegFixture` | `tests\BA.Dmo.IntegrationTests\Pegamentos\PegamentoWebApiTests.cs` | auth/identity/`IPegamentoRepository`/`IJobOnProductionFolderResolver`/`IAppSettingsReader`/`IJobOnProductionContextLookup` | disabled |
| `RepExtFixture` | `tests\BA.Dmo.IntegrationTests\ReparacaoExterna\ReparacaoExternaWebApiTests.cs` | auth/identity/`IRepairRepository`/`IToolPieceResolver`/`IArmazemRepairMovementPort`/`IRepairUnitOfWorkFactory` | disabled |
| `RepIntFixture` | `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs` | auth/identity/`IReparacaoInternaRepository`/`IJobOnActiveContextLookup`/`IFerramentasPieceLookup`/`IRepairUnitOfWorkFactory` | disabled |
| `TampoesFixture` | `tests\BA.Dmo.IntegrationTests\Tampoes\TampaoWebApiTests.cs` | auth/identity/`ITampaoRepository`/`ITampoesUnitOfWorkFactory` | disabled |

### 6.2 ADO.NET / HTTP / infra doubles used as test hosts

- `FakeDbConnection` / `FakeDbTransaction` / `FakeConnectionFactory` — `tests\BA.Dmo.IntegrationTests\Persistence\FakeDbConnection.cs` (lifecycle recording).
- `DataReaderDbConnection` / `DataReaderDbCommand` / `NoParameterCollection` / `FixedReaderConnectionFactory` — `tests\BA.Dmo.IntegrationTests\Access\DapperAdminRepositoryProjectionTests.cs` (capture `IssuedSql`, `WasDisposed`; in-memory `DataTableReader`).
- `FakeHttpMessageHandler` — `tests\BA.Dmo.IntegrationTests\Identity\FakeHttpMessageHandler.cs` (scriptable HTTP responses).
- `FakeMigrationGateway` — `tests\BA.Dmo.IntegrationTests\Migrations\FakeMigrationGateway.cs` (`IMigrationScriptGateway`).

### 6.3 Collection / `[CollectionDefinition]`

No `[CollectionDefinition]` found. No `[Collection("...")]` usage found. Tests do not rely on named xUnit collections.

### 6.4 Parallelization / runner config

No `xunit.runner.json`, `.runsettings`, or assembly-level parallelization/collection configuration files were found in the test projects. No `Directory.Build.targets` or `AssemblyInfo` test-collection attributes present; the only shared build file is `Directory.Build.props` (TFM/compile settings only).

---

## 7. Test Doubles

### 7.1 In-memory repository fakes (implement application repository ports)

| Fake | Implements | Path |
|---|---|---|
| `FakeJobOnRepository` | `IJobOnRepository` | `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnRepository.cs` |
| `FakeJobOnUserContextRepository` | `IJobOnUserContextRepository` | `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnUserContextRepository.cs` |
| `FakePesoRepository` | `IPesoRepository` | `tests\BA.Dmo.UnitTests\Modules\Peso\FakePesoRepository.cs` |
| `FakeArmazemRepository` | `IArmazemRepository` | `tests\BA.Dmo.UnitTests\Modules\Armazem\FakeArmazemRepository.cs` |
| `FakeBoquilhasRepository` | `IBoquilhasRepository` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs` |
| `FakeFerramentasRepository` | `IFerramentasRepository` | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FakeFerramentasRepository.cs` |
| `FakeHistoriaRepository` | `IHistoriaRepository` | `tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs` |
| `FakePegamentoRepository` | `IPegamentoRepository` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakePegamentoRepository.cs` |
| `FakeRepairRepository` | `IRepairRepository` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\FakeRepairRepository.cs` |
| `FakeReparacaoInternaRepository` | `IReparacaoInternaRepository` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| `FakeTampaoRepository` | `ITampaoRepository` | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoTestSupport.cs` |
| `FakeControloSheetRepository` | `IControloSheetRepository` | `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs` |
| `FakeAdminRepository` | `IAdminRepository` | `tests\BA.Dmo.UnitTests\Shared\Admin\FakeAdminRepository.cs` |
| `FakeBoquilhasWebRepository` | `IBoquilhasRepository` | `tests\BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` |

### 7.2 Resolver / lookup / port fakes

| Fake | Implements | Path |
|---|---|---|
| `FakeToolIdentityResolver` | `IToolIdentityResolver` | `tests\BA.Dmo.UnitTests\Modules\Armazem\FakeToolIdentityResolver.cs` |
| `FakeFerramentasIdentityLookup` | `IFerramentasIdentityLookup` | `tests\BA.Dmo.UnitTests\Modules\Armazem\ArmazemTestSupport.cs` |
| `FakeRuleLookup` | `IFerramentasRuleLookup` | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\FerramentasTestSupport.cs` |
| `FakeJobOnProductionContextLookup` | `IJobOnProductionContextLookup` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoTestSupport.cs` |
| `FakeJobOnProductionFolderResolver` | `IJobOnProductionFolderResolver` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakeJobOnProductionFolderResolver.cs` |
| `FakePegamentoPdfRenderer` | `IPegamentoPdfRenderer` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoTestSupport.cs` |
| `FakeToolPieceResolver` | `IToolPieceResolver` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs` |
| `FakeArmazemRepairMovementPort` | `IArmazemRepairMovementPort` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs` |
| `FakeJobOnActiveContextLookup` | `IJobOnActiveContextLookup` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| `FakeFerramentasPieceLookup` | `IFerramentasPieceLookup` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| `FakeControloProductionContextLookup` | `IControloProductionContextLookup` | `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs` |
| `FakeSettings` | `IAppSettingsReader` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoTestSupport.cs` |

### 7.3 Unit-of-work factory / `IDbUnitOfWork` no-op doubles

| Fake | Implements | Path |
|---|---|---|
| `FakeBqUnitOfWork` / `FakeBqUnitOfWorkFactory` | `IDbUnitOfWork` / `IBoquilhasUnitOfWorkFactory` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs` |
| `FakeControloUow` / `FakeControloUowFactory` | `IDbUnitOfWork` / `IRepairUnitOfWorkFactory` | `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs` |
| `FakeUnitOfWork` / `FakeRepairUnitOfWorkFactory` | `IDbUnitOfWork` / `IRepairUnitOfWorkFactory` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs` |
| `FakeReparacaoInternaUnitOfWork` / `FakeReparacaoInternaUowFactory` | `IDbUnitOfWork` / `IRepairUnitOfWorkFactory` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| `FakeTampaoUnitOfWork` / `FakeTampoesUnitOfWorkFactory` | `IDbUnitOfWork` / `ITampoesUnitOfWorkFactory` | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoTestSupport.cs` |
| `FakeBqWebUnitOfWork` / `FakeBqWebUnitOfWorkFactory` | `IDbUnitOfWork` / `IBoquilhasUnitOfWorkFactory` | `tests\BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs` |

### 7.4 Fixed clocks / authorship accessors / current-user accessors

Per module (names in module `*TestSupport.cs` files and test files):

| Module | Fixed Clock (`IClock`) | Authorship (`IPersistenceAuthorshipAccessor`) | Current-User (`ICurrentUserAccessor`) |
|---|---|---|---|
| Armazém | `ArmazemFixedClock` | `ArmazemFakeAuthorship` | `ArmazemCurrentUser` |
| Boquilhas | `BqFixedClock` | `BqFakeAuthorship` | `BqCurrentUser` |
| Controlo | `ControloFixedClock` | `ControloFakeAuthorship` | `ControloCurrentUser` |
| Ferramentas | `FixedClock` | `FakeAuthorshipAccessor` | `FakeCurrentUser` |
| JobOn | `FixedClock` / `PdfTestClock` / `TestClock` (file-local) | — | `FakeCurrentUserAccessor` / `PdfTestIdentityAccessor` / `LocalFakeCurrentUserAccessor` (nested) |
| Pegamentos | `FixedClock` | `FakeAuthorshipAccessor` | `PegamentoAuthorizationGate` via `FakeAuthorshipAccessor.Authorized()` |
| Peso | `FixedClock` (nested) | — | `FakeCurrentUserAccessor` (nested `GrantOperador/GrantResponsavel/GrantNone`) |
| Reparação Externa | `ReparacaoExternaFixedClock` | `ReparacaoExternaFakeAuthorship` | `ReparacaoExternaCurrentUser` |
| Reparação Interna | `ReparacaoInternaFixedClock` | `ReparacaoInternaFakeAuthorship` | `ReparacaoInternaCurrentUser` |
| Tampões | `TampaoFixedClock` | `TampaoFakeAuthorship` | `TampaoCurrentUser` |
| História | — | — | `HistoriaCurrentUser` |
| Admin | `FixedClock` (nested) | — | `FakeCurrentUserAccessor` (nested `GrantAdmin/GrantNone`) |
| Identity | `FixedClock` (nested) | — | — |

### 7.5 Other test doubles

- `NoopPdfRenderer` (`IPdfRenderer`, nested in `PesoServiceTests`), `TestPdfRenderer` (`IJobOnPdfRenderer`, nested in `JobOnPdfTests`), `NullJobOnImageProvider` (nested in `JobOnPdfTests`).
- `NoopMirror` / `FakeMirrorRepository` (`IModuleCatalogMirrorRepository`) in Admin integration files.
- `RecordingProvisioningAdapter` / `FakeProvisioning` / `FakeProvisioningAdapter` (`IAdminProvisioningAdapter`) in Admin/Identity tests.
- `FakeInternalUserRepository` / `FakeIdentityRepository` (`IInternalUserRepository`) in Identity/Admin and all WAF fixtures.
- `FakeAuthAdapter` / `FakeAuthAdapter` (`ISupabaseAuthAdapter`, with `AuthMode`) in all WAF fixtures + `WebAuthSessionTests`.
- `JobOnLineCatalog` (canonical six-line constant, in `JobOnUserContextTests.cs`).

---

## 8. Builders / Test Data Helpers

| Type | Produces | Main members | Path |
|---|---|---|---|
| `PegamentoContextBuilder` | `PegamentoProductionContext` | `Complete(jobOnId, revisionId, ...)` building CM/BQ/MF snapshots + nominals | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\PegamentoTestSupport.cs` |
| `ControloTestBuilder` | `ControloSheetService` + fakes | `Build(user, now)` returns `(service, repo, ctx)` | `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs` |
| `FakeJobOnActiveContextLookup` seed helpers | `InternalRepairContext` / resolutions | `SeedSingle`, `SeedNone`, `SeedAmbiguous`, `Context(...)` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` |
| `FakeBoquilhasRepository` seed helpers | `BqLote` / `BqTrace` / `BqRepairer` | `SeedLote`, `SeedActiveTrace`, `SeedRepairer` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs` |
| `FakeTampaoRepository` seed helper | `TampaoConfiguration` | `SeedConfiguration` | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoTestSupport.cs` |
| `FakeToolPieceResolver` seed helper | `RepairToolIdentity` | `Seed(reference, lot, number, type)` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs` |
| `FakePesoRepository` seed helpers | `PesoReference`/`PesoLote` | direct collection seeding used by tests | `tests\BA.Dmo.UnitTests\Modules\Peso\FakePesoRepository.cs` |

---

## 9. Database / Integration Test Mechanics

### 9.1 Single database-backed suite (`RemediationGuardTests`)

File: `tests\BA.Dmo.IntegrationTests\Integrity\RemediationGuardTests.cs`

- **Fixture**: test class itself (not a shared web fixture); each test connects directly via Npgsql.
- **Connection source**: environment variable `BA_DMO_TEST_DATABASE` (Npgsql keyword/value connection string).
- **Skip behavior**: when `BA_DMO_TEST_DATABASE` is absent, each test calls `SkipIfNoDatabase()` which prints `[SKIP]` and returns before executing (suite stays green with no DB).
- **Schema precondition**: assumes the target schema is fully migrated (N01–N25).
- **Isolation**: fresh GUID keys per run; `ON CONFLICT DO NOTHING` for seeded `access_templates`; no destructive teardown.
- **Helpers within the class**: `Exec` (ExecuteNonQuery), `CaptureSqlState` (returns `PostgresException.SqlState`), `CaptureMessage`, `EnsureTemplateAsync`, `SeedJobWithRevisionAsync`, `SeedPesoControloAsync`.
- **Assertions**: SQLSTATE equality (`23505`, `23502`, `23514`), trigger/message containment, `pg_trigger`/`pg_indexes`/`pg_policy`/`information_schema` counts.

### 9.2 Migration tests (SQL execution via gateway double — no DB)

Files: `tests\BA.Dmo.IntegrationTests\Migrations\MigrationRunnerTests.cs`, `MigrationDiscoveryTests.cs`, `MigrationChecksumTests.cs`.

- **Temp mechanism**: `Directory.CreateTempSubdirectory("ba_dmo_runner_"/"ba_dmo_migrations_"/"ba_dmo_checksum_")` in constructor; `Dispose()` deletes the directory recursively (`IDisposable`).
- **Whole-script execution**: `MigrationRunner` executes each migration file byte-for-byte (no statement splitting); recorded only after success with SHA-256 checksum (`MigrationChecksum.ComputeSha256File`).
- **Skip/failure semantics**: same-checksum applied → skipped; different checksum → `MigrationChecksumMismatchException`; failed script → not recorded and run stops (`MigrationExecutionException`).
- **Discovery ordering**: canonical N01…N26 ordinal regardless of creation order; rejects files outside family pattern, duplicate versions, missing directory; ignores non-SQL files.

### 9.3 Persistence / connection tests (no live DB)

Files: `tests\BA.Dmo.IntegrationTests\Persistence\DbConnectionFactoryTests.cs`, `DapperUnitOfWorkTests.cs`, `PersistenceMappingsTests.cs`.

- `DbConnectionFactoryTests` uses an unreachable endpoint (`Host=127.0.0.1;Port=9`) to exercise open-failure translation and cancellation without a real database; asserts no credential leakage.
- `DapperUnitOfWorkTests` verifies transaction lifecycle (begin/commit/rollback/dispose) via `FakeDbConnection` and injected `IDbConnectionFactory`; verifies cancellation prevents commit and rolls back.
- `PersistenceMappingsTests` verifies Dapper `DefaultTypeMap` snake_case↔PascalCase mapping after `PersistenceMappings.Configure()` (idempotent).

---

## 10. Web / HTTP Test Mechanics

### 10.1 Test host

- Used: `Microsoft.AspNetCore.Mvc.Testing` → `WebApplicationFactory<Program>` (see §6.1 fixtures).
- `CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true })`.
- Services replaced in `ConfigureTestServices`; scoped ports replaced with `AddScoped`, host singletons with `AddSingleton`.
- Antiforgery: disabled for scripted posts via `RazorPagesOptions.Conventions` (`IgnoreAntiforgeryTokenAttribute`) except `AfFixture` (which enforces the antiforgery pipeline in its test fixture).

### 10.2 Form / antiforgery mechanics

- `PostFormAsync` helper fetches the page HTML, extracts `__RequestVerificationToken`, and POSTs the form with the token.
- `AdminFormAntiforgeryTests` asserts: token rendered; tokenless post rejected 400 and writes nothing; cross-session token rejected 400; anonymous/operator posts denied.

### 10.3 Directly requested HTTP targets (endpoints/routes)

Razor pages: `/login`, `/logout`, `/`, `/no-access`, `/access-denied`, `/jobon`, `/boquilhas`, `/historia`, `/peso`, `/peso/responsavel`, `/pegamentos`, `/ferramentas`, `/armazem`, `/reparacao-interna`, `/reparacao-externa`, `/tampoes`, `/admin`, `/admin/users`, `/admin/users/create`, `/admin/users/edit`, `/admin/templates/edit`, `/admin/applications`, `/admin/audit`, `/design-laboratorio`.
API routes: `/api/boquilhas/lotes`, `/api/boquilhas/movements`, `/api/boquilhas/discrepancies`, `/api/ferramentas/references`, `/api/ferramentas/lotes/{id}/rules`, `/api/pegamentos/search`, `/api/pegamentos/context/{id}`, `/api/pegamentos/revision/{id}`, `/api/reparacao-externa`, `/api/reparacao-externa/repairers`, `/api/reparacao-externa/historico`, `/api/reparacao-externa/tools`, `/api/reparacao-interna/line-cards`, `/api/reparacao-interna/context`, `/api/reparacao-interna/historico`, `/api/reparacao-interna/{id}/corrigir`, `/api/tampoes/consulta`, `/api/tampoes/movimentos`, `/api/tampoes/opcoes/fields`.

### 10.4 Response assertions

- Status codes: `HttpStatusCode.Redirect` (302), `Forbidden` (403), `Unauthorized`, `BadRequest` (400), `OK` (200), `NoContent`.
- Redirect location checked against trusted routes (open-redirect negative assertions in `WebAuthSessionTests`).
- Session cookie presence via `Set-Cookie` header; `HandleCookies=true` for cookie round-trips.
- Body content checked via `HtmlDecode` for banner/error text and absence of secrets.
- JSON API bodies decoded with `System.Text.Json` (incl. `JsonStringEnumConverter` in `BoquilhasWebAuthorizationTests`).

### 10.5 HTTP adapter tests (Supabase)

`SupabaseAuthAdapterTests` / `SupabaseAdminProvisioningAdapterTests` drive the adapters through `FakeHttpMessageHandler` with scripted `HttpResponseMessage`s. They assert: exact request URIs, `apikey`/`Authorization: Bearer` headers, status→`ErrorCategory` mapping, idempotent duplicate handling, paginated email lookup, and absence of secrets in surfaced errors.

---

## 11. Assertion / Mocking Patterns

### 11.1 Assertion framework

**xUnit** (classic `Assert.*`) is used throughout both projects. No FluentAssertions/NUnit/MSTest. Common methods: `Assert.Equal`, `Assert.True/False`, `Assert.Single`, `Assert.Empty`, `Assert.Contains`, `Assert.DoesNotContain`, `Assert.StartsWith`, `Assert.EndsWith`, `Assert.NotEqual`, `Assert.All`, `Assert.Throws`, `Assert.ThrowsAsync`, `Assert.NotNull/Null`.

### 11.2 Mocking / substitute library

**None.** No NSubstitute, Moq, or FakeItEasy. All test doubles are hand-written fakes/stubs (see §7) confined to `tests/*`. The only dynamic-object use is `System.Dynamic.ExpandoObject` inside `FakeJobOnRepository.DuplicateAtomicallyAsync` for row-hydration, not mocking.

### 11.3 Exception/assertion contracts

Domain/infrastructure exceptions directly asserted: `ConcurrencyConflictException`, `SchemaMigrationRequiredException`, `AmbiguousIdentityException`, `MigrationChecksumMismatchException`, `MigrationDiscoveryException`, `MigrationExecutionException`, `DatabaseConnectionException`, `ArmazemLocationOccupiedException`.

### 11.4 Result/error-code assertion pattern

Service tests assert `Result.IsSuccess/IsFailure`, then check `result.Error.Category` (e.g. `ErrorCategory.Forbidden`, `ValidationError`, `DomainConflict`, `NotFound`, `BackendUnavailable`, `ConcurrencyConflict`, `Unauthorized`) and/or `result.Error.Code` (e.g. `JOBON_NOT_FOUND`, `PESO_CONTROL_NO_READING`, `BQ_DUPLICATE_LOT`, `FERRAMENTAS_DUPLICATE_REFERENCE`, `REPEXT_TYPE_SCOPE`, `REPINT_OPERATOR_REQUIRED`, `TAMPAO_NEGATIVE_BALANCE`, `ARMZ_LOCATION_CODE`, `ADMIN_SELF_LOCKOUT`, `PEGAMENTO_OUTPUT_ROOT_MISSING`).

---

## 12. Parameterized / Conditional Tests

### 12.1 Parameterized tests (`[Theory]`)

xUnit `[Theory]` with `[InlineData]` (no `[MemberData]`/`[ClassData]`). Representative uses:

- Dictionary/codec round-trips: `JobOnDomainTests.Codec_*`, `PesoDomainTests`, `WeightCalculatorTests.LookupDensity` (31 rows), `DomainErrorTests`, `CapabilityAndModuleDefinitionTests`, `CanonicalPageCatalogTests.RouteGrammar_*`, `AccessTemplateGrantsParserTests`, `ReparacaoInternaDomainTests.Create_StructurallyInvalid_IsARejection`, `TampaoDomainTests`.
- Web anonymous-authorization routes: `FerramentasWebApiTests`, `PegamentoWebApiTests`, `ReparacaoExternaWebApiTests`, `ReparacaoInternaWebApiTests`, `TampaoWebApiTests` (per-endpoint `[InlineData]`).
- CLI mode routing: `CliRoutingTests.OperationalVerbs_AreDistinguished` (5 rows), `NonVerbLeadingArgument_FallsBackToWebStartup` (3 rows).
- Admin template validation: `AdminTemplateServiceTests.CreateTemplate_InvalidGrants_AreRejected_WithExplicitReport` (3 rows).
- Antiforgery posts: `AdminFormAntiforgeryTests.TokenlessPost_IsRejected400_AndWritesNothing` (3 rows).

### 12.2 Conditional / skipped tests

`[Fact(Skip = ...)]`, `[Trait]`, `[Category]`, `[Explicit]` and conditional-`Skip` attributes: **none found**.

Environment-guarded behavior:

- `RemediationGuardTests` (database suite): each test checks `BA_DMO_TEST_DATABASE`; when absent the test prints `[SKIP]` and returns (no failure / no throw).
- `NoDebugBypassGuardTests.AuthPath_Sources_HaveNoDebugBlocks_AndExactlyOneSignInCallSite`: when the web source tree is absent next to the build output (e.g. CI without sources) it returns early as a no-op.

---

## 13. Target-to-Test Index

Reverse navigation: production target → test class(es) → test project.

| Production Target | Test Class(es) | Test Project |
|---|---|---|
| `JobOnService` | `JobOnServiceTests`, `JobOnUserContextTests`, `JobOnPdfTests` | UnitTests |
| `JobOn` (domain entity) | `JobOnDomainTests` | UnitTests |
| `JobOnVerificationGenerator` | `JobOnVerificationGeneratorTests` | UnitTests |
| `JobOnActivityResolver` | `JobOnActivityResolverTests` | UnitTests |
| `JobOnPdfService` | `JobOnPdfTests` | UnitTests |
| `PesoService` | `PesoServiceTests` | UnitTests |
| `PesoControl` | `PesoControlWorkflowTests` | UnitTests |
| `PesoValidator` / codecs / `WeightCalculator` | `PesoDomainTests`, `WeightCalculatorTests` | UnitTests |
| `ArmazemService` | `ArmazemServiceTests` | UnitTests |
| `ArmazemAuthorizationGate` | `ArmazemAuthorizationGateTests` | UnitTests |
| `WarehouseStockRules` / `WarehouseLocation` | `WarehouseStockRulesTests` | UnitTests |
| `FerramentasArmazemToolIdentityResolver` | `FerramentasArmazemToolIdentityResolverTests` | UnitTests |
| `BoquilhasService` | `BoquilhasServiceTests` | UnitTests |
| `BqAuthorizationGate` | `BqAuthorizationGateTests` | UnitTests |
| `BqInventoryCalculator` / `BqRules` | `BqInventoryCalculatorTests` | UnitTests |
| `ControloSheetService` | `ControloSheetServiceTests` | UnitTests |
| `ControloFolha` | `ControloFolhaTests` | UnitTests |
| `FerramentasService` | `FerramentasServiceTests`, `FerramentasUtilisationServiceTests` | UnitTests |
| `Ferramentas` domain types | `FerramentasDomainTests` | UnitTests |
| `HistoriaService` / `HistoriaAuthorizationGate` | `HistoriaServiceTests`, `HistoriaAuthorizationGateTests` | UnitTests |
| `PegamentoService` / `PegamentoPdfService` | `PegamentoServiceTests`, `PegamentoPdfTests`, `PegamentoHistoricalRelationshipTests`, `PegamentoDocumentConfirmationTests`, `JobOnProductionFolderResolverTests` | UnitTests |
| `PegamentoMeasurementCalculator` | `PegamentoMeasurementCalculatorTests` | UnitTests |
| `ReparacaoExternaService` | `ReparacaoExternaServiceTests`, `RepairerCapabilityTests` | UnitTests |
| `RepairExitStatusMachine` | `RepairExitStatusMachineTests` | UnitTests |
| `ReparacaoExternaAuthorizationGate` | `ReparacaoExternaAuthorizationGateTests` | UnitTests |
| `ReparacaoInternaService` | `ReparacaoInternaServiceTests` | UnitTests |
| `InternalRepairRecord` / `InternalRepairRules` | `ReparacaoInternaDomainTests` | UnitTests |
| `ReparacaoInternaProductionProjection` | `ReparacaoInternaProductionProjectionTests` | UnitTests |
| `TampaoService` | `TampaoServiceTests`, `TampaoMachineTests` | UnitTests |
| `TampaoConfigurationKey` / `TampaoRules` | `TampaoDomainTests` | UnitTests |
| `AccessResolver` / `EffectiveAccess` | `AccessResolverTests` | UnitTests |
| `CurrentUser` / `ICurrentUserAccessor` | `CurrentUserTests` | UnitTests |
| `ModuleCatalog` / `ModuleDefinition` | `ModuleCatalogTests` | UnitTests |
| `CanonicalModuleCatalog` | `CanonicalModuleCatalogTests` | UnitTests |
| `CanonicalPageCatalog` / `PageDefinition` | `CanonicalPageCatalogTests` | UnitTests |
| `Capability` | `CapabilityAndModuleDefinitionTests` | UnitTests |
| `CatalogValidator` | `CatalogValidatorTests` | UnitTests |
| `GrantNormalizer` | `GrantNormalizerTests` | UnitTests |
| `NavigationService` | `NavigationServiceTests` | UnitTests |
| `ModuleCatalogMirrorSynchronizer` | `ModuleCatalogMirrorSynchronizerTests` | UnitTests |
| `AdminUserService` | `AdminUserServiceTests` | UnitTests |
| `AdminAuditService` / `AdminMirrorService` | `AdminAuditAndMirrorTests` | UnitTests |
| `AdminTemplateService` | `AdminTemplateServiceTests` | UnitTests |
| `IdentityResolutionService` | `IdentityResolutionServiceTests` | UnitTests |
| `AccessTemplateGrantsParser` | `AccessTemplateGrantsParserTests` | UnitTests |
| `BootstrapAdminService` | `BootstrapAdminServiceTests` | UnitTests |
| `SystemClock` / `IClock` | `ClockTests` | UnitTests |
| `Result` | `ResultTests` | UnitTests |
| `DomainError` / `ErrorCategory` | `DomainErrorTests` | UnitTests |
| `ConcurrencyGuard` | `ConcurrencyGuardTests` | UnitTests |
| `PersistenceAuthorship` | `PersistenceAuthorshipTests` | UnitTests |
| `Program` + /admin Razor pages | `AdminSecurityGuardTests`, `AdminFormAntiforgeryTests`, `AdminWebAuthorizationTests`, `AdminUserListResetTests`, `CatalogCompositionGuardTests` | IntegrationTests |
| `Program` + module routes / shell | `ShellRoutingTests` | IntegrationTests |
| `Program` + `/boquilhas` + Boquilhas API | `BoquilhasWebAuthorizationTests` | IntegrationTests |
| `Program` + `/historia` + `IHistoriaRepository` | `HistoriaWebAuthorizationTests` | IntegrationTests |
| `DapperAdminRepository` | `DapperAdminRepositoryProjectionTests` | IntegrationTests |
| `/login` + session + `ISupabaseAuthAdapter` | `WebAuthSessionTests`, `IdentityAmbiguityLandingTests` | IntegrationTests |
| `SupabaseAuthAdapter` | `SupabaseAuthAdapterTests` | IntegrationTests |
| `SupabaseAdminProvisioningAdapter` | `SupabaseAdminProvisioningAdapterTests` | IntegrationTests |
| `SessionClaims` / `IAdminProvisioningAdapter` wiring | `IdentitySecurityGuardTests` | IntegrationTests |
| `/api/ferramentas/*` | `FerramentasWebApiTests` | IntegrationTests |
| `/api/reparacao-externa/*` | `ReparacaoExternaWebApiTests` | IntegrationTests |
| `/api/reparacao-interna/*` | `ReparacaoInternaWebApiTests` | IntegrationTests |
| `/api/tampoes/*` | `TampaoWebApiTests` | IntegrationTests |
| `/api/pegamentos/*` | `PegamentoWebApiTests` | IntegrationTests |
| `/jobon` landing + `IJobOnRepository` | `JobOnLandingTests` | IntegrationTests |
| `JobOnLineColor` | `JobOnLineColorMappingTests` | IntegrationTests |
| `MigrationRunner` / `MigrationDiscovery` / `MigrationChecksum` / `IMigrationScriptGateway` | `MigrationRunnerTests`, `MigrationDiscoveryTests`, `MigrationChecksumTests`, `MigrationArchitectureGuardTests` | IntegrationTests |
| N25_remediation.sql schema (PostgreSQL) | `RemediationGuardTests` | IntegrationTests |
| `DbConnectionFactory` / `DatabaseConnectionSettings` | `DbConnectionFactoryTests` | IntegrationTests |
| `DapperUnitOfWork` / `IDbConnectionFactory` | `DapperUnitOfWorkTests` | IntegrationTests |
| `PersistenceMappings` / `DefaultTypeMap` | `PersistenceMappingsTests` | IntegrationTests |
| Persistence dependency graph / assemblies | `PersistenceArchitectureGuardTests` | IntegrationTests |
| `PegamentoPdfRenderer` | `PegamentoPdfRendererTests` | IntegrationTests |
| `PesoSingleFilePdfRenderer` | `PesoPdfVisualCheck` | IntegrationTests |
| `BootstrapAdminCommand` / `MigrateCommand` / `CliModeResolver` | `BootstrapAdminCliTests`, `CliCommandPlaceholderTests`, `CliRoutingTests`, `MigrateCliTests` | IntegrationTests |
| Design-system static assets | `DesignSystemGuardTests`, `ShellAndCalendarGuardTests`, `JobOnScriptSafetyGuardTests` | IntegrationTests |
| Production auth path (no debug bypass) | `NoDebugBypassGuardTests` | IntegrationTests |

---

## 14. Module / Area Test Index

Filename/class navigation by module folder (as present on disk).

| Module / Area | Test Classes |
|---|---|
| Job On | `JobOnServiceTests`, `JobOnDomainTests`, `JobOnPdfTests`, `JobOnVerificationGeneratorTests`, `JobOnActivityResolverTests`, `JobOnUserContextTests`, `JobOnRevisionImmutabilityIntegrationTests` (UnitTests); `JobOnLandingTests`, `JobOnLineColorMappingTests` (IntegrationTests) |
| Peso | `PesoServiceTests`, `PesoDomainTests`, `WeightCalculatorTests`, `PesoControlWorkflowTests` (UnitTests); `PesoPdfVisualCheck` (IntegrationTests) |
| Armazém | `ArmazemServiceTests`, `WarehouseStockRulesTests`, `ArmazemAuthorizationGateTests`, `FerramentasArmazemToolIdentityResolverTests` |
| Boquilhas | `BoquilhasServiceTests`, `BqAuthorizationGateTests`, `BqInventoryCalculatorTests` |
| Controlo | `ControloSheetServiceTests`, `ControloFolhaTests` |
| Ferramentas | `FerramentasServiceTests`, `FerramentasDomainTests`, `FerramentasUtilisationServiceTests` |
| História | `HistoriaServiceTests`, `HistoriaAuthorizationGateTests` |
| Pegamentos | `PegamentoServiceTests`, `PegamentoPdfTests`, `PegamentoMeasurementCalculatorTests`, `PegamentoHistoricalRelationshipTests`, `PegamentoDocumentConfirmationTests`, `JobOnProductionFolderResolverTests` |
| Reparação Externa | `ReparacaoExternaServiceTests`, `RepairExitStatusMachineTests`, `RepairerCapabilityTests`, `ReparacaoExternaAuthorizationGateTests` |
| Reparação Interna | `ReparacaoInternaServiceTests`, `ReparacaoInternaDomainTests`, `ReparacaoInternaProductionProjectionTests` |
| Tampões | `TampaoServiceTests`, `TampaoDomainTests`, `TampaoMachineTests` |
| Admin / Access | `AccessResolverTests`, `ModuleCatalogTests`, `CanonicalModuleCatalogTests`, `CanonicalPageCatalogTests`, `CapabilityAndModuleDefinitionTests`, `CatalogValidatorTests`, `GrantNormalizerTests`, `NavigationServiceTests`, `ModuleCatalogMirrorSynchronizerTests`, `CurrentUserTests`; `AdminUserServiceTests`, `AdminAuditAndMirrorTests`, `AdminTemplateServiceTests`; `NoDebugBypassGuardTests` |
| Identity / Auth | `IdentityResolutionServiceTests`, `AccessTemplateGrantsParserTests`, `BootstrapAdminServiceTests`; `SupabaseAuthAdapterTests`, `SupabaseAdminProvisioningAdapterTests`, `IdentitySecurityGuardTests`, `WebAuthSessionTests`, `IdentityAmbiguityLandingTests` |
| Kernel | `ClockTests`, `ResultTests`, `DomainErrorTests` |
| Persistence | `ConcurrencyGuardTests`, `PersistenceAuthorshipTests` (UnitTests); `DbConnectionFactoryTests`, `DapperUnitOfWorkTests`, `PersistenceMappingsTests`, `PersistenceArchitectureGuardTests` (IntegrationTests) |
| Migrations | `MigrationRunnerTests`, `MigrationDiscoveryTests`, `MigrationChecksumTests`, `MigrationArchitectureGuardTests` |
| Database / Integrity | `RemediationGuardTests` |
| Web / Shell / Design | `ShellRoutingTests`, `BoquilhasWebAuthorizationTests`, `HistoriaWebAuthorizationTests`, `AdminWebAuthorizationTests`, `AdminFormAntiforgeryTests`, `AdminUserListResetTests`, `AdminSecurityGuardTests`, `CatalogCompositionGuardTests`, `FerramentasWebApiTests`, `ReparacaoExternaWebApiTests`, `ReparacaoInternaWebApiTests`, `TampaoWebApiTests`, `PegamentoWebApiTests`, `JobOnLandingTests`; `DesignSystemGuardTests`, `ShellAndCalendarGuardTests`, `JobOnScriptSafetyGuardTests` |
| CLI | `BootstrapAdminCliTests`, `CliCommandPlaceholderTests`, `CliRoutingTests`, `MigrateCliTests` |
| PDF / documents | `JobOnPdfTests`, `PegamentoPdfTests`, `PegamentoPdfRendererTests`, `PesoPdfVisualCheck` |

---

## 15. Count Summary by Project

Counting rules:

- **Test classes** = classes containing at least one `[Fact]` or `[Theory]` method (determined by brace-scope scan of each declared class across all `.cs` files). Fixture/DTO/helper-only classes and generated classes are excluded unless they contain a test method.
- **Test methods** = occurrences of `[Fact]` + `[Theory]` (each method counted once; `[InlineData]` rows are not separate methods).
- **Fixtures** = the 16 `WebApplicationFactory<Program>` test-host fixture classes.
- **Helpers/Test Doubles** = distinct hand-written fake/stub/builder/current-user/clock/authorship/UoW/HTTP/ADO.NET double classes (excluding test classes, DTOs and the single `EffectiveAccessTestExtensions` static extension class).

| Project | Source Files | Test Classes | Test Methods | Fixtures/Helpers |
|---|---|:---:|:---:|:---:|
| `BA.Dmo.UnitTests` | 80 | 62 | 543 | fakes/helpers/doubles (no web fixtures) |
| `BA.Dmo.IntegrationTests` | 44 | 40 | 205 | 16 web fixtures + fakes/infra doubles |
| **Total** | **124** | **102** | **748** | **16 web fixtures + ~115 helpers/doubles** |

> Note: test-class split (62 unit / 40 integration) is based on files containing `[Fact]`/`[Theory]` (each test-method file maps to one `*Tests`-named class). Fixtures = the 16 `WebApplicationFactory<Program>` test-host fixtures. Helper/double figures (~115 distinct) count fake/stub/builder/current-user/clock/authorship/UoW/HTTP/ADO.NET/seed double classes across both projects (nested doubles declared inside test files counted once by name; DTOs, the single static extension class, and the 16 web fixtures excluded).

---

## 16. Count Summary by Area

| Area | Test Classes | Test Methods |
|---|---|:---:|
| Job On (incl. revision immutability) | 7 | ~90 |
| Peso | 4 | ~50 |
| Armazém | 4 | ~25 |
| Boquilhas | 3 | ~20 |
| Controlo | 2 | ~12 |
| Ferramentas | 3 | ~25 |
| História | 2 | ~9 |
| Pegamentos | 6 | ~25 |
| Reparação Externa | 4 | ~30 |
| Reparação Interna | 3 | ~25 |
| Tampões | 3 | ~30 |
| Shared (Access/Admin/Identity/Kernel/Persistence unit) | 25 | ~200 |
| Integration — Web/Shell/Design | 12 | ~70 |
| Integration — Identity/Auth | 5 | ~40 |
| Integration — Migrations | 4 | ~20 |
| Integration — Persistence/Optim | 5 | ~35 |
| Integration — Database/Integrity | 1 | 10 |
| Integration — CLI | 4 | ~15 |
| Integration — API endpoints (Ferramentas/RepExt/RepInt/Tampões/Pegamentos/JobOn) | 6 | ~27 |

Area counts are approximate translations of the verified test files/methods; exact totals are the project totals in §15.

---

## 17. Source Locations

All exact paths are listed in §3 (Global Test Inventory), §4 and §5. The two project files are:

- `D:\BA-DMO-CODEX-CLEAN\tests\BA.Dmo.UnitTests\BA.Dmo.UnitTests.csproj`
- `D:\BA-DMO-CODEX-CLEAN\tests\BA.Dmo.IntegrationTests\BA.Dmo.IntegrationTests.csproj`

Shared build settings: `D:\BA-DMO-CODEX-CLEAN\Directory.Build.props`, `D:\BA-DMO-CODEX-CLEAN\global.json`.

---

## Sources Verified

Primary evidence (all current test source, read from disk):

- `tests\BA.Dmo.UnitTests\` (80 `.cs` files across `Modules\*` and `Shared\*`)
- `tests\BA.Dmo.IntegrationTests\` (44 `.cs` files across `Access`, `Cli`, `Design`, `Ferramentas`, `Identity`, `Integrity`, `JobOn`, `Migrations`, `Pegamentos`, `Persistence`, `Peso`, `ReparacaoExterna`, `ReparacaoInterna`, `Security`, `Tampoes`)
- Test `.csproj` files (`BA.Dmo.UnitTests.csproj`, `BA.Dmo.IntegrationTests.csproj`)
- `Directory.Build.props`, `global.json`

Project targets referenced in tests:

- `src\BA.Dmo.Domain`, `src\BA.Dmo.Application`, `src\BA.Dmo.Infrastructure`, `src\BA.Dmo.Web`

Registry reference: `maps\00_INDEX.md` (mapping contract/registry only).

Not used as test evidence: 01_DOMAIN.md, 02_DATABASE.md, 03_MIGRATIONS.md, 04_DAPPER_INFRASTRUCTURE.md, Design/SOT, historical pass logs.

**Scope disclaimer:** This map is a pure technical inventory of test code and location. It does not use Design/SOT interpretation, does not perform coverage or gap analysis, and does not judge test quality.