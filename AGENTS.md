# AGENTS.md

Instruções para agentes de IA (Codex, Claude Code e equivalentes) neste repositório.

## Regras inegociáveis de autoria

- Proibido incluir `Co-Authored-By:` em qualquer commit.
- Proibido incluir rodapés, assinaturas, créditos, badges ou qualquer menção a
  ferramentas de IA em commits, Pull Requests, issues ou comentários de código.
- Autoria única e exclusiva: `rafaelomodei <rafael.omodei@outlook.com>`.

## Convenções

- Commits: Conventional Commits, em português, no imperativo, sem emoji.
  Exemplo: `feat(transactions): adicionar caso de uso de registro de lançamento`.
- Branches: `docs/...`, `feat/...`, `fix/...`, `chore/...`. Nunca commitar na `master`.
- Documentação em pt-BR; código e testes em inglês.

## Regras de engenharia

- TDD obrigatório: teste falhando antes da implementação.
- Clean Architecture: `Domain` ← `Application` ← `Infrastructure`/`Api`.
  A camada de domínio não conhece infraestrutura.
- SOLID, Clean Code e Design Patterns aplicados de forma justificada.
- Decisões arquiteturais documentadas como ADR em `docs/decisions/`.
- Escopo restrito ao definido em `docs/scope.md` — sem scope creep.

O detalhamento completo está em `CLAUDE.md` e em `docs/`.
