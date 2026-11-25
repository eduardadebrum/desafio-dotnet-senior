# Nota Carioca: sustentação do legado (.NET / Oracle)

Entrega do desafio técnico de Engenheiro(a) de Software Sênior. O enunciado original está em [`DESAFIO.md`](DESAFIO.md).

## Onde está cada entregável

| Entregável | Onde |
| --- | --- |
| Documento por chamado | [`CH-01`](docs/chamados/CH-01.md), [`CH-02`](docs/chamados/CH-02.md), [`CH-04`](docs/chamados/CH-04.md), [`CH-05`](docs/chamados/CH-05.md) |
| Evolução do CNPJ alfanumérico | `legado/src/NotaCarioca.Dominio/ValidadorDocumento.vb`, migration `005`, testes `CnpjAlfanumericoTests` e `ValidadorDocumentoTests` |
| Revisão do PR-118 | [`revisao/REVIEW-PR-118.md`](revisao/REVIEW-PR-118.md) |
| Migrations e rollbacks | `db/migrations/` |
| Scripts de evidência | `db/evidencias/` (como executar está no documento de cada chamado) |
| Scripts de correção de dados | `db/correcoes/` |
| Testes de integração (Oracle real) | `legado/tests/NotaCarioca.Testes.Integracao/` |
| CI | `.github/workflows/ci.yml`, execuções no [GitHub Actions](https://github.com/eduardadebrum/desafio-dotnet-senior/actions) |

## Resumo dos chamados

| Chamado | Causa raiz | Correção | Camada |
| --- | --- | --- | --- |
| CH-01 | ISS da nota calculado em `Double` com arredondamento para o par; a guia recalcula em `NUMBER` | cálculo em `Decimal`, arredondando como o `ROUND` do Oracle | VB.NET |
| CH-02 | cinco problemas de concorrência entre lotes simultâneos: lote reservado sem atomicidade, numeração `MAX + 1` sem lock, RPS sem constraint única, lista de resultados `static`, criação simultânea da guia | reserva atômica do lote, lock por prestador, constraint única com `E060`, lista local, retry no `MERGE` da guia | C#, PL/SQL, DDL |
| CH-03 | não implementado | | |
| CH-04 | parâmetro numérico comparado com coluna de texto (`TO_NUMBER` na coluna), hint `FULL` e `TRUNC` na data | parâmetro como texto, sem hint, filtro direto na data | C# (SQL embutido) |
| CH-05 | trigger com transação autônoma recalcula a guia sem enxergar o cancelamento | compound trigger que recalcula na mesma transação, e script de correção das guias abertas | PL/SQL |

## Como rodar

Pré-requisitos: Docker (o Oracle usa de 2 a 3 GB) e .NET SDK 8. O [`just`](https://github.com/casey/just) é opcional.

```bash
docker compose up -d --build     # Oracle com carga (cerca de 2 milhões de notas) e o host dos web services
curl http://localhost:8080/health
just lote 1000                   # gera e envia um lote com 1.000 RPS
```

O `db/init.sh` cria o schema, carrega os dados e aplica todas as migrations de `db/migrations/` em ordem (os `*_rollback.sql` não são aplicados).

### Testes

| Com `just` | Sem `just` |
| --- | --- |
| `just build` | `dotnet build legado/NotaCarioca.sln` |
| `just test` | `dotnet test legado/tests/NotaCarioca.Testes -f net8.0` |
| `just test-integracao` | `dotnet test legado/tests/NotaCarioca.Testes.Integracao -f net8.0` |
| `just test-integracao-compose` | ver abaixo |

Sem `just`, a integração contra o Oracle do compose fica assim:

```bash
docker compose stop webservices
NFSE_CONNECTION_STRING="User Id=NFSE_OWNER;Password=nfse_dev_123;Data Source=localhost:1521/FREEPDB1" \
  dotnet test legado/tests/NotaCarioca.Testes.Integracao -f net8.0
docker compose start webservices
```

- `test-integracao` sobe um Oracle novo com Testcontainers (alguns minutos na primeira vez). É o mesmo caminho do CI.
- `test-integracao-compose` usa o Oracle do compose, mais rápido no dia a dia. Ele para o host durante os testes, porque o agendador de lotes do host disputaria os lotes com os testes.
- O banco do compose não reaplica migrations sozinho. Ao trocar de commit, aplique a migration nova com `sqlplus` ou recrie o banco com `just down && just up` (`docker compose down -v && docker compose up -d --build`). **A recriação apaga todos os dados locais**, inclusive o que os testes e as reproduções gravaram.
- Alguns testes leem visões `V$` (plano de execução e sessões bloqueadas). O Testcontainers concede o acesso ao subir; no compose, os grants estão em `docs/chamados/CH-02.md` e `docs/chamados/CH-04.md`.

Hoje são 26 testes unitários (mais 1 que já estava ignorado no código original) e 62 de integração.

### O que os testes protegem

| Grupo | Testes | Para quê |
| --- | --- | --- |
| Caracterização (`LoteTests`, `ConsultaLoteTests`, `ConsultaNfseTests`, `ServicoTomadoTests`, `CancelamentoTests`) | 41 | fixam a resposta de cada operação do web service como os ERPs recebem hoje, inclusive os comportamentos estranhos (último dia do período excluído, documento sem zero à esquerda) |
| `CH01Tests`, `CH02Tests`, `CH04Tests`, `CH05Tests` | 17 | cada chamado: os que reproduzem o defeito falham antes da correção e passam depois; os demais mostram que a correção não mudou o resultado |
| `CnpjAlfanumericoTests` | 4 | lote, consultas e cancelamento com CNPJ alfanumérico, para tomador e prestador |
| Unitários | 26 | arredondamento do ISS, validação de CPF e CNPJ (numérico e alfanumérico), competência e formatação |

## Como ler o histórico

Cada chamado tem um commit `test(...)` que reproduz o problema, com o CI vermelho de propósito, seguido dos commits `fix(...)` que deixam o CI verde. Os links das execuções e as exceções a esse padrão estão no documento de cada chamado.

O CI tem três jobs: unitários em Linux (`net8.0`), integração em Linux com Oracle via Testcontainers e unitários em Windows (`net48`). O build compila os dois frameworks, `net48` e `net8.0`, em todos os jobs.

## Ordem em que a fila foi atacada

1. **CI e testes contra Oracle real.** A configuração do CI e dos testes contra Oracle real permitiu reproduzir os problemas e validar as correções.
2. **CH-01.** Envolve dinheiro e ouvidoria, com perguntas diretas da fiscalização. A causa era localizada, e a resposta dependia mais de medir o impacto do que de mudar código.
3. **CH-02.** O mais grave: nota fiscal em duplicidade, com o fechamento do mês chegando.
4. **CH-04.** Desempenho, com a causa comprovada pelo plano de execução e correção pequena, sem mudança no banco.
5. **CH-05.** Cobrança indevida acontecendo todos os dias. A correção é pequena, mas muda o trigger de cancelamento, que passa a recalcular a guia na mesma transação (os riscos estão no documento do chamado).
6. **Testes de caracterização.** Antes do CNPJ, que mexe na recepção, nas consultas e no cancelamento, para mostrar que os clientes atuais não percebem diferença.
7. **CNPJ alfanumérico.** Evolução, não incidente. Ficou depois dos chamados também porque mexe em pontos alterados pelo CH-04.
8. **Revisão do PR-118.** Escrita depois das correções, para poder apontar cada bloqueio com a medição e a alternativa já testada.
9. **Documentação.**

## Decisões que valem para tudo

- **Corrigir na camada onde o defeito está**, com a menor mudança que resolve: sem camadas, interfaces ou bibliotecas novas. A única dependência adicionada é o Testcontainers, no projeto de testes.
- **Nota emitida não muda.** Nenhuma correção ou script altera `NFSE`.
- **Banco:** os scripts `db/01` a `db/06` são a linha de base e não foram alterados. Toda mudança de banco é migration com rollback, pensada para Oracle 19c com o sistema no ar.
- **Código:** tudo compila em `net48` com C# 7.3, no estilo do código existente.

## O que ficou de fora

**CH-03 (lote grande carregado inteiro em memória).** A correção exige mudanças no fluxo de processamento dos lotes, incluindo leitura e gravação incremental do XML. Priorizei os chamados com impacto fiscal e a evolução obrigatória do CNPJ, considerando o tempo necessário para implementar e validar essa solução.

Também ficaram de fora problemas encontrados durante o trabalho que mudam o que os clientes recebem hoje ou merecem chamado próprio:

- último dia do período excluído nas consultas;
- lote parado em "em processamento" depois de um reciclo, e o `E060` indevido quando ele é reprocessado;
- nota emitida depois de a guia ser paga;
- DTD habilitado no XML recebido;
- CNPJ do prestador não conferido com a inscrição municipal.

Os testes de integração rodam em `net8.0`; em `net48` rodam só os unitários (job Windows do CI).

## Com mais tempo

1. **CH-03:** ler e gravar o lote em streaming, sem carregar o XML inteiro em memória, com teste de memória para lotes grandes.
2. Retomada segura de lotes interrompidos, que é o que mais reduziria chamados depois de um reciclo.
3. Limite de lotes simultâneos e agendador sem execuções sobrepostas.
4. Teste de carga do fechamento do mês (vários grandes emissores ao mesmo tempo) como job separado do CI.
5. Repetir em um Oracle 19c de homologação os testes que dependem de comportamento do banco (concorrência do CH-02 e do CH-05, plano do CH-04). O ambiente local é 23ai.
6. Validar a constraint única do RPS em produção (`ENABLE VALIDATE`) depois de tratar os duplicados que existirem.
