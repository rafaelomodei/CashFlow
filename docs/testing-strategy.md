# Estratégia de Testes

> A justificativa da escolha por TDD está em [ADR-008](./decisions/ADR-008-tdd.md).
> Este documento é o **plano prático**: o que testar, em que nível e com qual nome.

## 1. Ciclo de trabalho

Cada funcionalidade nasce de um ciclo explícito:

```
1. RED       escrever o teste que descreve o comportamento desejado
             → rodar → ver falhar (falhar por compilação também conta)

2. GREEN     escrever a implementação mais simples que faz passar
             → rodar → ver passar

3. REFACTOR  melhorar nomes, extrair, remover duplicação
             → rodar → continuar verde
```

O commit segue o ciclo, não o arquivo:

```
test(domain): adicionar teste de valor negativo em Money      # RED
feat(domain): rejeitar valor negativo em Money                # GREEN
refactor(domain): extrair validação de Money                  # REFACTOR
```

Isso torna o TDD **verificável no histórico**, e não apenas uma afirmação no README.

## 2. Níveis e responsabilidades

| Nível | O que valida | O que **não** valida | Infra |
|-------|--------------|----------------------|-------|
| Unitário | Regras de domínio e orquestração dos casos de uso | SQL, serialização, rede | Nenhuma |
| Integração | Repositórios, outbox, consumidor, endpoints | Comportamento sob carga | Postgres + RabbitMQ em container |
| Arquitetura | Regra de dependência entre camadas | Comportamento | Nenhuma |
| Carga | RNF-003 e RNF-004 | Regra de negócio | Ambiente completo |

Regra de decisão: **se o teste precisa de I/O para ter valor, é de integração; se
não precisa, é unitário e deve rodar em milissegundos.**

## 3. Convenções

**Nomenclatura** — `Método_Cenário_ResultadoEsperado`:

```csharp
Create_WithNegativeAmount_ShouldThrowDomainException
Handle_WhenEventAlreadyProcessed_ShouldNotChangeBalance
ApplyTo_WhenTypeIsDebit_ShouldReturnNegativeValue
```

**Estrutura** — Arrange / Act / Assert explícito, com linha em branco separando os
três blocos.

**Regras**

- Um comportamento por teste; múltiplos asserts apenas sobre o mesmo resultado.
- Testar comportamento observável, nunca detalhe interno de implementação.
- Sem lógica condicional dentro de teste.
- Sem dependência de ordem de execução ou de estado compartilhado.
- Builders / Object Mothers para montar cenários, evitando setup repetido.
- Nada de mock de banco em teste de integração — infraestrutura real em container.

## 4. Plano de testes por requisito

### Domínio — `Transaction`, `Money`, `TransactionType`

| Cenário | Esperado | Requisito |
|---------|----------|-----------|
| Criar lançamento válido | Lançamento criado com valor e tipo corretos | RF-001 |
| Valor zero | Exceção de domínio | RN-001 |
| Valor negativo | Exceção de domínio | RN-001 |
| Valor com mais de 2 casas decimais | Normalizado para 2 casas | RN-001 |
| Tipo inválido | Exceção de domínio | RN-002 |
| `CREDIT.ApplyTo(100)` | `+100` | RN-003 |
| `DEBIT.ApplyTo(100)` | `−100` | RN-003 |
| Dois `Money` de mesmo valor | Iguais por valor | ADR-013 |
| Soma de valores com centavos | Sem erro de arredondamento | RN-001 |

### Aplicação — `RegisterTransactionUseCase`

| Cenário | Esperado | Requisito |
|---------|----------|-----------|
| Entrada válida | Persiste lançamento e grava evento no outbox | RF-001, ADR-004 |
| Entrada inválida | Não persiste nada, retorna erro | RN-001, RNF-011 |
| Falha ao gravar o outbox | Rollback do lançamento | ADR-004 |
| Evento gerado | Contém `eventId`, tipo, valor e `occurredAt` | ADR-004 |

### Aplicação — `ConsolidateTransactionUseCase`

