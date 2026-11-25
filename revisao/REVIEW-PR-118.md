# Revisão do PR #118: fix(lotes) ORA-00001 no processamento e lentidão na consulta de serviços tomados

**Parecer: mudanças solicitadas (request changes). Não aprovo o merge como está.**

Rafael, obrigada por assumir esses chamados durante o fechamento. Você identificou pontos importantes (numeração, lista de resultados, consulta do tomador, arredondamento), e algumas mudanças podem ser aproveitadas. Mas, do jeito que está, o PR troca um erro que aparece no log (`ORA-00001`) por um problema fiscal que não gera erro nenhum, e três das correções não mudam o comportamento descrito nos chamados. Abaixo separo o que bloqueia o merge, o que é sugestão e o que dá para aproveitar. Quando cito uma medição, indico onde ela está registrada e como reproduzir.

Apliquei o diff sobre o commit base (`7f823dd`) para revisar os arquivos completos: `git apply --check revisao/PR-118.diff` passa.

## Resumo

| # | Ponto | Tipo |
| --- | --- | --- |
| 1 | Sequence global quebra a numeração sequencial por prestador | bloqueia |
| 2 | Teste alterado passa a aceitar um valor incorreto de ISS | bloqueia |
| 3 | RPS duplicado continua possível, e o retry não resolve | bloqueia |
| 4 | `lock` só no `Add` não resolve o retorno do lote | bloqueia |
| 5 | Hint `INDEX` não muda o plano observado | bloqueia (a parte do CH-04 não corrige o problema) |
| 6 | Migration e rollback não são seguros em produção | bloqueia |
| 7 | `CommandTimeout` de 120 s | sugestão: remover |
| 8 | Três chamados num PR, teste com um lote só | processo |

## Bloqueios

### 1. `SEQ_NFSE_NUMERO` global quebra a regra fiscal de numeração

`db/03_pkg_nfse.sql:47-50`

```sql
        -- numeracao sequencial por prestador, sem buracos (exigencia fiscal)
        SELECT SEQ_NFSE_NUMERO.NEXTVAL
          INTO v_numero
          FROM DUAL;
```

O comentário acima da linha descreve justamente a regra que a linha deixa de cumprir: "numeração da NFS-e sequencial por prestador, sem lacunas". Com uma sequence única para todos os prestadores:

- **A primeira emissão após o deploy pode gerar lacunas na numeração.** A sequence começa em `MAX(NUMERO) + 1` da tabela inteira, então a próxima nota de qualquer prestador pula para depois do maior número já emitido no sistema.
- **As notas seguintes também ficam com lacuna.** Duas notas seguidas do mesmo prestador só terão números consecutivos se nenhum outro prestador emitir nota entre elas.
- **Erros também geram lacuna.** `NEXTVAL` não é desfeito no rollback: se um RPS falhar depois de obter o número, esse número é perdido. Com `CACHE 100`, números em cache também podem ser perdidos, por exemplo em um reinício da instância.

Isso não tem volta: nota emitida é documento fiscal e não pode ser renumerada.

Sobre a motivação ("o `MAX(NUMERO)+1` era caro, varre os números do prestador"): o índice único `UK_NFSE_NUMERO (PRESTADOR_ID, NUMERO)` permite ao Oracle obter o maior número do prestador direto pelo índice, sem ler as notas. O problema do `MAX + 1` não é desempenho, e sim que duas transações podem ler o mesmo `MAX`. A correção é serializar a emissão por prestador, mantendo a regra: `SELECT ... FROM CONTRIBUINTES WHERE ID = :prestador FOR UPDATE` no início do `gerar_nfse`. Foi o que entrou no CH-02 (migration `002_ch02_numeracao_por_prestador.sql`, ver `docs/chamados/CH-02.md`, causa 2). O lock dura apenas a transação de um RPS, e prestadores diferentes continuam processando em paralelo.

### 2. O teste alterado passa a aceitar um valor incorreto

`legado/tests/NotaCarioca.Testes/CalculoIssTests.cs:24`

```diff
-            Assert.AreEqual(3.01m, CalculoIss.CalcularValorIss(150.30m, 0.02m, "S"));
+            Assert.AreEqual(37.03m, CalculoIss.CalcularValorIss(1234.50m, 0.03m, "S"));
```

1234,50 x 0,03 = 37,035, que arredondado para duas casas é **37,04**. É o valor que o `ROUND` do Oracle retorna e o que a guia cobra (`SELECT ROUND(1234.50 * 0.03, 2) FROM DUAL`). É também um dos valores da reclamação do CH-01 (nota 19 do prestador `10000001`, setembro/2026).

`legado/src/NotaCarioca.Dominio/CalculoIss.vb:16-17`: usar `MidpointRounding.AwayFromZero` está correto, mas não é suficiente, porque o cálculo continua em `Double`. Em `Double`, 1234,50 x 0,03 resulta em 37,03499999..., que não é meio centavo, então é arredondado para 37,03 com qualquer regra de desempate. O teste novo passou porque foi ajustado ao resultado do código, e não o contrário.

