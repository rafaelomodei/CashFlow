# ADR-010 — Validação de performance com k6

- **Status:** Aceito
- **Data:** 2026-08-08
- **Revisado em:** 2026-08-09 — a escolha da ferramenta permanece; o número de
  cenários obrigatórios cai de quatro para um. Ver [Revisão](#revisão-2026-08-09).
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

Usamos **k6** para transformar RNF-003 e RNF-004 em critério de aceite executável.

A ambiguidade de "chamadas" é resolvida **escolhendo uma interpretação principal e
registrando as demais**, em vez de medir todas. A frase do enunciado é sobre *o
sistema de consolidação*, e é ele que o cenário obrigatório carrega.

### Cenário obrigatório

| Cenário | Alvo | Carga | Critério |
|---------|------|-------|----------|
| Leitura de saldo consolidado | `GET /daily-balances/{date}` | 50 req/s sustentados | erro < 1%, p95 < 100 ms |

### Thresholds

```javascript
thresholds: {
  http_req_failed:   ['rate<0.01'],   // meta interna: < 1%
  http_req_duration: ['p(95)<100'],
  checks:            ['rate>0.99'],
}
```

Os **5% do enunciado são o teto tolerado, não a meta**. Trabalhamos com margem
para que o critério oficial seja atingido com folga em máquinas mais modestas.

### O que é provado sem carga

Perda zero de eventos e independência de falha não são medidas por carga — são
provadas por **teste funcional**, onde a evidência é mais direta e não depende da
máquina de medição:

| Garantia | Onde é provada |
|----------|----------------|
| Lançamento e evento são atômicos | Teste de integração da etapa 8 |
| Broker fora do ar → `201` e evento pendente | Teste de integração da etapa 9 |
| Evento repetido não duplica saldo | Teste de integração da etapa 10 |
| Convergência lançamento → saldo | Teste ponta a ponta da etapa 11 |

Sob sobrecarga, o sistema deve apresentar *maior latência de consolidação*, nunca
*evento descartado* — consequência direta do Outbox
([ADR-004](./ADR-004-transactional-outbox.md)). Um teste de carga não é o
instrumento que demonstra isso melhor.

### Extras

Executados apenas se sobrar tempo, e registrados como extras nos resultados:

| Cenário | Alvo | Carga |
|---------|------|-------|
| Escrita de lançamentos | `POST /transactions` | 50 req/s |
| Ingestão fim a fim | escrita → consolidação | 50 eventos/s |
| Resiliência sob carga | broker derrubado durante o teste | 50 req/s |

### Organização

```
k6/
├── scenarios/
│   └── read-daily-balance.js     obrigatório; extras entram aqui se houver tempo
└── README.md
```

Os resultados (números medidos, máquina utilizada, a distinção entre meta interna
e teto do enunciado, e a interpretação adotada da ambiguidade) são registrados no
README do projeto.

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
- Rodando via container, qualquer avaliador reproduz o resultado.
- Um cenário obrigatório cabe em um script curto, auditável em uma leitura.

**Negativas**

- Mais uma ferramenta e mais um runtime no projeto.
- Resultados dependem da máquina: números absolutos não são comparáveis entre
  ambientes — por isso o hardware é registrado junto dos resultados.
- Medir em Docker local não equivale a medir em produção.
- A interpretação (b) da ambiguidade — 50 eventos/s de ingestão — fica sem número
  medido, apenas com prova funcional de que nada se perde.

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
docker compose run --rm k6 run /scripts/scenarios/read-daily-balance.js
```

O teste falha automaticamente se qualquer threshold for violado.

---

## Revisão (2026-08-09)

**O que mudou:** os quatro cenários viram um obrigatório — a leitura do saldo
consolidado — e três extras. O `p(95)` do bloco de thresholds passa de 200 ms para
100 ms, porque deixou de ser um teto comum a cenários de escrita e de leitura e
passou a valer só para a leitura, que já tinha esse critério.

**Por quê:** os quatro cenários existiam para cobrir todas as leituras possíveis
de uma frase ambígua. Cobrir todas as interpretações de uma ambiguidade não é
rigor; é recusar-se a decidir. O enunciado fala do *sistema de consolidação*, e
medir bem essa interpretação, registrando as outras, diz mais do que quatro
medições rasas.

Junto veio uma correção de instrumento: perda zero de evento e resiliência sob
falha estavam listadas como critério de teste de carga, quando são melhor
demonstradas por teste funcional. Um teste de carga que também precisa provar
convergência vira duas coisas mal feitas em vez de uma bem feita.

**O que não mudou:** k6 como ferramenta, a meta interna de erro < 1%, os 5% como
teto e não como meta, e o registro do hardware junto dos resultados.
