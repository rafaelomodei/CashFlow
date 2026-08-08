# ADR-014 — Paginação por cursor (keyset) na listagem de lançamentos

- **Status:** Aceito
- **Data:** 2026-08-08
- **Decisores:** rafaelomodei

## Contexto

[`requirements.md`](../requirements.md) define RF-003 — consultar os lançamentos
registrados — com "paginação simples e filtro por período" como teto. Falta decidir
**qual** paginação, e a escolha entra no contrato de API
([`api-contracts.md`](../api-contracts.md) §2.3), que é caro de reverter depois de
publicado.

Duas forças definem a decisão:

1. **Volume.** `transactions` é uma tabela que só cresce: lançamentos são
   imutáveis e nunca excluídos (premissa P-05). Em um lojista real ela chega a
   centenas de milhares de linhas em poucos meses.
2. **Padrão de consumo.** O cliente pretendido percorre a lista por **scroll
   infinito**: pede a primeira página e vai pedindo "mais" a partir de onde parou.
   Ele nunca salta para "página 47" e não precisa saber quantas páginas existem.

O frontend em si permanece fora do escopo ([`scope.md`](../scope.md)); o que está
em jogo aqui é não desenhar uma API que o inviabilize.

### Por que `OFFSET` é o candidato natural — e por que ele falha

`?page=3&pageSize=50` é o padrão mais comum, e ele quebra nos dois eixos acima.

**Correção.** A ordenação é `occurred_at DESC` — lançamentos novos entram no
**topo** da lista. Cada inserção durante a navegação empurra todos os itens uma
posição para baixo, e o offset passa a apontar para o lugar errado:

```
t0   página 1 (OFFSET 0)   →  [ A B C ]
t1   chega o lançamento Z  →  [ Z A B C ]
t2   página 2 (OFFSET 3)   →  [ C … ]        ← C aparece duas vezes
```

O item repetido não é um incômodo cosmético em uma lista financeira: ele parece um
lançamento duplicado. Um lançamento **retroativo** produz o defeito simétrico —
um item some da paginação em andamento.

**Custo.** `OFFSET n` exige que o banco produza e descarte `n` linhas antes de
devolver a página. O custo cresce linearmente com a profundidade do scroll: a
página 1 é instantânea, a página 500 varre 25 000 linhas para devolver 50.
É exatamente o oposto do perfil de acesso do scroll infinito, que se aprofunda por
construção.

## Decisão

A listagem usa **paginação por cursor (keyset)** sobre a chave composta
`(occurred_at, id)`, ordenada `DESC`.

```sql
-- primeira página
SELECT * FROM transactions
 WHERE occurred_at >= @start AND occurred_at < @endExclusive
 ORDER BY occurred_at DESC, id DESC
 LIMIT @limit;

-- páginas seguintes
SELECT * FROM transactions
 WHERE occurred_at >= @start AND occurred_at < @endExclusive
   AND (occurred_at, id) < (@cursorOccurredAt, @cursorId)
 ORDER BY occurred_at DESC, id DESC
 LIMIT @limit;
```

Elementos essenciais:

| Elemento | Decisão | Por quê |
|----------|---------|---------|
| Chave do cursor | `(occurred_at, id)` | `occurred_at` não é único; sem desempate a ordem é indefinida e a paginação pula ou repete |
| Comparação | Tupla `(a, b) < (@a, @b)` | Uma única comparação lexicográfica, resolvida pelo índice — não `a < @a OR (a = @a AND b < @b)` |
| Índice | `(occurred_at DESC, id DESC)` | Torna a busca da posição um *index seek*, não um *scan* |
| Formato do cursor | base64url de `{"o","i"}`, **opaco** | Documentado como não interpretável, o formato interno pode mudar sem quebrar cliente |
| `totalCount` | **Ausente** da resposta | `COUNT(*)` sobre o filtro é O(n) e reintroduz o custo que a decisão elimina |
| Fim da coleção | `nextCursor: null` + `hasMore: false` | O cliente para de pedir sem precisar comparar tamanhos de página |
| `limit` | 1 a 200, padrão 50 | Teto impede que um cliente transforme uma página em varredura completa |

O cursor carrega **apenas a posição**. Os filtros são reenviados a cada
requisição — codificá-los no cursor tornaria opaco algo que o cliente precisa
controlar.

### O que torna esta escolha segura aqui

