# ADR-010 — Validação de performance com k6

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

O enunciado define um requisito quantitativo:

> "Durante momentos de pico, o sistema de consolidação chega a processar 50
> chamadas por segundo, tolerando uma perda máxima de 5% dessas requisições."

Um requisito numérico só existe de fato se for **verificável**. Escrever no README
"a aplicação suporta 50 req/s" sem evidência é afirmação, não engenharia. A dica do
próprio enunciado ("reflita sobre a estrutura do código para suportar este volume
com resiliência") indica que o critério faz parte da avaliação.

Como registrado em [`requirements.md`](../requirements.md), "chamadas" é ambíguo —
pode ser leitura do saldo ou ingestão de eventos.

## Decisão

Usamos **k6** para transformar RNF-003 e RNF-004 em critério de aceite executável,
cobrindo os três cenários possíveis da ambiguidade.

### Cenários

| # | Cenário | Alvo | Carga | Critério |
|---|---------|------|-------|----------|
| 1 | Escrita de lançamentos | `POST /transactions` | 50 req/s sustentados | erro < 1%, p95 < 200 ms |
| 2 | Leitura de saldo | `GET /daily-balances/{date}` | 50 req/s sustentados | erro < 1%, p95 < 100 ms |
| 3 | Ingestão fim a fim | escrita → consolidação | 50 eventos/s | **0% de perda**, convergência total |
| 4 | Resiliência sob carga | broker derrubado durante o teste | 50 req/s | escrita segue em 100%, nada perdido |

### Thresholds

```javascript
thresholds: {
  http_req_failed:   ['rate<0.01'],   // meta interna: < 1%
  http_req_duration: ['p(95)<200'],
  checks:            ['rate>0.99'],
}
```

Os **5% do enunciado são o teto tolerado, não a meta**. Trabalhamos com margem
para que o critério oficial seja atingido com folga em máquinas mais modestas.

Para o cenário 3, o critério é mais rigoroso: **perda zero de eventos**. Isso não é
otimismo — é consequência direta do Outbox
([ADR-004](./ADR-004-transactional-outbox.md)). Sob sobrecarga, o sistema deve
apresentar *maior latência de consolidação*, nunca *evento descartado*. A validação
compara `Σcréditos − Σdébitos` esperado com o saldo consolidado após a convergência.

O cenário 4 é o que amarra performance a RNF-001: sob pico **e** com a consolidação
fora do ar, a escrita não pode degradar.

### Organização

```
k6/
├── scenarios/
│   ├── write-transactions.js
│   ├── read-daily-balance.js
│   ├── end-to-end-ingestion.js
│   └── resilience-under-load.js
├── lib/
└── README.md
```

Os resultados (números medidos, máquina utilizada e a distinção entre meta interna
e teto do enunciado) são registrados no README do projeto.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| k6 | Scripts em JavaScript, thresholds como critério de aceite, roda em container, saída clara | Ferramenta adicional fora do ecossistema .NET | **Escolhida** |
| NBomber | .NET nativo, integra à solução | Menos difundido; menos ergonômico para thresholds | Rejeitada — era alternativa razoável |
| Apache JMeter | Maduro, completo | Configuração em XML, pesado, ruim para versionar | Rejeitada |
| Artillery | Simples, YAML | Menos controle fino de cenário | Rejeitada |
| `wrk` / `ab` | Trivial de usar | Só medem HTTP bruto; não validam o fluxo fim a fim nem perda de eventos | Rejeitada |
| Nenhum teste de carga | Menos trabalho | Deixaria o requisito central do desafio sem evidência | Rejeitada |

## Consequências

**Positivas**

- RNF-003 e RNF-004 deixam de ser promessa e viram critério verificável.
- Os cenários funcionam como demonstração dos requisitos, não só como medição.
- Rodando via container, qualquer avaliador reproduz o resultado.
- O cenário 4 evidencia a resiliência exigida por RNF-001 sob carga real.

**Negativas**

- Mais uma ferramenta e mais um runtime no projeto.
- Resultados dependem da máquina: números absolutos não são comparáveis entre
  ambientes — por isso o hardware é registrado junto dos resultados.
- Medir em Docker local não equivale a medir em produção.

## Trade-off aceito

Aceitamos **uma ferramenta fora do ecossistema .NET** porque o ganho de clareza é
grande: com k6, o critério de aceite fica declarado no próprio script (`thresholds`)
e o teste falha sozinho quando o requisito não é atendido. Preferimos a evidência
executável à conveniência de manter tudo em C#.

Aceitamos também que os números sejam relativos ao ambiente local. A afirmação que
o projeto sustenta não é "este sistema suporta 50 req/s em qualquer infraestrutura",
mas "o requisito foi medido, nestas condições, com este resultado".

## Requisitos atendidos

RNF-003, RNF-004, RNF-014

## Como validar

```bash
docker compose up -d
docker compose run --rm k6 run /scripts/scenarios/write-transactions.js
```

O teste falha automaticamente se qualquer threshold for violado.
