# CashFlow — Instruções do Projeto

Estas instruções valem para qualquer agente de IA que trabalhe neste repositório
(Claude Code, Codex ou qualquer outro).

## Autoria e commits

- **Nunca** adicionar `Co-Authored-By` em commits.
- **Nunca** adicionar rodapés, assinaturas, badges ou menções a ferramentas de IA
  em mensagens de commit, descrições de Pull Request, issues ou comentários.
- **Nunca** adicionar links de sessão, `Generated with ...` ou similares.
- Toda a autoria é exclusivamente de `rafaelomodei <rafael.omodei@outlook.com>`.
- Commits seguem [Conventional Commits](https://www.conventionalcommits.org/):
  `feat|fix|docs|test|refactor|chore|perf|build|ci(escopo): descrição`.
- Mensagens de commit em português, no imperativo, sem emoji.

## Fluxo de trabalho

- Trabalho sempre em branch dedicada (`docs/...`, `feat/...`, `fix/...`),
  nunca commitando direto na `master`.
- Merge para `master` apenas via Pull Request.
- Não iniciar código de produção antes da documentação da etapa correspondente
  estar aprovada.

## Regras de engenharia

- **TDD é obrigatório**: teste primeiro (RED), implementação mínima (GREEN),
  refatoração (REFACTOR). Testes não são etapa posterior.
- **Clean Architecture**: dependências apontam sempre para dentro
  (`Domain` ← `Application` ← `Infrastructure`/`Api`).
  `Domain` não referencia nenhum pacote de infraestrutura.
- **SOLID** e **Clean Code** como regra arquitetural, não como sugestão.
- Toda decisão arquitetural relevante vira uma ADR em `docs/decisions/`.
- Nenhuma funcionalidade fora do escopo definido em `docs/scope.md`.

## Estrutura da documentação

```
docs/
├── README.md              índice da documentação
├── requirements.md        RF, RNF, restrições técnicas e rastreabilidade
├── architecture.md        visão arquitetural e diagramas
├── scope.md               escopo do MVP e itens fora de escopo
├── testing-strategy.md    estratégia de testes e TDD
├── roadmap.md             etapas de execução do projeto
├── challenge/             enunciado original do desafio
└── decisions/             ADRs
```

## Idioma

Documentação e commits em **português (pt-BR)**.
Código, nomes de classes, métodos e testes em **inglês**.
