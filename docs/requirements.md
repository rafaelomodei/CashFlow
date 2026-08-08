# Mapeamento de Requisitos

> Etapa 2 do projeto — tradução do enunciado do desafio em requisitos rastreáveis.
> Documento de origem: [`challenge/desafio-desenvolvedor-software.pdf`](./challenge/desafio-desenvolvedor-software.pdf)

## 1. Contexto

O enunciado descreve a necessidade de um lojista que precisa gerenciar o fluxo de
caixa do dia a dia, registrando créditos e débitos, e que demanda um relatório com
o saldo consolidado diário.

Do enunciado extraímos dois requisitos de negócio explícitos:

1. Uma aplicação focada na **gestão dos lançamentos financeiros**.
2. Uma aplicação responsável por fornecer o **saldo diário consolidado**.

E dois requisitos não funcionais explícitos:

1. A aplicação de lançamentos precisa **continuar operante mesmo com falha** no
   sistema de consolidação diária.
2. Em pico, a consolidação processa **50 chamadas por segundo**, tolerando
   **perda máxima de 5%**.

Todo o restante deste documento é derivação ou inferência a partir desses pontos,
e cada item indica sua origem.

---

## 2. Requisitos Funcionais

| ID | Requisito | Prioridade | Origem |
|----|-----------|------------|--------|
| RF-001 | Registrar um lançamento financeiro | Obrigatório | Enunciado |
| RF-002 | Classificar o lançamento como crédito ou débito | Obrigatório | Enunciado |
| RF-003 | Consultar os lançamentos financeiros registrados | MVP | Inferido de “gestão dos lançamentos” |
| RF-004 | Calcular o saldo consolidado diário | Obrigatório | Enunciado |
| RF-005 | Consultar o saldo consolidado de um determinado dia | Obrigatório | Enunciado |
| RF-006 | Consultar a consolidação diária sem depender do serviço de lançamentos | MVP / Arquitetural | Derivado do requisito de independência |

> **Nota:** RF-006 é um requisito de fronteira — ele descreve um comportamento
> observável pelo usuário (a consulta de saldo continua respondendo), mas sua
> causa é arquitetural e se sobrepõe a RNF-001 e RNF-002. Mantivemos como RF
> porque é verificável por teste funcional (derrubar o serviço de lançamentos e
> consultar o saldo).

### RF-001 — Registrar lançamento

A aplicação deve permitir registrar uma movimentação financeira.

Dados mínimos que fazem sentido para o domínio:

```
Transaction
├── Id           identificador único do lançamento
├── Type         CREDIT | DEBIT
├── Amount       valor monetário positivo
├── OccurredAt   data/hora da ocorrência do lançamento
└── Description  descrição textual (atributo de apoio)
```

`Description` não é exigido explicitamente pelo enunciado; é tratado como
atributo de apoio, opcional do ponto de vista do domínio.

**Regras de negócio associadas:**

| ID | Regra |
|----|-------|
| RN-001 | `Amount` deve ser estritamente maior que zero |
| RN-002 | `Type` deve ser obrigatoriamente `CREDIT` ou `DEBIT` |
| RN-003 | O sinal do lançamento é derivado do `Type`, nunca do `Amount` |
| RN-004 | `OccurredAt` define a qual dia o lançamento pertence para fins de consolidação |

### RF-002 — Crédito ou débito

Todo lançamento precisa possuir um tipo válido:

```
CREDIT
DEBIT
```

Não criamos duas entidades distintas: um único agregado `Transaction` com um
discriminador de tipo é suficiente e evita duplicação de regra.

### RF-003 — Consultar lançamentos

O enunciado não diz literalmente “listar lançamentos”, mas pede uma aplicação de
*gestão* dos lançamentos financeiros. Para que essa gestão seja minimamente
utilizável, a consulta faz parte do MVP.

Contrato pretendido:

```
GET /transactions          listagem paginada
GET /transactions/{id}     lançamento individual — destino do header Location do POST
```

Filtros sofisticados (por categoria, por faixa de valor, busca textual) ficam
fora do escopo. Paginação e filtro por período são o teto. A paginação é por
**cursor**, não por offset — ver [ADR-014](./decisions/ADR-014-cursor-pagination.md).

Contrato completo: [`api-contracts.md`](./api-contracts.md) §2.

### RF-004 — Consolidar saldo diário

Para cada dia:

```
Saldo diário = Σ Créditos − Σ Débitos
```

Exemplo:

```
08/08/2026

Créditos: R$ 1.500,00
Débitos:  R$   700,00
------------------------
Saldo:    R$   800,00
```

O saldo diário é o resultado do dia isolado. Saldo acumulado (running balance)
não é exigido pelo enunciado e fica fora do MVP.

### RF-005 — Consultar consolidação

A segunda aplicação disponibiliza o resultado consolidado:

```
GET /daily-balances/2026-08-08
```

O contrato definitivo é definido na etapa de desenho das APIs.

### RF-006 — Independência da consulta

A consulta de consolidação deve responder mesmo com o serviço de lançamentos
indisponível, pois lê de sua própria base. Ver [ADR-002](./decisions/ADR-002-service-decomposition.md).

