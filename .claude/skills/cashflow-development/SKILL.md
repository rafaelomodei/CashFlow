---
name: cashflow-development
description: Procedimento de execução de tarefas no projeto CashFlow — descobrir o próximo item em docs/progress.md, amarrá-lo a requisitos e ADRs, aplicar TDD, validar fronteiras arquiteturais e preparar o Pull Request. Use ao implementar qualquer item do roadmap, ao receber "implemente o próximo item", ou ao concluir uma etapa.
---

# Workflow de desenvolvimento — CashFlow

Este documento é **procedimento**, não regra. As regras estão em `AGENTS.md` e na
documentação de `docs/`. Aqui está apenas a ordem das ações.

```
Receber tarefa
      ↓
docs/progress.md          → qual é o item?
      ↓
docs/requirements.md      → qual RF/RNF/RN ele atende?
docs/decisions/           → qual ADR o governa?
docs/scope.md             → está dentro do escopo?
      ↓
RED → GREEN → REFACTOR
      ↓
testes direcionados → suíte completa → testes de arquitetura
      ↓
atualizar docs/progress.md
      ↓
Pull Request
```

## 1. Antes de implementar

1. Ler `docs/progress.md` e identificar a **etapa atual** e o **primeiro item não
   marcado** dela. Não pular itens da etapa para começar a próxima.
2. Identificar os RF / RNF / RN associados em `docs/requirements.md`.
3. Ler a ADR que governa a decisão envolvida (`docs/decisions/`).
4. Confirmar em `docs/scope.md` que a funcionalidade está dentro do MVP. Se não
   estiver, **parar e perguntar** em vez de implementar.
5. Criar a branch dedicada (`feat/...`, `test/...`, `docs/...`, `chore/...`).
   Nunca trabalhar na `master`.
6. Se o item exigir uma decisão estrutural nova, escrever a ADR **antes** do
   código.

Se o próximo item for ambíguo, dizer qual é a leitura adotada antes de começar —
não inventar requisito para resolver ambiguidade.

## 2. Durante a implementação

7. Aplicar TDD de verdade: escrever o teste, **vê-lo falhar**, implementar o
   mínimo, refatorar. Não escrever implementação e teste no mesmo passo.
8. Implementar apenas o que o requisito pede. Sem abstração especulativa, sem
   generalização "para o futuro", sem interface com um único implementador que o
   requisito não exija.
9. Respeitar as fronteiras:
   - `Domain` não conhece `Infrastructure` nem EF Core, RabbitMQ ou ASP.NET.
   - `Application` depende de `Domain` e de abstrações — nunca de implementação.
   - Cash Flow e Consolidation não se comunicam por HTTP.
   - Cash Flow não depende da disponibilidade do RabbitMQ para registrar
     lançamento.
   - `Shared.Contracts` só contém contrato de evento.
10. Código, nomes e testes em inglês. Comentários apenas quando explicam
    *por que*, nunca *o que*.
11. Não criar ADR para decisão local e reversível — critério em
    `docs/decisions/README.md`.

## 3. Antes de concluir

12. Rodar os testes direcionados ao que mudou.
13. Rodar a suíte completa: `dotnet test`.
14. Rodar os testes de arquitetura.
15. Se o item envolver infraestrutura, validar com `docker compose up -d`.
16. Atualizar `docs/progress.md` marcando o que foi realmente entregue — e
    somente isso.
17. Atualizar a documentação **apenas** quando comportamento, contrato ou
    arquitetura mudarem. Documentação atualizada por hábito vira ruído.
18. Se a implementação contrariou uma ADR, abrir ADR nova registrando a mudança.
    Nunca editar a ADR antiga silenciosamente.

## 4. Pull Request

19. Commits em Conventional Commits, pt-BR, imperativo, sem emoji, sem rodapé,
    sem coautoria.
20. Preencher `.github/pull_request_template.md`, indicando explicitamente os
    requisitos atendidos e as ADRs envolvidas.
21. Merge só com CI verde.

## Perguntas que interrompem o fluxo

Parar e perguntar ao usuário quando:

- o item não estiver em `docs/scope.md`;
- a implementação exigir contrariar uma ADR existente;
- o contrato de API ou de evento precisar mudar depois de definido;
- houver mais de uma leitura plausível do requisito e a escolha mudar o design.

Nos demais casos, decidir, registrar a premissa e seguir.
