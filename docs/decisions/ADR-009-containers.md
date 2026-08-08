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

| Serviço | Imagem | Porta | `depends_on` |
|---------|--------|-------|--------------|
| `cashflow-db` | `postgres:16-alpine` | 5432 | — |
| `consolidation-db` | `postgres:16-alpine` | 5433 | — |
| `rabbitmq` | `rabbitmq:4.3-management-alpine` | 5672 / 15672 | — |
| `cashflow-api` | build local | 5001 | `cashflow-db` |
| `consolidation-worker` | build local | — | `consolidation-db` |
| `consolidation-api` | build local | 5002 | `consolidation-db` |

### Dependência obrigatória × dependência opcional

Esta distinção é a decisão mais importante desta ADR, porque é onde RNF-001 pode
ser perdido silenciosamente na configuração do ambiente:

```
cashflow-api          ──REQUER──▶  cashflow-db
outbox publisher      ──RETRY──▶   rabbitmq
consolidation-worker  ──REQUER──▶  consolidation-db
consolidation-worker  ──RETRY──▶   rabbitmq
consolidation-api     ──REQUER──▶  consolidation-db
```

**Nenhum serviço declara `depends_on` no `rabbitmq`.** Colocar o broker como
pré-condição de startup da Cash Flow API contradiria tanto o Outbox
([ADR-004](./ADR-004-transactional-outbox.md)) quanto a definição de prontidão de
[ADR-011](./ADR-011-observability.md): com o RabbitMQ fora do ar, a API precisa
**subir**, responder `200` em `/health/ready` e aceitar `POST /transactions`
normalmente — o evento fica retido no outbox.

A conexão com o broker é, portanto, responsabilidade do publisher e do consumidor,
via reconexão com retry em segundo plano, e não do orquestrador de containers.
O mesmo vale para o worker: sem broker ele fica ocioso tentando reconectar, o que
é o comportamento correto, e não uma falha de boot.

Decisões de configuração:

- **Healthchecks** em bancos e broker. `depends_on: condition: service_healthy`
  é usado apenas nas dependências **obrigatórias** (os bancos). Sem isso, as
  aplicações sobem antes do banco e falham no primeiro boot — o que daria a falsa
  impressão de sistema quebrado. O healthcheck do `rabbitmq` existe para
  diagnóstico e para os testes de integração, não para bloquear startup.
- **Versões de imagem fixadas** por série (`postgres:16-alpine`,
  `rabbitmq:4.3-management-alpine`), nunca `latest` nem tag flutuante de série
  maior: o ambiente precisa ser o mesmo hoje e daqui a seis meses (RNF-012).
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
- **Sem broker:** `docker compose up -d --scale rabbitmq=0` (ou
  `docker compose stop rabbitmq`) e a `cashflow-api` ainda sobe, fica `ready` e
  responde `201` em `POST /transactions`. Este é o teste que prova que a
  dependência opcional foi configurada como opcional de fato.
