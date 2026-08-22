# Worfair — Arquitetura de Software

Documento de referência oficial da arquitetura do **Monolito Modular** em C#/.NET
para a plataforma multi-tenant de **contratação, recrutamento e prestação de serviços**.

> **Instrução de Continuidade**
>
> Este conjunto de documentos é parte oficial do projeto. Decisões aqui registradas
> **não devem ser contraditas** em prompts futuros. Ao identificar qualquer
> inconsistência, sinalize-a explicitamente antes de prosseguir e registre a
> correção como um ADR (decida a mudança, nunca a ignore).

## Índice

| Doc | Tema |
| --- | ---- |
| [01 — Visão geral e princípios](01-visao-geral-e-principios.md) | Contexto, por que Monolito Modular, princípios Clean Architecture + DDD, regras de dependência |
| [02 — Estrutura de pastas](02-estrutura-de-pastas.md) | Árvore completa do repositório, responsabilidade de cada projeto/camada |
| [03 — Módulos de negócio](03-modulos-de-negocio.md) | Bounded contexts, fronteiras, agregados e integrações entre módulos |
| [04 — Isolamento de dados por tenant](04-isolamento-de-dados-por-tenant.md) | Estratégia, implementação em EF Core e Row-Level Security |
| [05 — Exemplo prático de domínio](05-exemplo-de-dominio.md) | Entidade + Value Objects do módulo Recruitment (código) |
| [Banco de Dados (PostgreSQL)](../../docs/database/README.md) | Estratégia multi-tenancy, regras críticas, modelagem relacional e scripts SQL (v1.1: RLS obrigatório, roles codificadas, `SUPER_ADMIN` global, multi-roles, `companies`) |
| [Segurança e Autorização](../../docs/security/README.md) | JWT como contexto (não fonte de verdade), anti-manipulação de tenant, policies/modos, handlers e proteção financeiro/auditoria (v1.2: catálogo oficial de roles, modos operacionais) |
| [Módulo Financeiro (Asaas + RabbitMQ)](../../docs/financial/README.md) | Payment Split, retenção operacional, máquina de estados financeira, webhooks assíncronos, idempotência e Outbox (v1.3) |
| [Frontend (React + Vite + Bootstrap)](../../docs/frontend/README.md) | Estrutura de projeto, API backend-centric, rotas por modo, UX/UI e alternância de modos (v1.5: interceptor Axios corrigido — FE-05) |
| [DevOps (Infra + CI/CD)](../../docs/devops/README.md) | Docker Compose, estratégia de microsserviços, migrations controladas, GitHub Actions, observabilidade e validação do ciclo completo (v1.5) |

## Nota — Versão definitiva congelada

Os 6 documentos de planejamento (architecture, database, security, financial,
frontend, devops) foram declarados pelo cliente como **versão definitiva
congelada** (v1.5). Mudanças a partir daqui exigem ADR aprovado.

## Decisões arquiteturais base (versão 1.0)

Estas são as decisões vigentes. Mudanças exigem ADR aprovado.

| # | Decisão | Valor |
| - | ------- | ----- |
| D-01 | Estilo de arquitetura | **Monolito Modular** (um processo/implantação, múltiplos módulos isolados) |
| D-02 | Plataforma | C# / .NET 10 (LTS), ASP.NET Core, EF Core 10 |
| D-03 | Banco de dados | PostgreSQL 16+ |
| D-04 | Camadas | Clean Architecture por módulo: `Domain → Application → Infrastructure`, composição no módulo |
| D-05 | Modelagem | Domain-Driven Design tático (Aggregates, Value Objects, Domain Events, Repositories) |
| D-06 | CQRS | MediatR (Commands/Queries + handlers) intra-módulo |
| D-07 | Comunicação entre módulos | **Integration Events** assíncronos + Transactional Outbox; leitura síncrona via contratos (portas) read-only |
| D-08 | Isolamento por tenant | **Shared DB + Shared Schema** por padrão (coluna `TenantId`), com tiers opcionais `Schema-per-tenant` e `Database-per-tenant` |
| D-09 | Autenticação | Identity/Auth como módulo; JWT + RBAC; `tenant_id` presente no token |
| D-10 | Resultado de operações | Padrão `Result` + `Error` em Building Blocks (sem exceções para fluxo de negócio) |
| D-11 | Namespace raiz | `Worfair` (nome de produto assumido a partir do diretório — revisar se o nome comercial divergir) |

## Fluxo de trabalho sugerido

1. Revisar este índice e os documentos 01 a 05.
2. Aprovar/adicionar ADRs quando necessário (`docs/adr/`).
3. Próximo passo proposto: scaffolding do repositório com `dotnet new` + `Directory.Build.props` + estrutura de módulos (ver [02 — Estrutura de pastas](02-estrutura-de-pastas.md)).