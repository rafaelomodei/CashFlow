# Contribuindo

Convenções do repositório. O contexto de cada regra está em [`docs/`](./docs/README.md).

## Convenções

- Commits seguem [Conventional Commits](https://www.conventionalcommits.org/) —
  `feat|fix|docs|test|refactor|chore|perf|build|ci(escopo): descrição`.
  Em português, no imperativo, sem emoji.
  Exemplo: `feat(transactions): adicionar caso de uso de registro de lançamento`.
- Branches: `docs/...`, `feat/...`, `fix/...`, `chore/...`, `test/...`,
  `refactor/...`. Nunca commitar direto na `master`.
- Merge para `master` apenas via Pull Request, usando
  [`.github/pull_request_template.md`](./.github/pull_request_template.md), com a
  CI verde.
- Documentação e commits em pt-BR; código, nomes de classes, métodos e testes em
  inglês.

## Regras de engenharia

- **TDD**: teste falhando primeiro (RED), implementação mínima (GREEN),
  refatoração (REFACTOR) — [ADR-008](./docs/decisions/ADR-008-tdd.md).
- **Clean Architecture**: dependências apontam sempre para dentro
  (`Domain` ← `Application` ← `Infrastructure`/`Api`). As fronteiras são
  verificadas por testes de arquitetura, não por convenção.
- Decisões arquiteturais **estruturais** viram ADR em
  [`docs/decisions/`](./docs/decisions/README.md); decisão local e reversível não
  vira ADR — o critério está no README daquele diretório.
- Escopo restrito ao definido em [`docs/scope.md`](./docs/scope.md). Nenhuma
  abstração, dependência ou endpoint entra sem resolver um problema que **já
  existe**.

## Fronteiras que não podem ser violadas

- Cash Flow e Consolidation não se comunicam por HTTP — apenas por evento
  assíncrono ([ADR-002](./docs/decisions/ADR-002-service-decomposition.md)).
- Cash Flow não depende da disponibilidade do RabbitMQ para registrar um
  lançamento ([ADR-004](./docs/decisions/ADR-004-transactional-outbox.md)).
- Os dois contextos não compartilham banco de dados
  ([ADR-005](./docs/decisions/ADR-005-database.md)).
- `Shared.Contracts` contém apenas contratos de evento — nunca regra de negócio.

## Fluxo de uma tarefa

```
docs/progress.md          → qual é o próximo item?
docs/requirements.md      → qual RF/RNF/RN ele atende?
docs/decisions/           → qual ADR o governa?
docs/scope.md             → está dentro do escopo?
      ↓
RED → GREEN → REFACTOR
      ↓
dotnet test  (unitários → arquitetura → integração)
      ↓
atualizar docs/progress.md no mesmo Pull Request
```
