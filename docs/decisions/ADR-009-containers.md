# ADR-009 — Docker e Docker Compose para reprodutibilidade

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

O enunciado exige um README com "os passos detalhados para rodar a aplicação
localmente, seus pré-requisitos e seu modo de funcionamento" (RT-007) e cita
containers como requisito **opcional**.

A arquitetura escolhida tem seis componentes de runtime: duas APIs, dois bancos,
um worker e um broker. Pedir a quem avalia que instale PostgreSQL e RabbitMQ
manualmente, crie bancos, configure credenciais e suba cada projeto em um terminal
seria uma barreira desnecessária — e a primeira coisa que quebraria a avaliação.

## Decisão

Todo o ambiente sobe com **um único comando**:

```bash
docker compose up -d
```

### Serviços do Compose

| Serviço | Imagem | Porta | Depende de |
|---------|--------|-------|------------|
| `cashflow-db` | `postgres:16-alpine` | 5432 | — |
| `consolidation-db` | `postgres:16-alpine` | 5433 | — |
| `rabbitmq` | `rabbitmq:3-management-alpine` | 5672 / 15672 | — |
| `cashflow-api` | build local | 5001 | `cashflow-db`, `rabbitmq` |
| `consolidation-worker` | build local | — | `consolidation-db`, `rabbitmq` |
| `consolidation-api` | build local | 5002 | `consolidation-db` |

Decisões de configuração:

- **Healthchecks** em bancos e broker, com `depends_on: condition: service_healthy`.
  Sem isso, as aplicações sobem antes das dependências e falham no primeiro boot —
  o que daria a falsa impressão de sistema quebrado.
- **Dockerfiles multi-stage** (`sdk` para build, `aspnet`/`runtime` para execução),
  com usuário não-root e imagem final enxuta.
- **Migrations aplicadas no startup** de cada serviço. Em produção isso seria
  inadequado (deveria ser passo de deploy); aqui a prioridade é o comando único.
  Limitação registrada conscientemente.
- **Variáveis de ambiente** no Compose, com `.env.example` versionado e `.env` fora
  do controle de versão.
- **Volumes nomeados** para os bancos, permitindo `docker compose down -v` como
  reset limpo.
- Interface de gestão do RabbitMQ exposta em `15672` — útil para inspecionar filas
  e a DLQ durante a avaliação.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| Docker Compose | Um comando, isolado, reprodutível, atende ao opcional do enunciado | Requer Docker instalado; não é ambiente produtivo real | **Escolhida** |
| Instalação manual das dependências | Sem pré-requisito de Docker | Frágil, demorado, varia por máquina — inimigo direto de RNF-012 | Rejeitada |
| .NET Aspire | Orquestração nativa, ótimo dashboard | Exige SDK e tooling específicos; menos universal para quem avalia | Rejeitada |
| Kubernetes / Kind | Próximo de produção | Complexidade injustificável para execução local | Rejeitada |
| Devcontainer | Ambiente de desenvolvimento uniforme | Resolve o desenvolvimento, não a execução do sistema | Rejeitada (complementar, não substituto) |

## Consequências

**Positivas**

- Reprodutibilidade real: mesmo comportamento em qualquer máquina com Docker.
- Elimina o custo de setup para quem avalia o desafio.
- Permite os testes de resiliência do sistema de forma trivial
  (`docker compose stop <serviço>`), o que é a demonstração prática de RNF-001.
- Habilita Testcontainers nos testes de integração com a mesma stack.

**Negativas**

- Docker passa a ser pré-requisito obrigatório.
- Consumo de recursos: seis containers, dois deles PostgreSQL.
- Migration no startup é aceitável aqui, mas seria uma má prática em produção.
- O Compose não representa a topologia real de um ambiente produtivo.

## Trade-off aceito

Aceitamos **um pré-requisito (Docker) e maior consumo de memória** em troca de
**setup determinístico em um comando**. Para um projeto avaliado por terceiros,
qualquer atrito de instalação custa mais do que o pré-requisito.

Aceitamos conscientemente a migration automática no boot, priorizando a
experiência de execução única sobre o rigor de deploy — e a registramos como
limitação conhecida em vez de omiti-la.

## Requisitos atendidos

RNF-012, RT-007, requisito opcional do enunciado (containers)

## Como validar

- Em máquina limpa com Docker: `git clone` + `docker compose up -d` sobe tudo.
- Todos os healthchecks ficam `healthy`.
- `POST /transactions` seguido de `GET /daily-balances/{date}` funciona ponta a ponta.
- `docker compose down -v && docker compose up -d` retorna ao estado inicial.