A paginação por keyset tem uma fraqueza conhecida: se as linhas já percorridas
mudarem, a janela percorrida fica inconsistente. **Neste domínio isso não
acontece** — lançamentos são imutáveis e nunca excluídos (P-05). A chave do cursor
é, portanto, estável por construção.

Resta um caso, honestamente registrado: um lançamento **retroativo** inserido
durante uma navegação já em andamento cai em uma posição *anterior* ao cursor e não
aparece naquela sessão de scroll. Ele aparece normalmente na próxima listagem. É
uma limitação intrínseca a qualquer paginação incremental, e o `OFFSET`
a resolveria pior — lá o mesmo caso produz item repetido ou ausente **e** desloca
todo o restante.

## Alternativas consideradas

| Alternativa | Prós | Contras | Veredito |
|-------------|------|---------|----------|
| Cursor keyset em `(occurred_at, id)` | Custo constante por página; estável sob inserção concorrente; casa com scroll infinito | Sem salto para página arbitrária; sem total; exige índice composto | **Escolhida** |
| `OFFSET` / `LIMIT` com `page` e `pageSize` | Trivial de implementar; permite salto e total | Duplica e omite itens sob inserção concorrente; custo cresce com a profundidade | Rejeitada — incorreta no padrão de uso previsto |
| Cursor apenas em `id` (UUID v4) | Cursor menor | UUID v4 é aleatório: não tem ordem temporal, não serve como posição | Rejeitada |
| Cursor com `id` sequencial (`bigint`) | Cursor menor, ordem natural | Exigiria chave sequencial exposta, revelando volume de lançamentos; e a ordenação de negócio é por `occurred_at`, não por ordem de inserção | Rejeitada |
| Cursor assinado ou cifrado | Impede o cliente de forjar posição | Cursor forjado não dá acesso a nada além do que a rota já expõe; criptografia sem ameaça correspondente | Rejeitada |
| Paginação por intervalo de datas apenas | Sem estado nenhum | Um único dia pode conter mais lançamentos que qualquer página razoável | Rejeitada |
| Sem paginação | Nenhuma | Uma resposta cresce sem limite com o histórico | Rejeitada |

## Consequências

**Positivas**

- Custo por página **constante**, independente da profundidade do scroll.
- Nenhum item duplicado ou omitido por inserção concorrente — relevante em uma
  lista que representa dinheiro.
- O contrato fica alinhado ao consumo real (scroll infinito) em vez de a uma
  paginação numerada que nenhum cliente usaria.
- O `limit` máximo impede que a rota vire uma varredura completa da tabela.

**Negativas**

- Não é possível saltar para uma página arbitrária. Aceitável: RF-003 não pede.
- Não há contagem total de registros na resposta.
- Ordenação fixa. Permitir ordenar por outro campo exigiria um cursor por
  ordenação e um índice por ordenação.
- Mais complexo de implementar e de testar que `OFFSET` — o desempate por `id` e a
  codificação do cursor precisam de teste próprio.
- O cliente precisa guardar o cursor entre requisições; ele não é reconstruível a
  partir de um número de página.

## Trade-off aceito

Aceitamos **perder o salto para página arbitrária e a contagem total** em troca de
custo constante e de correção sob concorrência. As duas capacidades perdidas
existem para uma interface de paginação numerada, que não é a prevista; a correção
sob inserção concorrente vale para qualquer cliente e não tem substituto.

Aceitamos também a implementação mais complexa. É complexidade **local** — vive na
consulta e na codificação do cursor, com teste que a cobre — e não vaza para o
domínio nem para a arquitetura.

## Requisitos atendidos

RF-003, RNF-003, RNF-010

## Como validar

- Teste de integração: paginar uma coleção maior que `limit` percorre **todos** os
  registros, sem repetir e sem pular nenhum.
- Teste de concorrência: inserir novos lançamentos entre a página 1 e a página 2
  não produz item repetido nas páginas seguintes.
- Teste de desempate: N lançamentos com `occurred_at` idêntico paginam
  corretamente, em ordem estável, com `limit` menor que N.
- Teste de fim de coleção: a última página devolve `nextCursor: null` e
  `hasMore: false`.
- Teste de contrato: cursor inválido, truncado ou não decodificável retorna `400`.
- Teste de plano de execução: a consulta paginada usa o índice
  `(occurred_at DESC, id DESC)` — sem *sequential scan* na tabela.
