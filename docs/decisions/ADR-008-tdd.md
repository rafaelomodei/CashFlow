# ADR-008 — TDD como fluxo de desenvolvimento

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

O desafio exige "criação de rotinas de testes" e "testes automatizados para
atestar o funcionamento correto das lógicas de negócio" (RT-002, RNF-009).

Isso poderia ser cumprido escrevendo os testes **depois** do código. Escolhemos
não fazer assim. Teste escrito depois tende a documentar o comportamento que a
implementação por acaso teve, incluindo seus defeitos, e raramente influencia o
design — que é justamente o que está sendo avaliado neste desafio.

## Decisão

Adotamos **TDD** como fluxo de desenvolvimento, com o ciclo clássico:

```
   ┌──────────────────────────────────┐
   ▼                                  │
 RED ──▶ GREEN ──▶ REFACTOR ──────────┘
 teste    mínimo    melhorar sem
 falha    p/ passar quebrar
```

Regras operacionais:

1. Nenhum código de produção é escrito sem um teste falhando que o exija.
2. Escreve-se apenas o suficiente para o teste falhar (não compilar já é falhar).
3. Escreve-se apenas o suficiente para o teste passar.
4. A refatoração acontece com a suíte verde.
5. O ciclo é uma disciplina de **desenvolvimento**, não de histórico de commits —
   ver abaixo.

### O ciclo não vira coreografia de Git

TDD acontece no editor, em ciclos de minutos. Transformá-lo em obrigação de
commits (`test:` quebrado → `feat:` que conserta) produziria commits vermelhos de
propósito, um histórico que não pode ser bissectado e um pipeline que falha por
construção em metade das revisões.

A regra adotada é outra:

- Commits pequenos e **coerentes**: cada commit compila e mantém a suíte verde.
- Teste e implementação podem ir no mesmo commit quando são uma única mudança
  lógica; podem ir separados quando o teste tem valor próprio (ampliar cenários,
  cobrir um caso de borda já suportado).
- O que precisa ser verificável é a **cobertura das regras por testes** e o verde
  do pipeline, não a prova ritual de que o teste esteve vermelho cinco minutos
  antes.

### Pirâmide de testes adotada

```
        ╱╲          Carga (k6)
       ╱  ╲         50 req/s, perda ≤ 5%
      ╱────╲
     ╱      ╲       Integração
    ╱        ╲      Postgres + RabbitMQ reais (Testcontainers)
   ╱──────────╲
  ╱            ╲    Unitários
 ╱______________╲   Domínio e casos de uso, sem I/O
```

| Nível | Alvo | Ferramenta | Característica |
|-------|------|-----------|----------------|
| Unitário | Entidades, VOs, casos de uso | xUnit + FluentAssertions + NSubstitute | Milissegundos, sem I/O |
| Integração | Repositórios, outbox, consumidor, endpoints | Testcontainers + `WebApplicationFactory` | Infra real, sem mock de banco |
| Arquitetura | Fronteiras de camada | NetArchTest | Impede regressão estrutural |
| Carga | RNF-003 / RNF-004 | k6 | Ver [ADR-010](./ADR-010-performance-validation.md) |

Testes de integração usam **infraestrutura real em container**, não banco em
memória. Banco em memória não reproduz `ON CONFLICT`, `SKIP LOCKED` nem o
comportamento transacional — exatamente os pontos de que dependem
[ADR-004](./ADR-004-transactional-outbox.md) e [ADR-007](./ADR-007-idempotency.md).

### O que é obrigatoriamente coberto

- Todas as regras de negócio do domínio (RN-001 a RN-004).
- Todos os casos de uso, no caminho feliz e nos de erro.
- Atomicidade lançamento + outbox.
- Idempotência do consumidor.
- Convergência do saldo após falha.
- Independência: lançamento funciona com a consolidação fora do ar.

### Cobertura

Cobertura é **indicador, não meta**. Não perseguimos um número; perseguimos que
toda regra de negócio tenha teste. O relatório é gerado (Coverlet) para tornar
lacunas visíveis, e a expectativa é cobertura alta em `Domain` e `Application`
justamente porque essas camadas não têm I/O.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| Testes depois da implementação | Ritmo inicial mais rápido | Testa o que foi feito, não o que era exigido; não influencia o design; viés de confirmação | Rejeitada |
| TDD | Design guiado por uso, regressão barata, especificação executável | Ritmo inicial mais lento; exige disciplina | **Escolhida** |
| BDD com SpecFlow | Linguagem próxima ao negócio | Cerimônia desproporcional para um domínio de duas operações | Rejeitada |
| Apenas testes de integração ponta a ponta | Alta confiança no fluxo real | Lentos, frágeis, feedback ruim para o design | Rejeitada como abordagem única |
| Mock de banco em testes de integração | Rápido | Não valida o que realmente importa (SQL, transação, conflito) | Rejeitada |

## Consequências

**Positivas**

- O design nasce testável: dependências invertidas por necessidade, não por dogma
  — o TDD sustenta na prática o que [ADR-001](./ADR-001-architecture.md) propõe.
- A suíte funciona como especificação executável das regras.
- Refatoração segura, o que importa porque a arquitetura tem várias partes móveis.
- Menos código morto: só se escreve o que um teste exige.

**Negativas**

- Velocidade inicial menor.
- Testes de integração com Testcontainers exigem Docker disponível e são mais lentos.
- Risco de testes acoplados a implementação se mal escritos — mitigado testando
  comportamento observável, não estrutura interna.

## Trade-off aceito

Trocamos **velocidade inicial** por **confiança e qualidade de design**. Em um
desafio técnico avaliado por qualidade de código e testabilidade, essa troca é
claramente favorável. Em um MVP com prazo de mercado, poderia não ser.

## Requisitos atendidos

RT-002, RT-101, RNF-009, RNF-010

## Como validar

- `dotnet test` verde é pré-requisito de qualquer merge, verificado por CI em
  todo Pull Request — não por disciplina declarada.
- Toda regra de negócio (RN-001 a RN-004) tem teste correspondente.
- Testes de domínio rodam sem nenhum container ativo.
