# CashFlow — Instruções para o Claude Code

As regras deste repositório valem para qualquer agente e estão em um único lugar:

@AGENTS.md

Este arquivo contém **apenas** o que é específico do Claude Code. Se uma regra
vale para qualquer agente, ela pertence a `AGENTS.md` — não duplique aqui.

## Workflow

Para qualquer tarefa de implementação, use a skill `cashflow-development`
(`.claude/skills/cashflow-development/SKILL.md`). Ela define o procedimento:
descobrir o item atual em `docs/progress.md`, amarrá-lo aos requisitos e ADRs,
aplicar TDD, validar as fronteiras arquiteturais e preparar o Pull Request.

Quando o pedido for "implemente o próximo item", a skill é o ponto de partida.

## Commits

`.claude/settings.json` já define `includeCoAuthoredBy: false`. Isso é uma trava
de ferramenta, não a regra — a regra de autoria está em `AGENTS.md` e vale mesmo
que a configuração mude.