Medido no CH-01 (`docs/chamados/CH-01.md`): com a mudança deste PR, a diferença entre a guia e as notas do caso reportado cai de 0,05 para 0,02, mas continua existindo.

A correção é fazer o cálculo em `Decimal`:

```vb
Return Math.Round(baseCalculo * aliquota, 2, MidpointRounding.AwayFromZero)
```

O teste `Iss_DuasCasas` original (150,30 x 0,02 = 3,01) deve voltar, porque estava correto. Os 6 testes novos de `CalculoIssArredondamentoTests` são úteis, mas nenhum cobre o caso de meio centavo, que é o caso do chamado.

Além do código, o CH-01 pede outras respostas: a fiscalização quer saber onde está o erro, desde quando acontece e quantos contribuintes foram afetados. Isso está em `docs/chamados/CH-01.md`.

### 3. RPS duplicado continua possível, e o retry não resolve

`legado/src/NotaCarioca.Servicos/ProcessadorLoteRps.cs:135-156` e `:163`

O chamado relata que "um mesmo RPS gerou duas NFS-e". A causa é a falta de garantia de unicidade: `_rps.Existe(...)` seguido de `_rps.Inserir(...)`, sem nada no banco que impeça dois lotes de passarem pelo `Existe` ao mesmo tempo (o índice `IX_RPS_PRESTADOR_NUM` não é único). O `GravarRps` mantém essa mesma lógica.

Tentar de novo não garante a unicidade. Além disso, com a sequence, o `ORA-00001` da numeração deixa de acontecer: dois lotes com o mesmo RPS passam a gerar duas notas com números diferentes, sem nenhum erro. O problema deixa de aparecer no log, mas a duplicidade fiscal continua.

A correção: constraint única em `RPS (PRESTADOR_ID, SERIE, NUMERO)` e tradução da violação para `E060` ("RPS já informado"), que é o erro que o contribuinte deve receber. Isso também entrou no CH-02 (migration `001_ch02_rps_unico.sql`), tratando o caso de série vazia, que o Oracle grava como `NULL`.

### 4. `lock` só no `Add` não resolve "o retorno veio faltando RPS"

`legado/src/NotaCarioca.Servicos/ProcessadorLoteRps.cs:17`, `:71` e `:86-89`

A lista continua `static`, compartilhada por todos os lotes em processamento. O `lock` protege apenas o `Add`, deixando as outras operações sem proteção:

- `_resultados.Clear()` (`:71`) continua sem proteção. Quando o lote B começa, ele apaga os resultados que o lote A já tinha registrado.
- A leitura no final (`:101-110`) percorre a lista inteira, com resultados de outros lotes, e grava isso no `ERROS` do lote.

O `lock` evita que a `List<T>` seja corrompida, mas o problema do chamado é de escopo, não de sincronização: cada lote precisa da sua própria lista. A correção é usar uma variável local em `ProcessarLote`, sem `lock` (CH-02, causa 4).

### 5. O hint `INDEX` não muda o plano observado

`legado/src/NotaCarioca.Dados/NfseRepositorio.cs:150-151` e `:160`

`TOMADOR_DOCUMENTO` é `VARCHAR2(14)` e o `:doc` continua sendo passado como `Int64` (`:160`, que o PR não altera). No plano observado, o Oracle converte a coluna (`TO_NUMBER("N"."TOMADOR_DOCUMENTO") = :DOC`) e mantém o `TABLE ACCESS FULL` mesmo com o hint: com a função aplicada na coluna, o índice `IX_NFSE_TOMADOR_DT` não atende esse filtro.

Medido na base local com `db/evidencias/CH-04.sql` (tomador `11444777000161`, agosto/2026, estatísticas reais de execução; detalhes em `docs/chamados/CH-04.md`):

| Variante | Acesso à `NFSE` | Blocos lidos | Tempo |
| --- | --- | --- | --- |
| atual (`FULL`) | `TABLE ACCESS FULL` | 167 mil | 0,59 s |
| este PR (`INDEX`) | `TABLE ACCESS FULL` | 167 mil | 0,62 s |
| parâmetro como texto, sem hint, sem `TRUNC` na data | `INDEX RANGE SCAN IX_NFSE_TOMADOR_DT` | 944 | 0,01 s |

Para reproduzir:

```bash
docker compose exec oracle sqlplus -s system/oracle_dev_123@//localhost:1521/FREEPDB1 \
  @/opt/nfse/db/evidencias/CH-04.sql 11444777000161 2026-08-01 2026-09-01
```