| Cenário | Esperado | Requisito |
|---------|----------|-----------|
| Crédito em dia sem saldo | Cria saldo com o valor do crédito | RF-004 |
| Débito em dia sem saldo | Cria saldo negativo | RF-004 |
| Crédito em dia com saldo | Soma ao saldo existente | RF-004 |
| Mesmo `eventId` duas vezes | Saldo alterado apenas uma vez | RNF-008 |
| Eventos em ordens diferentes | Mesmo saldo final | ADR-006 |
| Lançamento retroativo | Consolida no dia de `occurredAt` | P-06 |

### Aplicação — `GetDailyBalanceUseCase`

| Cenário | Esperado | Requisito |
|---------|----------|-----------|
| Dia com movimentação | Retorna créditos, débitos, saldo e `updatedAt` | RF-005 |
| Dia sem movimentação | Retorna saldo zerado (não `404`) | ADR-006 |
| Data em formato inválido | `400 Bad Request` | RNF-011 |

### Integração — Outbox

| Cenário | Esperado | Requisito |
|---------|----------|-----------|
| Registrar lançamento | Lançamento e outbox gravados na mesma transação | ADR-004 |
| Broker fora do ar | `POST` retorna `201` e mensagem fica pendente | RNF-001 |
| Broker volta | Pendentes são publicadas e o saldo converge | RNF-007 |
| Falha na publicação | Mensagem não é marcada como processada; `attempts` incrementa | ADR-004 |
| Dois publishers simultâneos | Nenhuma mensagem publicada em duplicidade (SKIP LOCKED) | ADR-004 |

### Integração — Consumidor

| Cenário | Esperado | Requisito |
|---------|----------|-----------|
| Mesmo `eventId` publicado 10 vezes | Saldo aplicado uma única vez | RNF-008 |
| Crash entre efeito e `ack` | Reprocessamento não altera o saldo | RNF-008 |
| Mensagem malformada | Vai para a DLQ sem travar a fila | RNF-011 |
| N workers concorrentes | Mesmo saldo final que um worker único | ADR-007 |
| Banco de consolidação fora do ar | `nack`/requeue; nada perdido | RNF-005 |

### Integração — Resiliência ponta a ponta

| Cenário | Esperado | Requisito |
|---------|----------|-----------|
| Todo o contexto de consolidação parado | `POST /transactions` segue em `201` | **RNF-001** |
| Consolidação volta após N lançamentos | Saldo converge para `Σcréditos − Σdébitos` | RNF-006, RNF-007 |
| `cashflow-db` fora do ar | `/health/ready` da Cash Flow API falha | RNF-013 |
| RabbitMQ fora do ar | `/health/ready` da Cash Flow API continua `200` | ADR-011 |

O primeiro cenário desta tabela é **o teste mais importante do projeto**: é a
verificação direta do único requisito não funcional que o enunciado enuncia como
obrigação.

### Arquitetura

| Regra | Esperado |
|-------|----------|
| `Domain` não referencia EF Core, RabbitMQ ou ASP.NET | Teste falha se referenciar |
| `Application` não referencia `Infrastructure` | Teste falha se referenciar |
| Casos de uso dependem apenas de interfaces | Teste falha se dependerem de classe concreta de infra |
| Entidades de domínio não expõem setters públicos | Teste falha se expuserem |

## 5. Execução

```bash
dotnet test                                        # suíte completa
dotnet test --filter Category=Unit                 # apenas unitários (sem Docker)
dotnet test --filter Category=Integration          # exige Docker
dotnet test --collect:"XPlat Code Coverage"        # com cobertura
```

Testes de integração exigem Docker disponível (Testcontainers). Testes unitários e
de arquitetura não exigem nada além do SDK.

## 6. Critério de pronto

Nenhuma alteração é considerada concluída sem:

- [ ] Teste escrito antes da implementação
- [ ] Suíte completa verde
- [ ] Regra de negócio nova coberta em nível unitário
- [ ] Comportamento de infraestrutura novo coberto em nível de integração
- [ ] Testes de arquitetura passando
