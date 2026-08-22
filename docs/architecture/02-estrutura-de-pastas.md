# 02 — Estrutura de Pastas

## 1. Visão geral do repositório

```
Worfair/
├── Worfair.sln
├── Directory.Build.props              # compilação, nullable, LangVersion, regras de análise
├── Directory.Build.targets
├── .editorconfig
├── .gitignore
├── .dockerignore
├── docker-compose.yml                 # postgres, (local) mailhog, etc.
├── .github/
│   └── workflows/
│       ├── ci.yml                     # build, testes, arquitetura (NetArchTest), cobertura
│       └── release.yml
├── docs/
│   ├── adr/                           # Architecture Decision Records
│   │   └── template.md
│   └── architecture/                  # este conjunto de documentos
├── src/
│   ├── BuildingBlocks/                # (seção 2)
│   ├── Modules/                       # (seção 3)
│   │   ├── Identity/
│   │   ├── Tenants/
│   │   ├── Jobs/
│   │   ├── Recruitment/
│   │   ├── Proposals/
│   │   ├── Financial/
│   │   └── Notifications/
│   └── Api/
│       └── Worfair.Api/               # host: Program.cs, módulos de registro, middleware
└── tests/
    ├── Unit/                          # testes por módulo (Domain + Application)
    ├── Integration/                   # EF Core real (Testcontainers/Postgres)
    └── E2E/                           # fluxos ponta a ponta via API
```

## 2. Building Blocks (núcleo compartilhado)

Contratos genéricos **sem lógica de negócio**. Única dependência permitida para os
módulos (D-07).

```
src/BuildingBlocks/
├── Worfair.BuildingBlocks.Domain/
│   ├── Entities/
│   │   ├── Entity.cs                  # base (igualdade por ID)
│   │   └── AggregateRoot.cs           # + lista de domain events
│   ├── ValueObjects/
│   │   ├── ValueObject.cs             # base (igualdade estrutural)
│   │   ├── TypedId.cs                 # helper para IDs tipados
│   │   └── TenantId.cs                # ID do tenant (usado por todos os módulos)
│   ├── DomainEvents/
│   │   ├── IDomainEvent.cs
│   │   └── DomainEvent.cs             # base com Timestamp
│   ├── Repositories/
│   │   ├── IRepository.cs
│   │   └── ISpecification.cs
│   ├── Errors/
│   │   ├── Error.cs
│   │   ├── Result.cs                  # Result<T> / Result (D-10)
│   │   └── ResultExtensions.cs
│   ├── Tenancy/
│   │   ├── ITenantEntity.cs           # marca entidades tenant-owned
│   │   └── ITenantProvider.cs         # resolve o tenant corrente
│   └── Abstractions/
│       ├── IUnitOfWork.cs
│       └── IDomainEventDispatcher.cs
├── Worfair.BuildingBlocks.Application/
│   ├── Cqrs/
│   │   ├── ICommand.cs
│   │   ├── IQuery.cs
│   │   └── (base handlers)
│   ├── Pipelines/                     # behaviors MediatR (validation, logging, transaction)
│   ├── Contracts/
│   │   ├── IIntegrationEvent.cs
│   │   └── IEventBus.cs               # publish/subscribe assíncrono
│   ├── Ports/                         # abstrações de infra (cache, clock, id generation)
│   │   ├── IDateTimeProvider.cs
│   │   └── ICacheService.cs
│   └── Validation/
│       └── (abstração de validators)
├── Worfair.BuildingBlocks.Infrastructure/
│   ├── Persistence/
│   │   ├── Tenant/
│   │   │   ├── TenantSaveChangesInterceptor.cs
│   │   │   └── TenantQueryFilterExtensions.cs
│   │   ├── Outbox/
│   │   │   ├── OutboxMessage.cs
│   │   │   ├── IOutboxProcessor.cs
│   │   │   └── OutboxBackgroundService.cs
│   │   └── (helpers de EF: SnapshotHelper, Serialization)
│   ├── Events/
│   │   └── InProcessEventBus.cs       # transporte padrão (lançado por módulo)
│   ├── Caching/
│   │   └── MemoryCacheService.cs
│   └── Clock/
│       └── SystemDateTimeProvider.cs
└── Worfair.BuildingBlocks.Contracts/
    └── IntegrationEvents/
        └── IntegrationEvent.cs        # base com Id, OccurredOn, TenantId
```

