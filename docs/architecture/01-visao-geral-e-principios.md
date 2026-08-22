# 01 — Visão Geral e Princípios

## 1. Contexto

A plataforma Worfair atende três movimentos complementares de negócio:

1. **Recrutamento** — processo seletivo completo (ATS): requisições de vaga, pipeline
   de candidatos, entrevistas, avaliações e time de contratação.
2. **Contratação** — formalização da decisão: propostas de serviço, ofertas, aceite e contrato.
3. **Prestação de serviços** — marketplace de projetos/vagas: publicação, candidatura,
   entrega, faturamento e pagamento.

É uma plataforma **multi-tenant**: cada organização contratante (cliente) é um
tenant com dados isolados. Papéis globais (ex.: administração da plataforma) e
papéis por tenant (ex.: Recruiter, Hiring Manager) coexistem.

## 2. Por que Monolito Modular

| Critério | Microserviços | Monolito modular |
| -------- | ------------- | ---------------- |
| Complexidade operacional | Alta (rede, observabilidade, deploy distribuído) | Baixa (um processo) |
| Velocidade do time no início | Baixa (overhead) | Alta |
| Limites de contexto | Forçados por rede | Forçados por regras de dependência e compilação |
| Transações ACID entre módulos | Difíceis | Possíveis dentro do processo |
| Evolução para microserviços | N/A | Caminho claro (módulo → serviço) |

**Decisão:** Monolito Modular (D-01). Todos os módulos são implantados juntos, mas
cada módulo é um bounded context com `Domain → Application → Infrastructure`
próprias. A fronteira do módulo é respeitada por:

- **Regras de dependência** verificáveis em build (ex.: `NetArchTest`/`ArchUnitNET`).
- **Banco de dados**: cada módulo tem seu próprio `DbContext`, schema SQL próprio e
  **não compartilha tabelas** nem cria Foreign Keys entre módulos (referências por ID).
  Única exceção whitelisted: `tenant_id → tenancy.tenants(id)` e FKs internas ao
  núcleo Tenancy/Identity (ver [docs/database/02 — Regras críticas](../../docs/database/02-regras-criticas.md)).

## 3. Princípios de Clean Architecture por módulo

Dentro de cada módulo vale a **Regra de Dependência** (dependências apontam para dentro):

```
Infrastructure ──────────────► Application ──────────────► Domain
      │                              │                          │
      │  implementa interfaces       │  depende de abstrações    │ (zero dependências externas)
      └──────────────────────────────┘  do Domain                ▼
                                                          Domain = coração
```

- **Domain** — modelo de negócio puro. Sem referência a EF Core, MediatR, HTTP,
  bancos de dados. Contém Entidades, Aggregates, Value Objects, Domain Events,
  Domain Services e **interfaces de repositórios** (portas).
- **Application** — casos de uso (Commands/Queries/Handlers), validação de entrada,
  DTOs e orquestração. Depende apenas do Domain e de Building Blocks.
- **Infrastructure** — implementa as portas do Domain/Application (EF Core,
  repositórios, e-mail, bus, cache). Depende de Domain e Application.
- **Composition Root do módulo** — projeto `.Modules.X/` que registra tudo no DI.

## 4. Regras de dependência entre módulos

1. **Módulos não se referenciam diretamente.** `Worfair.Modules.Recruitment` nunca
   referencia `Worfair.Modules.Financial`.
2. Comunicação por **Integration Events** (assíncronos, via Outbox) para
   **escrita/efeitos colaterais**.
3. **Leitura síncrona** é permitida apenas através de **contratos read-only**
   expostos no projeto `*.Contracts` de cada módulo (portas implementadas na
   composição do host). Ex.: Recruitment lê nome/status de uma JobPosting sem
   acoplar-se ao módulo Jobs.
4. Referências entre agregados de módulos distintos sempre por **ID** (`Guid`/typed id),
   nunca por objeto do outro módulo.
5. Regras verificáveis por teste de arquitetura no CI (ex.: referência de
   `*.Infrastructure` a outro módulo é proibida).

```
        ┌─────────────── Todos os módulos dependem apenas de Building Blocks ───────────────┐
        ▼                                                                                    │
  ┌───────────┐  Integration Events  ┌───────────┐  Integration Events  ┌───────────┐        │
  │  Identity │ ───────────────────► │   Jobs    │ ───────────────────► │Notificat. │        │
  └───────────┘                      └───────────┘                      └───────────┘        │
        ▲                                    ▲                                                │
        │ Integration Events                 │ Integration Events                             │
  ┌───────────┐                      ┌───────────────┐                                        │
  │  Tenants  │ ───────────────────► │  Recruitment  │ ──────────────────►                    │
  └───────────┘                      └───────────────┘        Financial / Proposals           │
                                                                                              │
  ┌──────────────────────────────────────────────────────────────────────────────────────────┘
  │ Building Blocks (Domain/Application/Infrastructure/Contracts) + Worfair.Api (host)
```

## 5. Inconsistências sinalizadas

A especificação do cliente lista módulos com sobreposição potencial. Fronteiras
adotadas (ver [03 — Módulos de negócio](03-modulos-de-negocio.md#fronteiras-entre-módulos)):

1. **Recruitment × Jobs**: `Jobs` publica a **vitrine** (job posting / projeto de
   serviço no marketplace). `Recruitment` executa o **processo seletivo** (requisição
   interna, pipeline, entrevistas). Uma job posting publicada pode gerar uma
   `JobRequisition` no Recruitment — nunca o contrário.
2. **Recruitment × Proposals/Hiring**: o **hiring** em Proposals é a **decisão e
   oferta** (offer letter, proposta de serviço, contrato). O Recruitment **termina**
   quando emite `CandidateHired`. Proposals **começa** aí.
3. **D-11**: o namespace `Worfair` foi assumido a partir do diretório do repositório;
   se o nome comercial for outro, alterar somente no scaffolding (documentado no ADR).