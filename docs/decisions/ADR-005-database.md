# ADR-005 — PostgreSQL com bancos independentes por contexto

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

Duas decisões distintas se sobrepõem aqui:

1. **Qual banco** usar.
2. **Quantos bancos** usar.

A segunda é a arquiteturalmente relevante. [ADR-002](./ADR-002-service-decomposition.md)
exige independência de falha entre lançamentos e consolidação; um banco
compartilhado destruiria essa independência, porque a indisponibilidade de uma
única instância derrubaria os dois contextos ao mesmo tempo.

Além disso, o domínio lida com **valores monetários**, o que impõe uma restrição
concreta: nada de ponto flutuante.

## Decisão

### Banco

**PostgreSQL** para ambos os contextos, com **Entity Framework Core** como ORM.

- Suporte a `numeric(18,2)` — decimal exato, requisito para dinheiro ([ADR-013](./ADR-013-money-representation.md)).
- `jsonb` nativo para o payload do outbox.
- `SELECT ... FOR UPDATE SKIP LOCKED`, necessário ao publisher do outbox ([ADR-004](./ADR-004-transactional-outbox.md)).
- `INSERT ... ON CONFLICT` para o upsert idempotente do saldo diário ([ADR-007](./ADR-007-idempotency.md)).
- Imagem Docker leve, sem licença, previsível em ambiente local (RNF-012).

### Topologia

**Um banco por contexto**, com instâncias separadas:

```
cashflow_db          consolidation_db
├── transactions     ├── daily_balances
└── outbox_messages  └── processed_events
```

Restrições que preservam o isolamento:

- Nenhum serviço acessa o banco do outro, nem para leitura.
- Nenhum `JOIN`, `FOREIGN KEY` ou view entre os dois esquemas.
- Credenciais separadas.
- Em Docker Compose, **containers separados** — não apenas schemas distintos na
  mesma instância. Compartilhar a instância manteria um ponto único de falha e
  anularia RNF-001.

## Alternativas consideradas

### Quantidade de bancos

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| Banco único compartilhado | Simples, consistência transacional, sem duplicação | Ponto único de falha; contenção de recursos; acoplamento por esquema — **anula RNF-001** | Rejeitada |
| Mesma instância, schemas separados | Isolamento lógico, menos containers | Falha da instância derruba os dois contextos | Rejeitada |
| Instâncias separadas | Isolamento real de falha, evolução independente do esquema | Dado duplicado, mais recursos, sem JOIN entre contextos | **Escolhida** |

### Tecnologia

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| PostgreSQL | Recursos necessários, gratuito, ótimo em container | — | **Escolhida** |
| SQL Server | Integração natural com .NET | Imagem pesada, licenciamento, ambiente local mais lento | Rejeitada |
| MongoDB | Flexível, escrita rápida | Sem tipo decimal natural para dinheiro; transações multi-documento mais frouxas; sem ganho aqui | Rejeitada |
| SQLite | Zero infra | Não representa o comportamento concorrente real; sem `SKIP LOCKED` | Rejeitada para runtime; considerado apenas para testes |
| Banco de leitura em Redis para o saldo | Leitura muito rápida | Durabilidade mais fraca para dado financeiro; a leitura já é O(1) | Rejeitada |

### Acesso a dados

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| EF Core | Migrations, produtividade, `DbContext` como Unit of Work — essencial para o outbox atômico | Abstração pode esconder custo de query | **Escolhida** |
| Dapper | Controle fino de SQL, mais rápido | Sem migrations, sem Unit of Work pronta | Rejeitada como padrão; pode ser usada pontualmente em query crítica |

## Consequências

**Positivas**

- Isolamento de falha real entre contextos (RNF-001, RNF-002).
- Cada contexto evolui seu esquema sem coordenação com o outro.
- `numeric` garante exatidão monetária.
- O `DbContext` como Unit of Work viabiliza a atomicidade lançamento + outbox.

**Negativas**

- O valor do lançamento existe duplicado nos dois lados.
- Impossível responder por SQL "quais lançamentos compõem este saldo" — exigiria
  consulta a dois sistemas.
- Mais consumo de recursos no ambiente local.
- Divergência entre lançamentos e saldo é possível e precisa ser detectável.

## Trade-off aceito

Abrimos mão de **consistência transacional entre os contextos** e de **JOINs
cruzados** em troca de **isolamento de falha**. É a mesma troca de
[ADR-002](./ADR-002-service-decomposition.md), aplicada à camada de dados: a
consistência passa a ser eventual e garantida por eventos, não por transação
([ADR-006](./ADR-006-consistency.md)).

Como mitigação da divergência, a fonte da verdade é sempre `transactions`; o saldo
consolidado é uma projeção e pode ser reconstruído a partir dela.

## Requisitos atendidos

RNF-001, RNF-002, RNF-006, RNF-012, RN-001

## Como validar

- Teste de arquitetura/configuração: cada serviço possui exatamente uma connection
  string e ela aponta para seu próprio banco.
- Teste de resiliência: parar `consolidation-db` não afeta `POST /transactions`.
- Teste de precisão: somar valores com centavos não produz erro de arredondamento.