---

## 3. Requisitos Não Funcionais

Esta é a parte arquiteturalmente mais relevante do desafio.

| ID | Requisito | Critério de aceite |
|----|-----------|--------------------|
| RNF-001 | Disponibilidade | Falha da consolidação não impede novos lançamentos |
| RNF-002 | Desacoplamento | Lançamentos e consolidação possuem responsabilidades e ciclos de vida independentes |
| RNF-003 | Performance | Consolidação suporta pico de 50 req/s |
| RNF-004 | Confiabilidade | Perda máxima no pico ≤ 5% |
| RNF-005 | Resiliência | Falhas temporárias na consolidação não causam indisponibilidade do fluxo principal |
| RNF-006 | Consistência | Consolidação opera com consistência eventual |
| RNF-007 | Recuperabilidade | Eventos não processados podem ser processados posteriormente |
| RNF-008 | Idempotência | Reprocessar uma movimentação não duplica seu impacto no saldo |
| RNF-009 | Testabilidade | Regras de negócio possuem testes automatizados |
| RNF-010 | Manutenibilidade | Código segue SOLID, Clean Code e separação clara de responsabilidades |
| RNF-011 | Tratamento de erros | Erros são tratados sem quebrar fluxos independentes |
| RNF-012 | Reprodutibilidade | Ambiente executável localmente de forma previsível |
| RNF-013 | Observabilidade | Fluxos críticos possuem logs suficientes para diagnóstico |
| RNF-014 | Documentação | Arquitetura, decisões, execução e trade-offs documentados |

### RNF-001 — Independência da consolidação

Este é o requisito arquitetural central do desafio.

Se **todos** estes componentes estiverem indisponíveis:

```
Consolidation API
RabbitMQ
Consolidation Worker
Consolidation DB
```

isto ainda precisa funcionar:

```
POST /transactions  →  201 Created
```

Ou seja:

```
Consolidação DOWN
        ↓
   Cash Flow API
        ↓
Lançamento criado normalmente
```

É exatamente este requisito que justifica a arquitetura assíncrona, e não a
vontade de sofisticar o projeto.

### RNF-003 / RNF-004 — Interpretação das "50 chamadas por segundo"

O enunciado diz que *"o sistema de consolidação chega a processar 50 chamadas por
segundo, tolerando uma perda máxima de 5%"*. A frase é ambígua: "chamadas" pode
significar (a) requisições HTTP de leitura do saldo consolidado ou (b) eventos de
lançamento a serem consolidados.

**Decisão:** tratamos os dois casos, porque a arquitetura precisa sustentar ambos:

| Cenário | Carga | Critério de aceite |
|---------|-------|--------------------|
| Escrita de lançamentos | 50 req/s em `POST /transactions` | ≥ 95% de sucesso |
| Ingestão de eventos | 50 eventos/s consolidados | ≥ 95% processados |
| Leitura de saldo | 50 req/s em `GET /daily-balances/{date}` | ≥ 95% de sucesso |

Os 5% são o **limite tolerado pelo desafio**, não a nossa meta. Meta interna:

```
taxa de erro HTTP  < 1%
perda de eventos   = 0%
```

A meta de perda zero se apoia no Outbox: o evento é gravado na mesma transação do
lançamento e permanece durável no banco até ser publicado, de modo que uma falha
do broker atrasa a consolidação em vez de descartar o evento. Ver
[ADR-004](./decisions/ADR-004-transactional-outbox.md).

A verificação é feita com k6 — ver [ADR-010](./decisions/ADR-010-performance-validation.md).

---

## 4. Restrições Técnicas Obrigatórias

Estes itens não são requisitos funcionais nem não funcionais: são **restrições**
impostas pelo desafio.

| ID | Restrição | Origem |
|----|-----------|--------|
| RT-001 | Backend desenvolvido em C# | Enunciado |
| RT-002 | Implementar testes automatizados | Enunciado |
| RT-003 | Aplicar Clean Code | Enunciado |
| RT-004 | Aplicar SOLID | Enunciado |
| RT-005 | Utilizar Design Patterns quando apropriado | Enunciado |
| RT-006 | Separar responsabilidades entre domínio, aplicação e infraestrutura | Enunciado |
| RT-007 | README detalhando execução e funcionamento | Enunciado |
| RT-008 | Código em repositório público no GitHub | Enunciado |

### Restrições auto-impostas

Vão além do exigido e são decisão nossa, não do enunciado:

| ID | Restrição | Justificativa |
|----|-----------|---------------|
| RT-101 | TDD como fluxo de desenvolvimento | O desafio exige testes; TDD faz com que sejam causa e não consequência do design |
| RT-102 | ADRs para decisões arquiteturais | Torna as escolhas auditáveis e reversíveis |
| RT-103 | Sem coautoria de IA em commits e PRs | Autoria exclusiva do desenvolvedor |
| RT-104 | CI executando build e testes em todo Pull Request | Transforma o critério de qualidade em barreira automática, e não em declaração |

