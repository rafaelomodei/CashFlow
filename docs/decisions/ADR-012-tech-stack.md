# ADR-012 — Stack técnica: .NET 10 e ASP.NET Core

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

RT-001 impõe C# como linguagem. Restam decisões sobre versão do runtime, estilo de
API e bibliotecas de apoio — escolhas que afetam diretamente a legibilidade e a
testabilidade avaliadas pelo desafio.

## Decisão

| Item | Escolha | Justificativa |
|------|---------|---------------|
| Runtime | **.NET 10** | Versão **LTS** ativa no início do desenvolvimento; janela de suporte longa e sem migração forçada no meio do projeto |
| Estilo de API | **Controllers** | Fronteira explícita entre HTTP e aplicação, alinhada às camadas de [ADR-001](./ADR-001-architecture.md) |
| Validação | **FluentValidation** na borda | Mantém validação de entrada fora do domínio; regras de negócio permanecem nas entidades |
| ORM | **EF Core 10** + Npgsql | Migrations e `DbContext` como Unit of Work — ver [ADR-005](./ADR-005-database.md) |
| Mensageria | **RabbitMQ.Client** | Cliente oficial, controle explícito de ack, confirms e DLQ — ver [ADR-003](./ADR-003-messaging.md) |
| Workers | **`BackgroundService`** | Hospedagem nativa, sem framework adicional |
| Testes | **xUnit + FluentAssertions + NSubstitute + Testcontainers** | Ver [ADR-008](./ADR-008-tdd.md) |
| Logs | **Serilog** | Ver [ADR-011](./ADR-011-observability.md) |
| Documentação de API | **Swagger / OpenAPI** | Torna as APIs exploráveis sem cliente externo |
| Carga | **k6** | Ver [ADR-010](./ADR-010-performance-validation.md) |

### Decisões deliberadas de **não** usar

| Biblioteca | Motivo da recusa |
|-----------|------------------|
| MediatR | O benefício (desacoplar handler do chamador) não se paga com poucos casos de uso; injetar o caso de uso diretamente é mais legível e mais fácil de testar. Adicionaria indireção sem requisito que a justifique |
| AutoMapper | Mapeamento explícito é mais legível, verificável em tempo de compilação e não esconde custo |
| MassTransit / NServiceBus | Abstraem a mensageria e implementam outbox e retry por nós. Como a mensageria **é** o ponto avaliado do desafio, esconder o mecanismo tiraria justamente o que deve ser demonstrado |
| Minimal APIs | Adequadas e concisas, mas controllers deixam a separação de camadas mais evidente para leitura |

O caso do MassTransit é o mais relevante: é uma escolha excelente em produção
(economiza semanas), e **má** neste contexto, porque o desafio avalia exatamente a
compreensão de outbox, retry e idempotência. Implementar à mão aqui é decisão
consciente sobre o objetivo do projeto, não desconhecimento da alternativa.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| .NET 9 (STS) | Estável e amplamente conhecida | Ciclo STS, já no fim da janela de suporte; escolher hoje significaria nascer em manutenção | Rejeitada |
| .NET 8 (LTS anterior) | Ainda suportada, ecossistema maduro | Janela de suporte menor que a do .NET 10, sem ganho técnico compensatório | Rejeitada |
| Minimal APIs | Menos cerimônia, arquivos menores | Fronteira de camada menos evidente | Rejeitada, mas defensável |
| MediatR como padrão de aplicação | Uniformiza os casos de uso; pipeline behaviors | Indireção sem necessidade neste tamanho | Rejeitada |
| MassTransit | Outbox, retry e idempotência prontos | Esconde o que o desafio quer ver | Rejeitada conscientemente |

## Consequências

**Positivas**

- Stack moderna, amplamente conhecida e fácil de avaliar.
- Poucas dependências: menos superfície para explicar e manter.
- Os mecanismos centrais (outbox, retry, idempotência) ficam visíveis no código.

**Negativas**

- Exige SDK do .NET 10 na máquina de quem avalia (mitigado pelo `global.json` e
  pela execução via Docker, que não depende do SDK local).
- Implementar outbox e retry manualmente custa mais código e mais testes do que
  usar MassTransit.
- Mapeamento manual entre camadas gera código repetitivo.

## Trade-off aceito

Aceitamos **escrever mais código de infraestrutura** para manter os mecanismos
arquiteturais explícitos e auditáveis. Esta é uma escolha de contexto: em um
sistema de produção com prazo, usaríamos MassTransit e registraríamos a decisão
inversa nesta mesma ADR.

## Requisitos atendidos

RT-001, RT-003, RT-004, RT-005, RNF-010

## Como validar

- `dotnet --version` compatível com o `global.json` do repositório.
- O projeto compila e os testes rodam sem dependências além de .NET SDK e Docker.
