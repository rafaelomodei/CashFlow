# ADR-001 — Clean Architecture com SOLID como regra estrutural

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

O desafio exige explicitamente "isolamento evidente das atribuições em diferentes
camadas da aplicação (como infraestrutura, aplicação e domínio)" e a aplicação de
Clean Code e SOLID (RT-003, RT-004, RT-006, RNF-010).

Um projeto pequeno como este poderia ser resolvido com controllers acessando o
`DbContext` diretamente. Isso funcionaria, mas tornaria as regras de negócio
inseparáveis do EF Core e do ASP.NET, inviabilizando testes unitários rápidos e,
por consequência, o TDD que adotamos ([ADR-008](./ADR-008-tdd.md)).

## Decisão

Adotamos **Clean Architecture** com quatro camadas por serviço e regra de
dependência apontando sempre para dentro:

```
Api ──▶ Application ──▶ Domain
Infrastructure ──▶ Application ──▶ Domain
```

- `Domain` não referencia nenhum pacote externo de infraestrutura.
- `Application` define **portas** (interfaces) e nunca depende de implementações.
- `Infrastructure` implementa as portas (Dependency Inversion — o "D" de SOLID).
- `Api` é apenas *composition root* e adaptador HTTP; não contém regra de negócio.

A regra de dependência é validada por **testes de arquitetura automatizados**
(NetArchTest ou equivalente), não apenas por convenção.

## Como SOLID se materializa aqui

| Princípio | Materialização concreta |
|-----------|-------------------------|
| SRP | Um caso de uso por classe (`RegisterTransactionUseCase`), não um `TransactionService` com dez métodos |
| OCP | Novos tipos de consumidor/publicador entram por nova implementação de porta, sem alterar o caso de uso |
| LSP | Value Objects imutáveis; sem herança que altere contrato |
| ISP | Portas pequenas e específicas (`IEventPublisher`, `ITransactionRepository`) em vez de um `IRepository` genérico gordo |
| DIP | `Application` depende de abstração; `Infrastructure` fornece a implementação via DI |

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| Camadas tradicionais (Controller → Service → Repository) | Simples, familiar | Domínio anêmico, regra vaza para o service, acoplamento ao ORM | Rejeitada — conflita com RT-006 |
| Vertical Slice Architecture | Baixo acoplamento entre features, pouca cerimônia | Fronteira de camada menos evidente para avaliação; menos aderente ao texto do enunciado | Rejeitada, mas era defensável |
| Hexagonal (Ports & Adapters) | Praticamente equivalente ao escolhido | Diferença é mais de vocabulário que de estrutura | Absorvida — usamos o vocabulário de portas |
| Sem camadas (CRUD direto) | Entrega mais rápida | Viola RT-006 e inviabiliza TDD | Rejeitada |

## Consequências

**Positivas**

- Regras de negócio testáveis sem banco, sem fila e sem HTTP.
- Troca de infraestrutura (Postgres → outro, RabbitMQ → outro) não toca o domínio.
- Fronteiras explícitas facilitam a avaliação do que o desafio pede.

**Negativas**

- Mais projetos, mais arquivos e mais indireção para um sistema pequeno.
- Mapeamentos entre entidade de domínio, modelo de persistência e DTO.
- Risco de over-engineering percebido se as camadas virarem repasse vazio.

## Trade-off aceito

Aceitamos **mais cerimônia e mais código de fronteira** em troca de testabilidade
e de fronteiras verificáveis. Mitigamos o risco de camada-vazia evitando
abstrações que não têm mais de uma implementação real ou justificativa de teste.

## Requisitos atendidos

RT-003, RT-004, RT-006, RNF-009, RNF-010, RNF-011

## Como validar

- Teste de arquitetura falha se `Domain` referenciar `Microsoft.EntityFrameworkCore`.
- Teste de arquitetura falha se `Application` referenciar `Infrastructure`.
- Todo teste de domínio roda sem nenhum container ativo.
