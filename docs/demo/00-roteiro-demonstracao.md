# 00 — Roteiro de demonstração

Passo a passo para apresentar o Worfair do zero (sem dados prévios).
Tempo estimado: 15–20 min. Pré-requisito: seguir o `README.md`
(passos 1–6: chaves JWT, banco, migrations, API em `http://localhost:5000`,
web em `http://localhost:5173`).

> Use e-mails e senhas de teste (ex.: `admin@teste.local` / senha 12+).
> Nunca use credenciais reais.

## 1. Admin da plataforma (3 min)

1. Suba a API uma vez com `DB_AUTO_MIGRATE=true` e `AdminSeed__Email` /
   `AdminSeed__Password` definidos (via `$env:` no PowerShell com
   `dotnet run`, ou `.env` com `docker compose`).
2. Login com o e-mail do seed → modo global, papel `SUPER_ADMIN`.
3. Mostre `/painel` (receita), `/plataforma` (espaços + disputas) e
   `/financeiro` (faturas da plataforma).

## 2. Usuário comum: espaço e publicação (5 min)

1. `/cadastro`: crie a conta (pessoa física, CPF fictício válido em dígitos).
2. Login → `/onboarding`: crie o espaço pessoal (ex.: "Estúdio Demo").
   A conta entra no espaço como Dono + Prestador.
3. `/empresa`: cadastre uma empresa (CNPJ/CPF fictícios).
4. `/contratar`, aba "Trabalho freelancer": publique um trabalho com orçamento.
5. `/contratar`, aba "Vaga de emprego": publique uma vaga ligada à empresa.

## 3. Segundo usuário: proposta e fatura (5 min)

1. Cadastre outra conta, crie outro espaço, entre nele.
2. Em `/trabalhos`, abra o trabalho publicado e envie uma proposta.
3. Com o primeiro usuário, abra `/propostas` e aceite.
4. Crie a cobrança e liquide a fatura (fluxo de contratante).
5. Em `/mensagens`, troque mensagens na fatura; use "reportar" para abrir
   disputa e mostre a mediação no `/plataforma` com o admin.

## 4. Equipe e propriedade (3 min)

1. Em `/equipe`, convide o segundo usuário pelo e-mail e atribua cargos
   (nomes em português: Dono, Contratante, Recrutador, Gestor, Prestador).
2. Mostre os cargos atuais exibidos em cada membro.
3. Transfira a propriedade do espaço ("Transferir propriedade") e mostre que
   o antigo dono perde o acesso de gestão.

## Fora do escopo da demo

- Cobrança Asaas: só com `ASAAS_API_KEY` do sandbox (opcional).
- Documentos fiscais (NFS-e): não implementado — só existem faturas internas.
- Modo offline: não há fila de sincronização; sem internet as ações falham.