### Requisitos opcionais do enunciado que decidimos atender

| Item opcional | Decisão | Onde |
|---------------|---------|------|
| Diagramas da arquitetura | Atender | [`architecture.md`](./architecture.md) |
| Processamento assíncrono / mensageria | Atender | [ADR-003](./decisions/ADR-003-messaging.md) |
| Containers (Docker) | Atender | [ADR-009](./decisions/ADR-009-containers.md) |

---

## 5. Matriz de Rastreabilidade — Requisito → Decisão

Esta matriz existe para transformar a arquitetura em **decisão justificável**, em
vez de parecer que RabbitMQ e Outbox entraram apenas para sofisticar o projeto.

| Requisito | Decisão | ADR |
|-----------|---------|-----|
| RNF-001 Disponibilidade | Cash Flow e Consolidation independentes | [ADR-002](./decisions/ADR-002-service-decomposition.md) |
| RNF-002 Desacoplamento | APIs e serviços separados, bases separadas | [ADR-002](./decisions/ADR-002-service-decomposition.md), [ADR-005](./decisions/ADR-005-database.md) |
| RNF-003 50 req/s | Processamento assíncrono | [ADR-003](./decisions/ADR-003-messaging.md) |
| RNF-004 Confiabilidade | RabbitMQ com ack manual e DLQ | [ADR-003](./decisions/ADR-003-messaging.md) |
| RNF-005 Resiliência | Mensageria + retry com backoff | [ADR-003](./decisions/ADR-003-messaging.md) |
| RNF-006 Consistência eventual | Comunicação por eventos | [ADR-006](./decisions/ADR-006-consistency.md) |
| RNF-007 Recuperabilidade | Transactional Outbox | [ADR-004](./decisions/ADR-004-transactional-outbox.md) |
| RNF-008 Idempotência | Registro de eventos processados no consumidor | [ADR-007](./decisions/ADR-007-idempotency.md) |
| RNF-009 Testabilidade | TDD | [ADR-008](./decisions/ADR-008-tdd.md) |
| RNF-010 Manutenibilidade | Clean Architecture + SOLID | [ADR-001](./decisions/ADR-001-architecture.md) |
| RNF-011 Tratamento de erros | Exceção de domínio + middleware de exceção → Problem Details + DLQ | [ADR-001](./decisions/ADR-001-architecture.md), [ADR-003](./decisions/ADR-003-messaging.md) |
| RNF-012 Reprodutibilidade | Docker Compose | [ADR-009](./decisions/ADR-009-containers.md) |
| RNF-013 Observabilidade | Logs estruturados + correlation id + health checks | [ADR-011](./decisions/ADR-011-observability.md) |
| RNF-014 Documentação | ADRs + README + diagramas | Este conjunto de documentos |
| RT-104 CI | GitHub Actions com build e testes por PR | [`testing-strategy.md`](./testing-strategy.md) §5 |
| RNF-003/004 Validação | Testes de carga com k6 | [ADR-010](./decisions/ADR-010-performance-validation.md) |
| RT-001 C# | .NET / C# | [ADR-012](./decisions/ADR-012-tech-stack.md) |
| RN-001..004 Valores monetários | Value Objects `Money` e `TransactionType` | [ADR-013](./decisions/ADR-013-money-representation.md) |
| RF-003 Consulta em volume | Paginação por cursor (keyset) | [ADR-014](./decisions/ADR-014-cursor-pagination.md) |

---

## 6. Premissas e Ambiguidades Assumidas

Registradas explicitamente para que a avaliação não confunda decisão com omissão.

| # | Ambiguidade | Premissa adotada |
|---|-------------|------------------|
| P-01 | "50 chamadas por segundo" — leitura ou ingestão? | Tratamos ambos os cenários (ver seção 3) |
| P-02 | Existe multi-lojista? | Não. Um único lojista implícito; sem `TenantId` no MVP |
| P-03 | Qual moeda? | BRL única; sem conversão cambial |
| P-04 | Fuso horário da consolidação | O dia é determinado por `OccurredAt` em UTC; documentado como limitação conhecida |
| P-05 | Lançamento pode ser editado/estornado? | Não no MVP. Lançamentos são imutáveis após criados |
| P-06 | Lançamento retroativo é permitido? | Sim — a consolidação do dia afetado é recalculada/incrementada |
| P-07 | Consolidação é histórica ou só do dia atual? | Por data arbitrária; o dia corrente é apenas um caso particular |

As premissas **P-08 a P-10** surgiram na definição dos contratos (etapa 4) e estão
registradas em [`api-contracts.md`](./api-contracts.md) §6, junto do contrato que
as originou. Elas resolvem ambiguidades de contrato, não do enunciado.

---

## 7. Definition of Done desta etapa

- [x] Requisitos funcionais identificados e priorizados
- [x] Requisitos não funcionais com critério de aceite
- [x] Restrições técnicas separadas dos requisitos
- [x] Ambiguidades do enunciado registradas como premissas
- [x] Matriz de rastreabilidade requisito → decisão
- [x] Escopo do MVP fechado em [`scope.md`](./scope.md)
