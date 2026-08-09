# AGENTS.md

Fonte única das regras para agentes de IA (Codex, Claude Code e equivalentes)
neste repositório. Arquivos específicos de ferramenta — como `CLAUDE.md` —
apontam para aqui em vez de repetir estas regras.

```
AGENTS.md                    regras — o que vale sempre
   ↑
CLAUDE.md                    o que é específico do Claude Code
   ↑
.claude/skills/…             workflow — como executar uma tarefa
   ↑
docs/                        contexto — requisitos, arquitetura, decisões
```

## Regras inegociáveis de autoria

- Proibido incluir `Co-Authored-By:` em qualquer commit.
- Proibido incluir rodapés, assinaturas, créditos, badges, links de sessão,
  `Generated with ...` ou qualquer menção a ferramentas de IA em commits, Pull
  Requests, issues ou comentários de código.
- Autoria única e exclusiva: `rafaelomodei <rafael.omodei@outlook.com>`.

## Convenções

- Commits: [Conventional Commits](https://www.conventionalcommits.org/) —
  `feat|fix|docs|test|refactor|chore|perf|build|ci(escopo): descrição`.
  Em português, no imperativo, sem emoji.
  Exemplo: `feat(transactions): adicionar caso de uso de registro de lançamento`.
- Branches: `docs/...`, `feat/...`, `fix/...`, `chore/...`, `test/...`,
  `refactor/...`. Nunca commitar direto na `master`.
- Merge para `master` apenas via Pull Request, usando
  [`.github/pull_request_template.md`](./.github/pull_request_template.md).
- Documentação e commits em pt-BR; código, nomes de classes, métodos e testes em
  inglês.

## Regras de engenharia

- **TDD obrigatório**: teste falhando primeiro (RED), implementação mínima
  (GREEN), refatoração (REFACTOR). Testes não são etapa posterior.
- **Clean Architecture**: dependências apontam sempre para dentro
  (`Domain` ← `Application` ← `Infrastructure`/`Api`). `Domain` não referencia
  nenhum pacote de infraestrutura.
- **SOLID** e **Clean Code** como regra arquitetural, não como sugestão.
- Decisões arquiteturais **estruturais** viram ADR em `docs/decisions/`.
  Decisão local e reversível não vira ADR — critério em
  [`docs/decisions/README.md`](./docs/decisions/README.md).
- Escopo restrito ao definido em [`docs/scope.md`](./docs/scope.md) — sem scope
  creep, sem abstração especulativa.
- **Regra de contenção:** nenhuma abstração, ADR, pattern, endpoint, componente,
  configuração ou dependência entra sem resolver um problema que **já existe**.
  Problema previsto para o futuro não conta; problema que o desafio não levanta
  não conta. Na dúvida entre a peça a mais e a peça a menos, fica a menos.
- Não iniciar código de produção de uma etapa antes de a documentação
  correspondente estar aprovada.

## Fronteiras que não podem ser violadas

- Cash Flow e Consolidation não se comunicam por HTTP — apenas por evento
  assíncrono ([ADR-002](./docs/decisions/ADR-002-service-decomposition.md)).
- Cash Flow não pode depender da disponibilidade do RabbitMQ para registrar um
  lançamento ([ADR-004](./docs/decisions/ADR-004-transactional-outbox.md)).
- Os dois contextos não compartilham banco de dados
  ([ADR-005](./docs/decisions/ADR-005-database.md)).
- `Shared.Contracts` contém apenas contratos de evento — nunca regra de negócio.

## Onde está cada coisa

| Pergunta | Documento |
|----------|-----------|
| O que o sistema precisa fazer | [`docs/requirements.md`](./docs/requirements.md) |
| O que está fora do escopo | [`docs/scope.md`](./docs/scope.md) |
| Como o sistema é estruturado | [`docs/architecture.md`](./docs/architecture.md) |
| Qual é o contrato de API e de evento | [`docs/api-contracts.md`](./docs/api-contracts.md) |
| Por que cada escolha foi feita | [`docs/decisions/`](./docs/decisions/README.md) |
| Como a corretude é garantida | [`docs/testing-strategy.md`](./docs/testing-strategy.md) |
| Em que ordem construir | [`docs/roadmap.md`](./docs/roadmap.md) |
| **Qual é o próximo item** | [`docs/progress.md`](./docs/progress.md) |

## Fluxo de trabalho de uma tarefa

O procedimento operacional está em
[`.claude/skills/cashflow-development/SKILL.md`](./.claude/skills/cashflow-development/SKILL.md).
Agentes sem suporte a skills devem seguir o mesmo arquivo como checklist.

```
ler progress.md → identificar RF/RNF/ADR → conferir scope.md
        → RED → GREEN → REFACTOR → testes → fronteiras
        → atualizar progress.md → PR
```
