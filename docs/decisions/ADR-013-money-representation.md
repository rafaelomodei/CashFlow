# ADR-013 — Representação de valores monetários e do tipo de lançamento

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

O domínio é financeiro. Duas escolhas aparentemente triviais concentram a maior
parte do risco de defeito silencioso:

1. **Como representar dinheiro.** `double`/`float` são binários e não representam
   exatamente valores decimais. `0.1 + 0.2 != 0.3` é irrelevante em muitos
   domínios e inaceitável em um saldo.
2. **Como representar o tipo do lançamento.** Se crédito/débito for uma `string`
   solta, cada ponto do código precisa validar e comparar texto, e o sinal do
   valor vira decisão dispersa — exatamente o tipo de regra que se duplica e
   diverge.

## Decisão

### Dinheiro: `decimal` encapsulado em um Value Object `Money`

- Tipo C#: `decimal` (base 10, exato para valores monetários).
- Coluna: `numeric(18,2)` — ver [ADR-005](./ADR-005-database.md).
- Encapsulado em um Value Object **imutável** `Money`:
  - impede valor negativo na criação (RN-001);
  - normaliza a escala para 2 casas;
  - rejeita mais de 2 casas decimais — ver a nota de esclarecimento abaixo;
  - concentra as operações de soma e subtração;
  - compara por valor, não por referência.

`Amount` é sempre **positivo**. O sinal nunca é armazenado: ele é derivado do tipo
(RN-003). Isso elimina a classe inteira de bugs em que um débito é registrado com
valor positivo e passa a somar no lugar de subtrair.

### Tipo: enum `TransactionType` com comportamento

`CREDIT` e `DEBIT`, com o efeito sobre o saldo pertencendo ao próprio tipo:

```
saldo += transaction.Type.ApplyTo(transaction.Amount)
        // CREDIT → +amount
        // DEBIT  → −amount
```

A regra de sinal existe em **um único lugar**. Nada de `if (type == "DEBIT")`
espalhado entre casos de uso, worker e consolidação.

Persistência e serialização usam a **string** (`"CREDIT"` / `"DEBIT"`), não o valor
numérico do enum: reordenar o enum não pode reinterpretar dados já gravados nem
eventos já publicados.

### Arredondamento

Nenhum cálculo do MVP produz fração indivisível — o saldo é soma e subtração de
valores já com 2 casas, e não há juros, rateio ou percentual. Nenhuma política de
arredondamento é exercitada no escopo atual, e por isso nenhuma é declarada: a
única operação de escala que existe em `Money` é a normalização descrita abaixo.

### Nota de esclarecimento — 2026-08-08 (etapa 6)

"Normalizar a escala" acima significa **escala**, não arredondamento. O texto
original era ambíguo o bastante para admitir a leitura de que `10.555` viraria
`10.56`, e essa leitura contradiz
[`api-contracts.md`](../api-contracts.md) §1.4, que promete `400` para mais de duas
casas e afirma explicitamente "não arredondamento silencioso".

A leitura em vigor, implementada em `Money`:

| Entrada | Resultado |
|---------|-----------|
| `10.555` | Exceção de domínio — mais de 2 casas decimais |
| `10.5` | `10.50` — escala elevada, valor inalterado |
| `10.500` | `10.50` — zeros à direita não são casas significativas |
| `0` ou negativo | Exceção de domínio (RN-001) |
| acima de `9999999999999999.99` | Exceção de domínio — faixa de `numeric(18,2)` (ADR-005) |

Não é ADR nova porque não é decisão estrutural: é regra de validação, que
[`decisions/README.md`](./README.md) classifica explicitamente como nota na
documentação existente. A decisão de fundo — `decimal` em Value Object imutável —
permanece intacta.

A rejeição vive no domínio, e não apenas na borda HTTP: o `400` da API passa a ser
eco de uma regra única, em vez de uma segunda validação livre para divergir da
primeira.

### Data do lançamento

`OccurredAt` é `timestamptz` em UTC. O dia da consolidação é derivado da data UTC
de `OccurredAt` (premissa P-04). Limitação conhecida: um lançamento às 22h em
Brasília (UTC−3) cai no dia seguinte em UTC. Conversão para o fuso do lojista está
registrada como melhoria futura — no MVP, a regra fica explícita em vez de
implícita e incorreta.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| `decimal` + Value Object `Money` | Exato, regras centralizadas, imutável | Mapeamento extra no EF Core | **Escolhida** |
| `decimal` puro, sem VO | Menos código | Validação e regra de sinal se espalham; domínio anêmico | Rejeitada |
| `long` em centavos | Sem qualquer dúvida de precisão; aritmética inteira | Conversão em toda fronteira; leitura e depuração piores; ganho nulo frente a `numeric` no Postgres | Rejeitada — era tecnicamente defensável |
| `double` / `float` | — | Impreciso para dinheiro | Rejeitada categoricamente |
| Duas entidades (`Credit`, `Debit`) | Tipagem forte por tipo | Duplica regra e persistência sem ganho real | Rejeitada |
| `string` para o tipo | Flexível | Sem validação em compilação; comparação textual espalhada | Rejeitada |
| Enum persistido como `int` | Compacto | Reordenar o enum corrompe a interpretação de dados históricos | Rejeitada |

## Consequências

**Positivas**

- Impossível construir um lançamento com valor inválido: a regra está no
  construtor, não em uma validação que alguém pode esquecer de chamar.
- A regra de sinal existe uma única vez — SRP aplicado onde mais importa.
- Value Objects imutáveis são triviais de testar e seguros sob concorrência.
- Enum como string mantém eventos e banco legíveis e estáveis.

**Negativas**

- Configuração de conversão de `Money` no EF Core e na serialização JSON.
- Mais tipos para um domínio pequeno — indireção que só se justifica por ser
  justamente o núcleo do risco.
- `decimal` é mais lento que tipos primitivos (irrelevante nesta escala).

## Trade-off aceito

Aceitamos **mais tipos e mais configuração de mapeamento** em troca de tornar
estados inválidos irrepresentáveis. Em domínio financeiro, um valor negativo ou um
sinal invertido não falha ruidosamente: ele produz um saldo errado que parece
correto. Encapsular é a defesa mais barata contra isso.

Aceitamos também a limitação de fuso horário do MVP, documentada de forma explícita.

## Requisitos atendidos

RN-001, RN-002, RN-003, RN-004, RF-001, RF-002, RNF-010

## Como validar

- Teste unitário: criar `Money` negativo lança exceção de domínio.
- Teste unitário: `Money` com mais de 2 casas lança exceção de domínio.
- Teste unitário: `Money` com menos de 2 casas tem a escala normalizada.
- Teste unitário: `DEBIT.ApplyTo(100)` retorna `−100`.
- Teste de propriedade: somar e subtrair valores com centavos não acumula erro.
- Teste de serialização: o evento publicado contém `"type": "CREDIT"`, não `0`.
