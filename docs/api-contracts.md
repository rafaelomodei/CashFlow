# Contratos de API e de Eventos

> Etapa 4 do projeto. Este documento é a **fronteira** entre os dois contextos e
> entre o backend e qualquer cliente. Mudá-lo depois da etapa 11 custa retrabalho
> dos dois lados — por isso ele é definido antes de existir código.
>
> Enquanto não houver implementação, **este documento é a fonte da verdade**. A
> partir da etapa 11, a especificação OpenAPI é gerada do código e passa a ter de
> concordar com ele (§8). Divergência entre os dois é defeito, não evolução.

## Índice

| Seção | Conteúdo |
|-------|----------|
| [1](#1-convenções-gerais) | Convenções gerais — datas, dinheiro, correlação, versionamento |
| [2](#2-cash-flow-api) | Cash Flow API — `POST /transactions`, `GET /transactions/{id}`, `GET /transactions` |
| [3](#3-consolidation-api) | Consolidation API — `GET /daily-balances/{date}` |
| [4](#4-erros-http) | Erros HTTP — Problem Details |
| [5](#5-evento-transactionregistered) | Evento `TransactionRegistered` |
| [6](#6-premissas-adotadas-nesta-etapa) | Premissas adotadas nesta etapa |
| [7](#7-rastreabilidade) | Rastreabilidade contrato → requisito → ADR |
| [8](#8-especificação-openapi) | Especificação OpenAPI |

---

## 1. Convenções gerais

### 1.1 Transporte

| Item | Valor |
|------|-------|
| Protocolo | HTTP/1.1 |
| Content-Type de requisição | `application/json; charset=utf-8` |
| Content-Type de resposta (sucesso) | `application/json; charset=utf-8` |
| Content-Type de resposta (erro) | `application/problem+json; charset=utf-8` |
| Codificação | UTF-8 em todos os casos |
| Convenção de nomes JSON | `camelCase` |

Campo desconhecido enviado no corpo é **ignorado**, não rejeitado. A recusa por
campo extra transforma um cliente ligeiramente adiantado em erro, sem ganho de
segurança — a validação relevante é a dos campos que existem.

### 1.2 Versionamento das APIs

As rotas são **não versionadas** (`/transactions`, `/daily-balances/{date}`).
Uma mudança incompatível futura introduziria o prefixo `/v2`, mantendo as rotas
atuais em funcionamento durante a migração. Não criamos `/v1` agora: um prefixo de
versão que nunca teve uma segunda versão é cerimônia, não compatibilidade.

Mudanças **aditivas** (novo campo opcional na resposta, novo parâmetro opcional
com padrão) não mudam a versão. O cliente é obrigado a ser tolerante a campos
desconhecidos.

### 1.3 Datas e horas

| Contexto | Formato | Exemplo |
|----------|---------|---------|
| Instante (corpo e resposta) | ISO 8601 com offset explícito | `2026-08-08T14:30:00Z` |
| Data civil (path e filtros) | ISO 8601 `YYYY-MM-DD` | `2026-08-08` |

Regras:

- Toda resposta emite instantes **normalizados em UTC**, com sufixo `Z`.
- Requisição pode enviar qualquer offset (`2026-08-08T11:30:00-03:00`); o servidor
  converte para UTC antes de persistir.
- Instante **sem** offset (`2026-08-08T14:30:00`) é **rejeitado** com `400`. Aceitá-lo
  exigiria adivinhar o fuso do cliente, e adivinhar fuso em domínio financeiro
  produz lançamento no dia errado de forma silenciosa.
- O **dia da consolidação** é a data UTC de `occurredAt` (premissa P-04,
  [ADR-013](./decisions/ADR-013-money-representation.md)). Limitação conhecida e
  aceita: um lançamento às 22h em Brasília (UTC−3) pertence ao dia seguinte em UTC.

### 1.4 Dinheiro

| Item | Regra |
|------|-------|
| Tipo JSON | número (`1500.00`), nunca string |
| Separador decimal | `.` — sem separador de milhar |
| Casas decimais | no máximo **2**; mais que isso é `400`, não arredondamento silencioso |
| Faixa | `0.01` a `9999999999999999.99` — limite de `numeric(18,2)` ([ADR-005](./decisions/ADR-005-database.md)) |
| Moeda | BRL implícita; não há campo `currency` (premissa P-03) |
| Sinal | `amount` é **sempre positivo**. O sinal deriva de `type` (RN-003) e nunca trafega no contrato |

Rejeitar `1500.005` em vez de arredondar é decisão deliberada: arredondamento
silencioso em dinheiro é a classe de defeito que não falha ruidosamente — ela só
produz um saldo errado que parece certo.

### 1.5 Correlação

Toda requisição aceita o header `X-Correlation-Id`. Ausente, o servidor gera um
GUID. Presente ou gerado, ele é:

1. devolvido no header `X-Correlation-Id` de **toda** resposta (sucesso e erro);
2. incluído no corpo de toda resposta de erro (§4);
3. propagado até o evento publicado (§5) e até os logs do worker.

Ver [ADR-011](./decisions/ADR-011-observability.md). É esse identificador que
transforma quatro processos em uma única história rastreável.

### 1.6 Identificadores

`UUID` versão 4 em representação canônica minúscula
(`6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f`). Gerados pelo servidor — o cliente nunca
propõe um id.

---

## 2. Cash Flow API

### 2.1 `POST /transactions` — registrar lançamento

Registra um lançamento financeiro. Atende RF-001 e RF-002 (UC-01).

O lançamento e o evento de integração são gravados na **mesma transação de banco**
([ADR-004](./decisions/ADR-004-transactional-outbox.md)). Consequência contratual
direta: **`201` não depende do RabbitMQ**. Com o broker inteiro fora do ar, esta
rota continua respondendo `201` (RNF-001).

#### Request

```http
POST /transactions HTTP/1.1
Content-Type: application/json
X-Correlation-Id: b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d
```

```json
{
  "type": "CREDIT",
  "amount": 1500.00,
  "occurredAt": "2026-08-08T14:30:00Z",
  "description": "Venda no balcão"
}
```

| Campo | Tipo | Obrigatório | Regra |
|-------|------|-------------|-------|
| `type` | string | **sim** | `CREDIT` ou `DEBIT`, maiúsculas, exato (RN-002) |
| `amount` | número | **sim** | `> 0`, até 2 casas decimais, dentro da faixa de §1.4 (RN-001) |
| `occurredAt` | string | não | Instante ISO 8601 com offset. Ausente → instante do servidor em UTC (P-08) |
| `description` | string | não | Até **200** caracteres. `null` e `""` são equivalentes a ausente |

#### `occurredAt`

Qualquer instante ISO 8601 válido é aceito e normalizado para UTC. Lançamento
retroativo é permitido (premissa P-06) — a consolidação do dia afetado é
atualizada normalmente, porque o worker usa `data.occurredAt` para escolher o dia
(RN-004).

Não há teto de retroatividade nem janela de datação futura. O enunciado não
restringe *quando* um lançamento pode ter ocorrido, e inventar um limite
arbitrário rejeitaria lançamento legítimo em nome de um problema que ninguém
levantou.

#### Response — `201 Created`

```http
HTTP/1.1 201 Created
Location: /transactions/6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f
Content-Type: application/json; charset=utf-8
X-Correlation-Id: b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d
```

```json
{
  "id": "6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f",
  "type": "CREDIT",
  "amount": 1500.00,
  "occurredAt": "2026-08-08T14:30:00Z",
  "description": "Venda no balcão",
  "createdAt": "2026-08-08T14:32:11Z"
}
```

| Campo | Observação |
|-------|------------|
| `id` | UUID gerado pelo servidor |
| `occurredAt` | Normalizado em UTC. Quando omitido no request, é igual a `createdAt` |
| `createdAt` | Instante do registro no sistema — distinto de `occurredAt` por definição |
| `description` | `null` quando não informada |

O corpo devolve o recurso **inteiro** para que o cliente não precise de uma segunda
requisição só para conhecer `id` e `createdAt`. `Location` aponta para §2.2.

A resposta **não** informa o saldo do dia. Informá-lo exigiria consultar a
consolidação de forma síncrona — exatamente o acoplamento que
[ADR-002](./decisions/ADR-002-service-decomposition.md) proíbe.

#### Erros

| Status | Situação |
|--------|----------|
| `400` | Qualquer violação da tabela de campos ou da janela de `occurredAt` |
| `400` | JSON malformado, ou instante sem offset |
| `415` | `Content-Type` diferente de `application/json` |
| `500` | Falha inesperada — inclusive indisponibilidade do `cashflow_db` |

Não existe `409`: lançamentos não têm chave natural e a solução não implementa
`Idempotency-Key` (registrado como melhoria futura em
[ADR-007](./decisions/ADR-007-idempotency.md) §"Idempotência na entrada da API").

---

### 2.2 `GET /transactions/{id}` — consultar um lançamento

Existe para dar destino real ao header `Location` de §2.1. Atende RF-003.

```http
GET /transactions/6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f HTTP/1.1
```

`200 OK` devolve exatamente o mesmo DTO de §2.1.

| Status | Situação |
|--------|----------|
| `200` | Lançamento encontrado |
| `400` | `id` não é um UUID válido |
| `404` | UUID válido, lançamento inexistente |

Este é o **único** ponto da API em que `404` significa "recurso não encontrado"
(§4.4).

---

### 2.3 `GET /transactions` — listar lançamentos

Atende RF-003 (UC-03). Paginação por **cursor** (keyset), não por offset — decisão
registrada em [ADR-014](./decisions/ADR-014-cursor-pagination.md).

```http
GET /transactions?limit=50&startDate=2026-08-01&endDate=2026-08-08 HTTP/1.1
```

#### Parâmetros

| Parâmetro | Tipo | Padrão | Regra |
|-----------|------|--------|-------|
| `limit` | inteiro | `50` | Entre `1` e `200`. Fora da faixa → `400` |
| `cursor` | string | — | Opaco, devolvido por `nextCursor`. Ausente → primeira página |
| `startDate` | `YYYY-MM-DD` | — | Filtra `occurredAt` **a partir de** `startDate` 00:00:00Z, inclusive |
| `endDate` | `YYYY-MM-DD` | — | Filtra `occurredAt` **até o fim** de `endDate`, inclusive — avaliado como `< endDate + 1 dia` |

`startDate` e `endDate` são independentes: qualquer um pode ser usado sozinho.
`startDate > endDate` → `400`. Ambos são interpretados em UTC, coerentes com §1.3.

`endDate` inclui o dia inteiro porque a alternativa — comparar uma data contra um
instante — faria `endDate=2026-08-08` excluir tudo que ocorreu depois da meia-noite
daquele mesmo dia. É o erro de intervalo mais comum em API de período, e ele é
silencioso.

#### Ordenação

```
occurredAt DESC, id DESC
```

Mais recentes primeiro — a ordem que o scroll infinito consome. `id` é
desempate obrigatório: sem ele, lançamentos com o mesmo `occurredAt` teriam ordem
indefinida e a paginação por cursor poderia pular ou repetir registros.
Não há parâmetro de ordenação configurável neste escopo.

#### Response — `200 OK`

```json
{
  "items": [
    {
      "id": "6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f",
      "type": "CREDIT",
      "amount": 1500.00,
      "occurredAt": "2026-08-08T14:30:00Z",
      "description": "Venda no balcão",
      "createdAt": "2026-08-08T14:32:11Z"
    },
    {
      "id": "1f0e9d8c-7b6a-4958-8473-625140f3e2d1",
      "type": "DEBIT",
      "amount": 700.00,
      "occurredAt": "2026-08-08T09:15:00Z",
      "description": null,
      "createdAt": "2026-08-08T09:15:42Z"
    }
  ],
  "nextCursor": "eyJvIjoiMjAyNi0wOC0wOFQwOToxNTowMFoiLCJpIjoiMWYwZTlkOGMtN2I2YS00OTU4LTg0NzMtNjI1MTQwZjNlMmQxIn0",
  "hasMore": true
}
```

| Campo | Tipo | Significado |
|-------|------|-------------|
| `items` | array | Página de lançamentos, no máximo `limit` elementos |
| `nextCursor` | string \| null | Cursor da próxima página. `null` quando não há mais |
| `hasMore` | booleano | `true` ⟺ `nextCursor != null`. Redundante por conveniência do cliente |

**Não existe `totalCount`.** Contá-lo custaria um `COUNT(*)` sobre o filtro a cada
página — exatamente o custo O(n) que a paginação por cursor foi escolhida para
evitar. O scroll infinito não precisa do total; ele precisa saber se há mais.

#### Comportamento do cursor

```
GET /transactions?limit=50                    →  items[50], nextCursor: "eyJ..."
GET /transactions?limit=50&cursor=eyJ...      →  items[50], nextCursor: "eyK..."
GET /transactions?limit=50&cursor=eyK...      →  items[12], nextCursor: null, hasMore: false
```

Regras:

1. O cursor é **opaco**. O cliente não interpreta, não constrói e não altera seu
   conteúdo — o formato interno pode mudar sem aviso.
2. O cursor codifica **apenas a posição**. Os filtros (`startDate`, `endDate`,
   `limit`) precisam ser reenviados a cada requisição, idênticos.
3. Mudar o filtro no meio da paginação é permitido, mas o resultado é "continuar a
   partir daquela posição com o novo filtro" — não "reiniciar". Para reiniciar,
   basta omitir `cursor`.
4. Cursor sintaticamente inválido, truncado ou não decodificável → `400`.

Formato interno (documentado para revisão, **não** para consumo): base64url, sem
padding, de

```json
{ "o": "2026-08-08T09:15:00.0000000Z", "i": "1f0e9d8c-7b6a-4958-8473-625140f3e2d1" }
```

que a consulta traduz em

```sql
WHERE (occurred_at, id) < (@o, @i)
ORDER BY occurred_at DESC, id DESC
LIMIT @limit
```

#### Coleção vazia

Período sem lançamentos **não** é erro:

```json
{ "items": [], "nextCursor": null, "hasMore": false }
```

`200 OK`, nunca `404`. Ausência de resultado é uma resposta legítima da consulta;
`404` diria que a *rota* não existe.

#### Erros

| Status | Situação |
|--------|----------|
| `400` | `limit` fora de `[1, 200]` ou não numérico |
| `400` | `cursor` inválido |
| `400` | `startDate` ou `endDate` fora do formato `YYYY-MM-DD` |
| `400` | `startDate > endDate` |
| `500` | Falha inesperada |

---

## 3. Consolidation API

### 3.1 `GET /daily-balances/{date}` — saldo consolidado do dia

Atende RF-004, RF-005 e RF-006 (UC-02). Lê o `consolidation_db`, que é banco
próprio: responde normalmente com **toda** a Cash Flow API fora do ar.

```http
GET /daily-balances/2026-08-08 HTTP/1.1
```

| Elemento | Regra |
|----------|-------|
| `{date}` | `YYYY-MM-DD`, data civil **em UTC** (P-04). Sem hora, sem offset |

#### Response — `200 OK`

```json
{
  "date": "2026-08-08",
  "totalCredits": 1500.00,
  "totalDebits": 700.00,
  "balance": 800.00,
  "updatedAt": "2026-08-08T14:32:15Z"
}
```

| Campo | Tipo | Significado |
|-------|------|-------------|
| `date` | `YYYY-MM-DD` | O dia consultado, ecoado |
| `totalCredits` | número | Σ dos lançamentos `CREDIT` do dia |
| `totalDebits` | número | Σ dos lançamentos `DEBIT` do dia — **positivo**, sem sinal |
| `balance` | número | `totalCredits − totalDebits` (RF-004). Pode ser **negativo** |
| `updatedAt` | string \| null | Instante da última aplicação de evento a este dia |

`updatedAt` não é metadado decorativo: ele é a **evidência da consistência
eventual** ([ADR-006](./decisions/ADR-006-consistency.md)). `now − updatedAt` é a
defasagem observável da consolidação. Sem ele, o cliente não teria como distinguir
"saldo atualizado" de "worker parado há duas horas".

#### Dia sem lançamentos

```json
{
  "date": "2026-08-09",
  "totalCredits": 0.00,
  "totalDebits": 0.00,
  "balance": 0.00,
  "updatedAt": null
}
```

`200 OK` com saldo zerado e `updatedAt: null` — **nunca `404`**, conforme
[ADR-006](./decisions/ADR-006-consistency.md). Um dia sem movimentação tem saldo
zero; ele não deixa de existir. `404` obrigaria todo cliente a tratar "não
encontrado" como "zero", movendo regra de negócio para dentro do cliente.

A mesma resposta vale para uma data futura e para uma data anterior ao início da
operação. O contrato não distingue "ainda não aconteceu" de "não houve
movimentação" — a distinção não muda o saldo.

> ⚠️ **Consequência aceita:** um lançamento recém-registrado pode ainda não estar
> refletido aqui. A janela típica é de poucos segundos e o valor converge sem
> intervenção ([ADR-006](./decisions/ADR-006-consistency.md)).

#### Erros

| Status | Situação |
|--------|----------|
| `400` | `{date}` fora do formato `YYYY-MM-DD`, ou data inexistente no calendário (`2026-02-30`) |
| `500` | Falha inesperada — inclusive indisponibilidade do `consolidation_db` |

---

## 4. Erros HTTP

### 4.1 Formato

Todo erro usa **Problem Details**, [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807)
(atualizada pela [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)), com
`Content-Type: application/problem+json`.

Adotamos um padrão existente em vez de inventar um envelope próprio: ele já é
compreendido por ferramenta e cliente, e o ASP.NET Core o produz nativamente.

Campos:

| Campo | Origem | Sempre presente |
|-------|--------|-----------------|
| `type` | URI que identifica a classe do problema | sim |
| `title` | Descrição curta e estável da classe do problema | sim |
| `status` | Código HTTP, repetido no corpo | sim |
| `detail` | Descrição legível desta ocorrência | sim |
| `instance` | Caminho da requisição que falhou | sim |
| `correlationId` | Extensão nossa — §1.5, [ADR-011](./decisions/ADR-011-observability.md) | sim |
| `errors` | Extensão nossa — erros campo a campo | só em `400` de validação |

`title` é estável e serve para o cliente decidir comportamento; `detail` é
humano e pode mudar. Cliente que faz `switch` em `detail` está errado por
construção — é por isso que a distinção está escrita aqui.

### 4.2 `400 Bad Request` — validação

O caso mais frequente. `errors` mapeia **campo → lista de mensagens**, e todos os
campos inválidos vêm de uma vez: validar em cascata obrigaria o cliente a um
ciclo de tentativa e erro por campo.

```json
{
  "type": "https://cashflow.dev/problems/validation-error",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "instance": "/transactions",
  "correlationId": "b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "errors": {
    "amount": ["Amount must be greater than zero."],
    "type": ["Type must be either CREDIT or DEBIT."],
    "occurredAt": ["OccurredAt must be a valid ISO 8601 instant."]
  }
}
```

A chave de `errors` é o nome do campo em `camelCase`, **igual ao do request** —
inclusive para parâmetros de query (`limit`, `cursor`, `startDate`).

### 4.3 `400 Bad Request` — requisição malformada

Quando o corpo sequer pode ser interpretado, não há campo a apontar e `errors` é
omitido:

```json
{
  "type": "https://cashflow.dev/problems/malformed-request",
  "title": "Malformed request",
  "status": 400,
  "detail": "The request body is not valid JSON.",
  "instance": "/transactions",
  "correlationId": "b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d"
}
```

### 4.4 `404 Not Found`

Aplica-se em **dois** casos, e apenas neles:

1. `GET /transactions/{id}` com UUID válido de lançamento inexistente (§2.2);
2. rota inexistente.

```json
{
  "type": "https://cashflow.dev/problems/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Transaction '6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f' was not found.",
  "instance": "/transactions/6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f",
  "correlationId": "b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d"
}
```

**Não** é `404`:

| Situação | Resposta correta | Por quê |
|----------|------------------|---------|
| `GET /transactions` sem resultados | `200` com `items: []` | Coleção vazia é resultado, não ausência |
| `GET /daily-balances/{date}` sem movimentação | `200` com saldo zerado | O saldo do dia é zero, e zero é um valor ([ADR-006](./decisions/ADR-006-consistency.md)) |

### 4.5 `415 Unsupported Media Type`

`POST /transactions` com `Content-Type` diferente de `application/json`.

```json
{
  "type": "https://cashflow.dev/problems/unsupported-media-type",
  "title": "Unsupported media type",
  "status": 415,
  "detail": "Content-Type must be 'application/json'.",
  "instance": "/transactions",
  "correlationId": "b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d"
}
```

### 4.6 `500 Internal Server Error`

Resposta deliberadamente **opaca**. Stack trace, mensagem de exceção, nome de
tabela e string de conexão nunca aparecem no corpo — eles vão para o log
estruturado, indexados pelo mesmo `correlationId`.

```json
{
  "type": "https://cashflow.dev/problems/internal-error",
  "title": "Internal server error",
  "status": 500,
  "detail": "An unexpected error occurred. Use the correlationId to trace it.",
  "instance": "/transactions",
  "correlationId": "b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d"
}
```

É esse contrato que dá função ao `correlationId`: o cliente não recebe o
diagnóstico, mas recebe a chave que permite obtê-lo do suporte.

### 4.7 Tabela consolidada

| Status | `type` | Quando |
|--------|--------|--------|
| `400` | `validation-error` | Campo ou parâmetro viola uma regra |
| `400` | `malformed-request` | JSON inválido, instante sem offset, cursor indecifrável |
| `404` | `not-found` | Lançamento inexistente ou rota inexistente |
| `405` | `method-not-allowed` | Verbo não suportado pela rota |
| `415` | `unsupported-media-type` | `Content-Type` não é JSON |
| `500` | `internal-error` | Qualquer falha não prevista |

O domínio de `type` (`https://cashflow.dev/problems/...`) é um identificador
estável, não uma URL que precise resolver. Documentá-lo aqui é o suficiente no
escopo do desafio.

---

## 5. Evento `TransactionRegistered`

Único evento de integração do sistema. É o **contrato mais caro de mudar** do
projeto: ele atravessa `Shared.Contracts`, o outbox, o broker e o worker.

### 5.1 Envelope

```json
{
  "eventId": "0d9f1f4c-2f4e-4c1a-9a4e-6b1c0c2f0f11",
  "eventType": "TransactionRegistered",
  "eventVersion": 1,
  "occurredAt": "2026-08-08T14:32:11Z",
  "correlationId": "b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "data": {
    "transactionId": "6c6a2f4e-1b2c-4d3e-8f90-1a2b3c4d5e6f",
    "type": "CREDIT",
    "amount": 1500.00,
    "occurredAt": "2026-08-08T14:30:00Z"
  }
}
```

| Campo | Tipo | Papel |
|-------|------|-------|
| `eventId` | UUID | **Chave de idempotência** ([ADR-007](./decisions/ADR-007-idempotency.md)). Gerado uma vez, na gravação do outbox |
| `eventType` | string | Discriminador. `TransactionRegistered` |
| `eventVersion` | inteiro | Versão do schema. `1` nesta versão do contrato |
| `occurredAt` | instante | Momento da **emissão** do evento |
| `correlationId` | UUID | Correlação ponta a ponta ([ADR-011](./decisions/ADR-011-observability.md)) |
| `data.transactionId` | UUID | Id do lançamento no `cashflow_db` |
| `data.type` | string | `CREDIT` ou `DEBIT`, sempre como **texto** ([ADR-013](./decisions/ADR-013-money-representation.md)) |
| `data.amount` | número | Sempre positivo, 2 casas decimais |
| `data.occurredAt` | instante | Momento do **fato econômico** — determina o dia da consolidação (RN-004) |

### 5.2 Os dois `occurredAt`

A duplicidade de nome é intencional e é o ponto mais fácil de errar do contrato:

```
envelope.occurredAt   quando o evento foi emitido     → usado em log e diagnóstico
data.occurredAt       quando o lançamento aconteceu   → usado para escolher o dia do saldo
```

Em um lançamento retroativo eles ficam a meses de distância. **O worker consolida
por `data.occurredAt`.** Consolidar pelo envelope colocaria todo lançamento
retroativo no dia da emissão — um saldo errado que não falha em lugar nenhum.

### 5.3 `eventId` ≠ `transactionId`

São identificadores de coisas diferentes e não devem ser confundidos:

| Identificador | Identifica | Cardinalidade |
|---------------|------------|---------------|
| `data.transactionId` | O lançamento | 1 por lançamento |
| `eventId` | A mensagem sobre o lançamento | 1 por mensagem no outbox |

Hoje há exatamente um evento por lançamento, o que os torna aparentemente
intercambiáveis. Usar `transactionId` como chave de idempotência funcionaria hoje
e quebraria no primeiro evento adicional sobre o mesmo lançamento. `eventId` é a
chave de `processed_events` ([ADR-007](./decisions/ADR-007-idempotency.md)).

Uma **reentrega** do mesmo evento mantém o mesmo `eventId` — é precisamente isso
que permite ao consumidor descartá-la.

### 5.4 Transporte AMQP

Topologia definida em [ADR-003](./decisions/ADR-003-messaging.md):

| Elemento | Valor |
|----------|-------|
| Exchange | `cashflow.transactions` (topic, durable) |
| Routing key | `transaction.registered` |
| Fila | `consolidation.transaction-registered` (durable) |
| Dead-letter exchange | `cashflow.transactions.dlx` |
| DLQ | `consolidation.transaction-registered.dlq` |

Propriedades da mensagem:

| Propriedade | Valor |
|-------------|-------|
| `content_type` | `application/json` |
| `content_encoding` | `utf-8` |
| `delivery_mode` | `2` (persistente) |
| `message_id` | `eventId` |
| `type` | `eventType` |
| `timestamp` | `occurredAt` do envelope |
| `correlation_id` | `correlationId` |
| header `x-event-version` | `eventVersion` |

`correlationId` viaja **no envelope JSON e na propriedade AMQP**. A duplicação é
proposital: a propriedade permite inspecionar a correlação na UI do RabbitMQ sem
desserializar o corpo, e o envelope garante que a informação sobreviva a qualquer
transporte futuro.

### 5.5 Compatibilidade e evolução

| Mudança | Compatível? | Como proceder |
|---------|-------------|---------------|
| Adicionar campo **opcional** | Sim | Publicar direto; consumidores toleram campo desconhecido |
| Remover campo | **Não** | Novo `eventVersion` e nova routing key |
| Renomear campo | **Não** | Idem |
| Mudar tipo ou semântica de campo | **Não** | Idem |
| Mudar unidade ou escala de `amount` | **Não** | Idem |

Duas regras sustentam isso:

1. **Consumidor tolerante:** campo desconhecido é ignorado, nunca causa erro de
   desserialização nem envio à DLQ. Sem isso, todo produtor fica travado.
2. **Mudança incompatível é evento novo:** publica-se em
   `transaction.registered.v2`, com fila própria, e as duas versões coexistem até
   que a antiga deixe de ter produtor. Nunca se altera o schema de uma versão já
   publicada — mensagens antigas podem estar retidas no outbox ou na fila.

Isso é o que [`decisions/README.md`](./decisions/README.md) classifica como
"mudar o contrato de evento de forma incompatível": exige **ADR nova**.

O contrato materializa-se como records em `Shared.Contracts` na etapa 5 — apenas o
contrato, sem regra de negócio.

---

## 6. Premissas adotadas nesta etapa

Continuação da numeração de [`requirements.md`](./requirements.md) §6.

| # | Ambiguidade | Premissa adotada |
|---|-------------|------------------|
| P-08 | `occurredAt` é obrigatório no request? | Não. Ausente → instante do servidor em UTC |
| ~~P-09~~ | ~~Existe limite para retroatividade e para datação futura?~~ | **Descartada.** Qualquer instante válido é aceito |
| P-10 | Qual o tamanho máximo de `description`? | 200 caracteres |

P-09 era a única premissa desta etapa que **restringia** um comportamento antes
irrestrito, e foi removida antes de virar código: o enunciado não limita quando um
lançamento pode ter ocorrido, e o teto de 365 dias era regra de negócio inventada
por nós. O número não é reaproveitado.

---

## 7. Rastreabilidade

| Elemento do contrato | Requisito | Decisão |
|----------------------|-----------|---------|
| `POST /transactions` | RF-001, RF-002 | [ADR-004](./decisions/ADR-004-transactional-outbox.md) |
| `201` independente do broker | RNF-001, RNF-005 | [ADR-004](./decisions/ADR-004-transactional-outbox.md) |
| `amount > 0`, 2 casas | RN-001 | [ADR-013](./decisions/ADR-013-money-representation.md) |
| `type` como `CREDIT`/`DEBIT` textual | RN-002, RN-003 | [ADR-013](./decisions/ADR-013-money-representation.md) |
| `occurredAt` define o dia | RN-004, P-04 | [ADR-013](./decisions/ADR-013-money-representation.md) |
| `GET /transactions/{id}` | RF-003 | — |
| `GET /transactions` com cursor | RF-003 | [ADR-014](./decisions/ADR-014-cursor-pagination.md) |
| `GET /daily-balances/{date}` | RF-004, RF-005, RF-006 | [ADR-002](./decisions/ADR-002-service-decomposition.md) |
| `updatedAt` na resposta de saldo | RNF-006 | [ADR-006](./decisions/ADR-006-consistency.md) |
| Dia sem movimento → `200` zerado | RF-005 | [ADR-006](./decisions/ADR-006-consistency.md) |
| Problem Details + `correlationId` | RNF-011, RNF-013 | [ADR-011](./decisions/ADR-011-observability.md) |
| `eventId` no envelope | RNF-008 | [ADR-007](./decisions/ADR-007-idempotency.md) |
| Topologia e propriedades AMQP | RNF-003, RNF-004 | [ADR-003](./decisions/ADR-003-messaging.md) |
| Política de evolução do schema | RNF-002 | [ADR-003](./decisions/ADR-003-messaging.md) |

### O que este documento deliberadamente **não** define

| Item | Por quê |
|------|---------|
| Autenticação e autorização | Fora do escopo ([`scope.md`](./scope.md)) |
| `Idempotency-Key` em `POST /transactions` | Melhoria futura ([ADR-007](./decisions/ADR-007-idempotency.md)) |
| Rate limiting | Não solicitado; k6 valida capacidade, não proteção |
| Campo `currency` | BRL única (P-03) |
| Edição, exclusão e estorno | Lançamentos imutáveis (P-05) |
| Saldo acumulado ou por período | O enunciado pede saldo **diário** |
| Endpoint de reprocessamento da DLQ | Melhoria futura ([ADR-003](./decisions/ADR-003-messaging.md)) |

---

## 8. Especificação OpenAPI

| Item | Decisão |
|------|---------|
| Geração | `Microsoft.AspNetCore.OpenApi` — nativo do .NET, sem dependência externa ([ADR-012](./decisions/ADR-012-tech-stack.md)) |
| Fonte | O **código**, a partir da etapa 11 |
| Documento | `/openapi/v1.json`, em cada uma das duas APIs |
| UI | Swagger UI em `/swagger`, habilitada apenas fora do ambiente `Production` |
| Versão | OpenAPI 3.1 |

Cada API publica a **sua** especificação. Não há documento unificado: um único
arquivo descrevendo os dois serviços sugeriria um gateway que
[ADR-002](./decisions/ADR-002-service-decomposition.md) decidiu não ter.

O evento de §5 **não** entra na OpenAPI — ela descreve HTTP. Se um dia for
necessário publicá-lo em formato de máquina, AsyncAPI é o caminho; hoje o contrato
escrito aqui e os records de `Shared.Contracts` são suficientes.

Relação entre este documento e a especificação gerada:

```
etapas 4 → 10        api-contracts.md  é a fonte da verdade
etapa  11 em diante  OpenAPI é gerada do código e precisa concordar com este documento
                     divergência = defeito, corrigido no código ou registrado aqui
```

---

## 9. Definition of Done desta etapa

- [x] Todos os endpoints do escopo possuem contrato completo
- [x] Todos os status HTTP possíveis estão definidos
- [x] O evento possui schema estável e versionado
- [x] Ambiguidades de contrato foram eliminadas e registradas como premissas
- [x] Nenhuma regra de negócio nova foi inventada — apenas limites de contrato,
      configuráveis e explicitados em §6