**Regra:** um módulo **pode** depender de todos os Building Blocks; Building Blocks
**não** dependem de módulos.

## 3. Estrutura interna de cada módulo

Cada módulo (ex.: `Recruitment`) é composto por **cinco projetos**:

```
src/Modules/Recruitment/
├── Worfair.Modules.Recruitment.Domain/           # (a) domínio puro
├── Worfair.Modules.Recruitment.Application/      # (b) casos de uso
├── Worfair.Modules.Recruitment.Infrastructure/   # (c) infraestrutura (EF Core, repositórios)
├── Worfair.Modules.Recruitment.Contracts/        # (d) contratos de integração
└── Worfair.Modules.Recruitment/                  # (e) composition root do módulo
```

### (a) Domain — exemplo Recruitment

```
Worfair.Modules.Recruitment.Domain/
├── Abstractions/
│   ├── IJobRequisitionRepository.cs
│   └── ICandidateRepository.cs
├── Aggregates/
│   ├── JobRequisition/
│   │   ├── JobRequisition.cs            # Aggregate Root
│   │   ├── JobRequisitionId.cs          # typed id
│   │   ├── JobRequisitionStatus.cs      # enum com transições
│   │   ├── HiringTeamMember.cs          # entidade filha
│   │   └── Events/
│   │       ├── JobRequisitionCreatedDomainEvent.cs
│   │       └── JobRequisitionPublishedDomainEvent.cs
│   └── Candidate/
│       ├── Candidate.cs
│       ├── CandidateId.cs
│       ├── CandidateStatus.cs
│       └── Events/
│           ├── CandidateAppliedDomainEvent.cs
│           └── CandidateHiredDomainEvent.cs
├── Interviews/
│   ├── Interview.cs
│   └── InterviewId.cs
├── ValueObjects/
│   ├── JobTitle.cs
│   ├── SalaryRange.cs
│   ├── Money.cs
│   ├── ContactEmail.cs
│   └── ExperienceLevel.cs
├── DomainServices/
│   └── CandidateSourcingService.cs
├── Errors/
│   ├── JobRequisitionErrors.cs
│   └── CandidateErrors.cs
└── Enums/
    ├── HiringRole.cs                    # Recruiter, Sourcer, Interviewer, HiringManager
    └── InterviewType.cs
```

### (b) Application

```
Worfair.Modules.Recruitment.Application/
├── Abstractions/
│   ├── IRecruitmentReadRepository.cs    # consultas read-only (síncronas p/ outros módulos)
│   └── IRecruitmentUnitOfWork.cs
├── JobRequisitions/
│   ├── Commands/
│   │   ├── CreateJobRequisition/
│   │   │   ├── CreateJobRequisitionCommand.cs
│   │   │   ├── CreateJobRequisitionCommandValidator.cs
│   │   │   └── CreateJobRequisitionCommandHandler.cs
│   │   ├── PublishJobRequisition/
│   │   │   └── (…)
│   │   └── (Close, AddTeamMember, ChangeSalaryRange…)
│   ├── Queries/
│   │   ├── GetJobRequisition/…
│   │   └── ListJobRequisitions/…
│   └── Dtos/
│       ├── JobRequisitionDto.cs
│       └── JobRequisitionListItemDto.cs
├── Candidates/
│   └── (Commands/Queries/Dtos…)
├── EventHandlers/                       # consome domain events → dispatch p/ Integration Events
│   └── JobRequisitionPublishedHandler.cs
└── Integration/                         # consome integration events de OUTROS módulos
    └── JobPostingCreatedConsumer.cs
```