A aprovação do DBA não substitui o plano de execução: vale anexar ao PR a saída do `DBMS_XPLAN.DISPLAY_CURSOR` da consulta executada. A correção está em `docs/chamados/CH-04.md` e não precisa de hint: com o parâmetro no mesmo tipo da coluna e sem função aplicada nas colunas, o plano passa a usar o índice.

### 6. Migration e rollback

`db/migrations/V001__seq_numero_nfse.sql`, `db/migrations/V001__seq_numero_nfse_rollback.sql`, `db/03_pkg_nfse.sql`

Mesmo sem considerar o bloqueio 1, a forma de aplicar a mudança no banco não é segura:

- **O package foi alterado no script de criação, e não em uma migration.** `db/03_pkg_nfse.sql` é a linha de base. Em produção, nada aplica a mudança do `PKG_NFSE`: a migration cria a sequence, mas o package continua o antigo, a menos que alguém execute o `03` manualmente.
- **O rollback faz com que o package deixe de funcionar.** Ele só executa `DROP SEQUENCE`. Se o package novo estiver em produção, ele fica inválido e toda emissão de nota falha.
- **O rollback não desfaz o efeito.** As notas emitidas com números da sequence continuam no banco. Ao voltar para o `MAX + 1`, a numeração do prestador continua a partir do número alto.
- **`SELECT MAX(NUMERO) FROM NFSE`** sem filtro de prestador precisa ler o índice inteiro, em uma tabela grande, durante o deploy.

## Sugestão

### 7. `CommandTimeout` de 120 s

`legado/src/NotaCarioca.Dados/OracleHelper.cs:35-41` e `:61-76`, `NfseRepositorio.cs:159`

Configurar um timeout de 120 segundos não resolve a causa da lentidão, que é a consulta ler a `NFSE` inteira (bloqueio 5). Aumentar o limite não deixa a consulta mais rápida, só mantém a sessão do banco ocupada por mais tempo nos horários de pico. Com a consulta usando o índice, o limite deixa de ser necessário, assim como as duas novas sobrecargas de `ExecuteDataTable` e o parâmetro extra no `CriarComando`.

Sugiro remover. Se quisermos um limite de tempo nas consultas, essa decisão deve valer para todas e ser feita em outro PR.

## Processo

### 8. Escopo e teste

- **Três chamados no mesmo PR** (CH-02, CH-04 e CH-01 "aproveitando"). Se a numeração der problema na quinta, o revert desfaz junto o arredondamento e a consulta. Um PR por chamado, cada um com seu teste, deixa o histórico e o rollback mais simples.
- **"Testei com um lote de 1000 RPS na minha máquina, sem nenhum erro"** não reproduz o problema. O `ORA-00001` e a nota duplicada acontecem com lotes simultâneos do mesmo prestador, e o próprio chamado diz que, com lote pequeno, ninguém conseguiu reproduzir. Os testes do CH-02 (`CH02Tests`) forçam essa concorrência mantendo um lock no banco até as sessões estarem realmente esperando, e falham no código antigo.
- **"Nenhuma mudança de contrato nos web services"**: a numeração das notas muda para todos os contribuintes. Para um ERP que confere a sequência, isso é uma mudança de contrato.
- Faltam testes de integração com Oracle para as mudanças de banco. Teste unitário passando em `just test` não cobre package, constraint nem plano de execução.

## O que eu aproveitaria

- **Log com a exceção completa** (`ProcessadorLoteRps.cs:43`, `ex` no lugar de `ex.Message`): ajuda no suporte e pode entrar já.
- **Prestador na mensagem de "processando lote"** (`:69`): mesma coisa.
- **Extração do `GravarRps`**: melhora a leitura, desde que o retry seja removido.
- **`MidpointRounding.AwayFromZero`**: é a regra de desempate correta e deve ficar, desde que o cálculo passe a ser feito em `Decimal` (bloqueio 2).
- **Os 6 testes de `CalculoIssArredondamentoTests`**: cobrem MEI, dedução e alíquota zero, que não tinham teste.

## Proposta

O fechamento é na semana que vem, e concordo que não dá para esperar. As correções dos três chamados já estão prontas, cada uma com um teste que falha antes e passa depois:

| Chamado | Correção | Documento |
| --- | --- | --- |
| CH-02 | lote reservado de forma atômica, lista de resultados local, RPS único com `E060`, numeração serializada por prestador, recálculo da guia tolerante a criação simultânea | `docs/chamados/CH-02.md` |
| CH-01 | ISS em `Decimal` com o mesmo arredondamento do banco | `docs/chamados/CH-01.md` |
| CH-04 | parâmetro como texto, sem hint, sem `TRUNC` | `docs/chamados/CH-04.md` |

Minha sugestão: fechar este PR, abrir um PR pequeno só com as melhorias de log e revisarmos juntos os PRs das correções acima antes da release de quinta. Se quiser, posso passar com você pelos testes de concorrência do CH-02, que são a parte menos óbvia.
