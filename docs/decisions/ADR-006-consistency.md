# ADR-006 — Consistência eventual entre lançamentos e saldo consolidado

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

[ADR-002](./ADR-002-service-decomposition.md) e [ADR-005](./ADR-005-database.md)
separaram os contextos em processos e bancos distintos. Isso tem uma consequência
inevitável, pelo teorema CAP e pela simples ausência de transação distribuída:

**não é possível ter, ao mesmo tempo, independência de falha e consistência forte
entre os dois lados.**

O enunciado escolheu por nós: ele exige que os lançamentos continuem funcionando
mesmo com a consolidação fora do ar. Ao exigir disponibilidade sob partição, ele
abre mão de consistência imediata.

## Decisão

O sistema opera com **consistência eventual** entre lançamentos e saldo
consolidado.

Fluxo aceito:

```
Lançamento criado           t0
        ↓
Evento persistido (outbox)  t0   (atômico com o lançamento)
        ↓
Mensagem publicada          t0 + Δ1   (ciclo do publisher)
        ↓
Worker processa             t0 + Δ2   (entrega + consumo)
        ↓
Saldo atualizado            t0 + Δ3
```

Durante a janela `t0 → t0+Δ3`, o lançamento **existe** e o saldo **ainda não o
reflete**. Isso é comportamento esperado, não defeito.

### Garantias que assumimos

| Garantia | Status |
|----------|--------|
| Nenhum lançamento é perdido | ✅ Garantido — outbox atômico |
| Nenhum evento é perdido | ✅ Garantido — outbox + fila durável |
| Todo evento é aplicado ao menos uma vez | ✅ Garantido — at-least-once |
| Todo evento é aplicado no máximo uma vez ao saldo | ✅ Garantido — idempotência ([ADR-007](./ADR-007-idempotency.md)) |
| O saldo converge para o valor correto | ✅ Garantido, dado tempo suficiente |
| O saldo reflete o lançamento imediatamente | ❌ **Não garantido** — e não é exigido |
| Ordem de aplicação dos eventos | ❌ Não garantida — irrelevante: soma é comutativa |

O fato de a consolidação ser uma **soma** é o que torna a consistência eventual
segura aqui. Adição é comutativa e associativa: a ordem de chegada dos eventos não
altera o resultado final. Se a operação fosse dependente de ordem (por exemplo,
um saldo com limite que rejeita débitos), essa decisão precisaria ser revista.

### Janela de inconsistência esperada

| Etapa | Ordem de grandeza |
|-------|-------------------|
| Ciclo do outbox publisher | segundos |
| Entrega pelo broker | milissegundos |
| Processamento no worker | milissegundos |
| **Total em operação normal** | **poucos segundos** |

Sob falha, a janela cresce até a duração da indisponibilidade — mas o resultado
final continua correto, porque nada é descartado.

### Como isso é comunicado ao cliente

A resposta da consulta de saldo expõe o momento da última atualização, para que a
defasagem seja observável em vez de invisível:

```json
{
  "date": "2026-08-08",
  "totalCredits": 1500.00,
  "totalDebits": 700.00,
  "balance": 800.00,
  "updatedAt": "2026-08-08T14:32:15Z"
}
```

Uma data sem lançamentos consolidados retorna saldo zerado com `updatedAt` nulo —
não `404`. Ausência de movimentação é uma resposta legítima, não um erro.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| Consistência forte via transação distribuída (2PC) | Saldo sempre exato | Bloqueante, frágil, acopla a disponibilidade dos dois contextos — trabalha contra RNF-001 | Rejeitada |
| Consolidação síncrona no `POST /transactions` | Simples de entender, saldo imediato | Acoplamento temporal total: consolidação fora do ar impede o lançamento | Rejeitada — contraria RNF-001 |
| Saldo calculado sob demanda (`SUM` na hora) | Sempre exato, sem duplicação | Exigiria acesso ao banco de lançamentos — reintroduz o acoplamento; custo cresce com o histórico | Rejeitada |
| Consistência eventual por eventos | Independência de falha, absorve pico | Janela de defasagem; exige idempotência | **Escolhida** |
| Job em lote no fim do dia | Simples | Saldo do dia corrente indisponível durante o dia | Rejeitada |

## Consequências

**Positivas**

- Independência real entre os contextos (RNF-001, RNF-002).
- Picos são absorvidos como latência, não como erro (RNF-003).
- O sistema se autorrecupera: ao voltar de uma falha, converge sem intervenção.

**Negativas**

- Um lançamento recém-criado pode não aparecer imediatamente no saldo.
- Exige idempotência obrigatória no consumidor.
- Divergência prolongada é possível se o worker ficar muito tempo fora, e precisa
  ser observável (RNF-013).

## Trade-off aceito

Aceitamos que o saldo seja **eventualmente correto** em vez de **imediatamente
correto**. O domínio suporta: o requisito é um relatório de saldo **diário**, não
um saldo em tempo real com decisão transacional dependente dele. Nenhuma regra de
negócio deste escopo rejeita um lançamento com base no saldo atual — se essa regra
existisse, a consistência eventual seria inadequada.

Como rede de segurança, `transactions` permanece a fonte da verdade: o saldo é uma
projeção e pode ser recalculado integralmente a partir dela.

## Requisitos atendidos

RNF-001, RNF-002, RNF-006

## Como validar

- Teste de integração: registrar lançamento, aguardar convergência, verificar saldo.
- Teste de convergência sob falha: derrubar o worker, registrar N lançamentos,
  subir o worker, verificar que o saldo final é exatamente `Σcréditos − Σdébitos`.
- Teste de comutatividade: aplicar os mesmos eventos em ordens diferentes produz
  o mesmo saldo.