### (c) Infrastructure

```
Worfair.Modules.Recruitment.Infrastructure/
├── Persistence/
│   ├── RecruitmentDbContext.cs          # DbContext do módulo (schema "recruitment")
│   ├── Configurations/
│   │   ├── JobRequisitionConfiguration.cs
│   │   └── CandidateConfiguration.cs
│   ├── Migrations/
│   │   └── (dotnet ef migrations …)
│   └── Repositories/
│       ├── JobRequisitionRepository.cs
│       └── CandidateRepository.cs
├── IntegrationEvents/
│   ├── JobRequisitionPublishedIntegrationEvent.cs
│   └── (handlers de publicação via Outbox)
└── ReadModels/
    └── (projeções read-only)
```

### (d) Contracts

```
Worfair.Modules.Recruitment.Contracts/
├── Events/
│   ├── JobRequisitionPublishedIntegrationEvent.cs   # para Notifications, Financial
│   └── CandidateHiredIntegrationEvent.cs            # para Proposals/Hiring
└── Abstractions/
    └── IRecruitmentReadContract.cs     # portas read-only para outros módulos
```

### (e) Composition root

```
Worfair.Modules.Recruitment/
├── RecruitmentModule.cs                # IServiceCollection.AddRecruitment(this…)
│                                        # registra MediatR, DbContext, repositórios,
│                                        # validators, processors de Outbox e eventos
├── Dependencies.cs                     # rota de referências (usada pelo teste de arquitetura)
└── Options/
    └── RecruitmentOptions.cs           # connection string, toggles de feature
```

## 4. Host (API)

```
src/Api/Worfair.Api/
├── Program.cs                          # bootstrap: AddAllModules() → pipeline HTTP
├── Modules/
│   └── ModuleRegistrar.cs              # chama AddIdentity(), AddTenants(), AddRecruitment()…
├── Middleware/
│   ├── TenantContextMiddleware.cs      # lê claim tenant_id → ITenantProvider
│   └── ExceptionHandlingMiddleware.cs
├── Endpoints/                          # Minimal APIs por módulo (thin controllers)
│   ├── Identity/
│   ├── Recruitment/
│   └── (…)
├── Authentication/
│   └── JwtConfiguration.cs
├── OpenApi/
└── Extensions/
    └── (migrations runner, seeding)
```

O host **não contém lógica de negócio** — apenas orquestra registros e HTTP.

## 5. Testes

```
tests/
├── Unit/
│   ├── Worfair.Modules.Recruitment.Domain.UnitTests/        # regras de negócio (xUnit)
│   ├── Worfair.Modules.Recruitment.Application.UnitTests/   # handlers + validators
│   ├── Worfair.Modules.Identity.Domain.UnitTests/
│   └── (um por módulo)
├── Integration/
│   ├── Worfair.Tests.Integration/                           # Testcontainers/Postgres
│   │   ├── Tenancy/                                         # isolamento por tenant
│   │   ├── Outbox/                                          # entrega de eventos
│   │   └── Modules/…
└── E2E/
    └── Worfair.Tests.E2E/                                   # WebApplicationFactory + dados seed
```

## 6. Resumo das responsabilidades por projeto

| Projeto | Responsabilidade | Depende de |
| ------- | ---------------- | ---------- |
| `.Domain` | modelo de negócio, invariantes, portas de persistência | BuildingBlocks.Domain |
| `.Application` | casos de uso, validação, orquestração | `.Domain`, BuildingBlocks.Application |
| `.Infrastructure` | EF Core, repositórios, outbox, integração externa | `.Domain`, `.Application`, BuildingBlocks.Infrastructure |
| `.Contracts` | contratos de integração (eventos + portas read-only) | BuildingBlocks.Contracts |
| `.` (composition root) | registro DI do módulo | todos acima |
| `Worfair.Api` | host HTTP, tenant context, module registrar | todos os composition roots |