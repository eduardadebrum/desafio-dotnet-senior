using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dados;
using NotaCarioca.Dominio;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class CH02Tests
    {
        private const string Tomador = "11222333000181";
        private const string Item = "01.01";
        private const string ItemInexistente = "99.99";
        private const int ProcessadoComErro = 3;
        private const int ProcessadoComSucesso = 4;
        private const int ViolacaoDeChaveUnica = 1;

        private static readonly TimeSpan Limite = TimeSpan.FromSeconds(30);

        [TestMethod]
        public void DoisProcessadores_NaoDevemDuplicarUmMesmoLote()
        {
            Prestador prestador = Massa.CriarPrestador();
            string protocolo = Massa.RecepcionarLoteRps(prestador, 1, Lote(1, 200));

            ProcessarComLotesTravados(new[] { protocolo }, 2, 2);

            Assert.AreEqual(ProcessadoComSucesso, Consultas.SituacaoLote(protocolo), Consultas.ErrosLote(protocolo));
            Assert.AreEqual(200, Consultas.QuantidadeNotas(prestador.Id));
            Assert.AreEqual(200L, Consultas.MaiorNumero(prestador.Id));
            Assert.AreEqual(200, Consultas.RpsComNota(prestador.Id));
            Assert.AreEqual(0, Consultas.RpsComMaisDeUmaNota(prestador.Id));
        }

        [TestMethod]
        public void Numeracao_LotesParalelosMesmoPrestador()
        {
            Prestador prestador = Massa.CriarPrestador();
            List<string> protocolos = CriarLotesParaMesmoPrestador(prestador, 4, 100);

            ProcessarComLotesTravados(protocolos, 1, 4);

            foreach (string protocolo in protocolos)
                Assert.AreEqual(ProcessadoComSucesso, Consultas.SituacaoLote(protocolo), Consultas.ErrosLote(protocolo));
            Assert.AreEqual(400, Consultas.QuantidadeNotas(prestador.Id));
            Assert.AreEqual(400L, Consultas.MaiorNumero(prestador.Id));
            Assert.AreEqual(400, Consultas.RpsComNota(prestador.Id));
            Assert.AreEqual(0, Consultas.RpsComMaisDeUmaNota(prestador.Id));
        }

        [TestMethod]
        public void Numeracao_EmissaoSimultanea()
        {
            Prestador prestador = Massa.CriarPrestador();
            long numeroSegundaNota = 0;
            List<Exception> erros = new List<Exception>();

            using (OracleConnection conexaoPrimeiraNota = OracleHelper.AbrirConexao())
            using (OracleTransaction primeiraNota = conexaoPrimeiraNota.BeginTransaction())
            {
                EmitirNota(conexaoPrimeiraNota, primeiraNota, prestador.Id, 1);

                List<Thread> emissaoSegundaNota = Iniciar(1, erros, () =>
                {
                    using (OracleConnection conexaoSegundaNota = OracleHelper.AbrirConexao())
                    using (OracleTransaction segundaNota = conexaoSegundaNota.BeginTransaction())
                    {
                        numeroSegundaNota = EmitirNota(conexaoSegundaNota, segundaNota, prestador.Id, 2);
                        segundaNota.Commit();
                    }
                });
                AguardarBloqueadas(conexaoPrimeiraNota, 1);
                primeiraNota.Commit();
                Aguardar(emissaoSegundaNota, erros);
            }

            Assert.AreEqual(2L, numeroSegundaNota);
            Assert.AreEqual(2, Consultas.QuantidadeNotas(prestador.Id));
        }

        [TestMethod]
        public void Rps_RepetidoEntreVerificacaoEInsercao()
        {
            Prestador prestador = Massa.CriarPrestador();
            RpsRepositorio repositorioRps = new RpsRepositorio();
            OracleException erroSegundoLote = null;

            using (OracleConnection conexaoPrimeiroLote = OracleHelper.AbrirConexao())
            using (OracleConnection conexaoSegundoLote = OracleHelper.AbrirConexao())
            using (OracleTransaction primeiroLote = conexaoPrimeiroLote.BeginTransaction())
            using (OracleTransaction segundoLote = conexaoSegundoLote.BeginTransaction())
            {
                Assert.IsFalse(repositorioRps.Existe(conexaoPrimeiroLote, primeiroLote, prestador.Id, Massa.Serie, 1));
                EmitirNota(conexaoPrimeiroLote, primeiroLote, prestador.Id, 1);
                Assert.IsFalse(repositorioRps.Existe(conexaoSegundoLote, segundoLote, prestador.Id, Massa.Serie, 1));
                primeiroLote.Commit();
                try
                {
                    EmitirNota(conexaoSegundoLote, segundoLote, prestador.Id, 1);
                    segundoLote.Commit();
                }
                catch (OracleException ex)
                {
                    erroSegundoLote = ex;
                }
            }

            Assert.AreEqual(1, Consultas.QuantidadeNotas(prestador.Id));
            Assert.AreEqual(1, Consultas.QuantidadeRps(prestador.Id));
            Assert.IsNotNull(erroSegundoLote);
            Assert.AreEqual(ViolacaoDeChaveUnica, erroSegundoLote.Number, erroSegundoLote.Message);
        }

        [TestMethod]
        public void Retorno_LotesParalelos()
        {
            Prestador prestadorItemInexistente = Massa.CriarPrestador();
            List<RpsLote> loteItemInexistente = Lote(1, 200);
            RpsNumero(loteItemInexistente, 10).ItemListaServico = ItemInexistente;
            RpsNumero(loteItemInexistente, 20).ItemListaServico = ItemInexistente;
            string protocoloItemInexistente = Massa.RecepcionarLoteRps(prestadorItemInexistente, 1, loteItemInexistente);

            Prestador prestadorValorZero = Massa.CriarPrestador();
            List<RpsLote> loteValorZero = Lote(1, 200);
            RpsNumero(loteValorZero, 5).ValorServicos = 0m;
            RpsNumero(loteValorZero, 15).ValorServicos = 0m;
            string protocoloValorZero = Massa.RecepcionarLoteRps(prestadorValorZero, 1, loteValorZero);

            ProcessarComLotesTravados(new[] { protocoloItemInexistente, protocoloValorZero }, 1, 2);

            Assert.AreEqual(ProcessadoComErro, Consultas.SituacaoLote(protocoloItemInexistente));
            Assert.AreEqual(ProcessadoComErro, Consultas.SituacaoLote(protocoloValorZero));
            Assert.AreEqual(198, Consultas.QuantidadeNotas(prestadorItemInexistente.Id));
            Assert.AreEqual(198, Consultas.QuantidadeNotas(prestadorValorZero.Id));
            VerificarErros(protocoloItemInexistente, Erro(10, "E061"), Erro(20, "E061"));
            VerificarErros(protocoloValorZero, Erro(5, "E062"), Erro(15, "E062"));
        }

        [TestMethod]
        public void RpsSemSerie_NaoDeveDuplicarNota()
        {
            Prestador prestador = Massa.CriarPrestador();
            RpsLote rpsSemSerie = Massa.Rps(1, 1000m, Item, Tomador);
            rpsSemSerie.Serie = "";

            Massa.Enviar(prestador, 1, new List<RpsLote> { rpsSemSerie });
            string protocoloReenvio = Massa.Enviar(prestador, 2, new List<RpsLote> { rpsSemSerie });

            Assert.AreEqual(1, Consultas.QuantidadeNotas(prestador.Id));
            StringAssert.Contains(Consultas.ErrosLote(protocoloReenvio), "<Codigo>E060</Codigo>");
        }

        [TestMethod]
        public void Guia_RecalculoSimultaneoDoMesmoPrestador()
        {
            Prestador prestador = Massa.CriarPrestador();
            DateTime competencia = Competencia.PrimeiroDia(DateTime.Today);
            List<Exception> erros = new List<Exception>();

            using (OracleConnection conexaoNota = OracleHelper.AbrirConexao())
            using (OracleTransaction nota = conexaoNota.BeginTransaction())
            {
                EmitirNota(conexaoNota, nota, prestador.Id, 1);
                nota.Commit();
            }

            using (OracleConnection conexaoPrimeiroRecalculo = OracleHelper.AbrirConexao())
            using (OracleTransaction primeiroRecalculo = conexaoPrimeiroRecalculo.BeginTransaction())
            {
                OracleHelper.ExecuteNonQuery(conexaoPrimeiroRecalculo, primeiroRecalculo,
                    "BEGIN PKG_GUIA.recalcular_guia(:prest, :comp); END;",
                    OracleHelper.P("prest", prestador.Id),
                    OracleHelper.P("comp", OracleDbType.Date, competencia));

                List<Thread> segundoRecalculo = Iniciar(1, erros, () => new GuiaRepositorio().Recalcular(prestador.Id, competencia));
                AguardarBloqueadas(conexaoPrimeiroRecalculo, 1);
                primeiroRecalculo.Commit();
                Aguardar(segundoRecalculo, erros);
            }

            Assert.AreEqual(20.00m, Consultas.ValorGuia(prestador.Id, competencia));
        }

        private static List<RpsLote> Lote(int primeiroNumero, int quantidade)
        {
            List<RpsLote> lote = new List<RpsLote>();
            for (int numero = primeiroNumero; numero < primeiroNumero + quantidade; numero++)
                lote.Add(Massa.Rps(numero, 100m + numero, Item, Tomador));
            return lote;
        }

        private static RpsLote RpsNumero(List<RpsLote> lote, long numero)
        {
            return lote.Find(rps => rps.Numero == numero);
        }

        private static List<string> CriarLotesParaMesmoPrestador(Prestador prestador, int quantidadeLotes, int rpsPorLote)
        {
            List<string> protocolos = new List<string>();
            for (int i = 0; i < quantidadeLotes; i++)
                protocolos.Add(Massa.RecepcionarLoteRps(prestador, i + 1, Lote(i * rpsPorLote + 1, rpsPorLote)));
            return protocolos;
        }

        private static string Erro(long numeroRps, string codigo)
        {
            return "<Numero>" + numeroRps + "</Numero><Serie>" + Massa.Serie + "</Serie></IdentificacaoRps><Codigo>" + codigo + "</Codigo>";
        }

        private static void VerificarErros(string protocolo, params string[] errosEsperados)
        {
            string erros = Consultas.ErrosLote(protocolo);
            Assert.AreEqual(errosEsperados.Length, Massa.Contar(erros, "<MensagemRetorno>"), erros);
            foreach (string erro in errosEsperados)
                StringAssert.Contains(erros, erro);
        }

        private static long EmitirNota(OracleConnection conexao, OracleTransaction transacao, long prestadorId, long numeroRps)
        {
            long rpsId = new RpsRepositorio().Inserir(conexao, transacao, prestadorId, numeroRps, Massa.Serie, 1, DateTime.Today,
                                                      "CH02", Tomador, "TOMADOR TESTE");
            long nfseId;
            long numeroNota;
            string codigoVerificacao;
            new NfseRepositorio().GerarNfse(conexao, transacao, rpsId, prestadorId, Competencia.PrimeiroDia(DateTime.Today), Item,
                                            "Servico de teste", 1000m, 0m, 0.02m, 20m, false, Tomador, "TOMADOR TESTE",
                                            out nfseId, out numeroNota, out codigoVerificacao);
            return numeroNota;
        }

        private static void ProcessarComLotesTravados(IEnumerable<string> protocolos, int execucoes, int bloqueiosEsperados)
        {
            using (OracleConnection conexao = OracleHelper.AbrirConexao())
            using (OracleTransaction travaDosLotes = conexao.BeginTransaction())
            {
                foreach (string protocolo in protocolos)
                    OracleHelper.ExecuteScalar(conexao, travaDosLotes, "SELECT PROTOCOLO FROM LOTES_RPS WHERE PROTOCOLO = :prot FOR UPDATE",
                                               OracleHelper.P("prot", protocolo));

                List<Exception> erros = new List<Exception>();
                List<Thread> processadores = Iniciar(execucoes, erros, () => new ProcessadorLoteRps().ProcessarPendentes());
                AguardarBloqueadas(conexao, bloqueiosEsperados);
                travaDosLotes.Rollback();
                Aguardar(processadores, erros);
            }
        }

        private static void AguardarBloqueadas(OracleConnection donaDaTrava, int esperadas)
        {
            int sid = Convert.ToInt32(OracleHelper.ExecuteScalar(donaDaTrava, null, "SELECT SYS_CONTEXT('USERENV', 'SID') FROM DUAL"));
            DateTime limite = DateTime.Now + Limite;
            while (Consultas.SessoesBloqueadasPor(sid) < esperadas)
            {
                if (DateTime.Now > limite)
                    Assert.Fail("concorrencia nao exercitada: esperava " + esperadas + " sessoes bloqueadas");
                Thread.Sleep(50);
            }
        }

        private static List<Thread> Iniciar(int quantidade, List<Exception> erros, Action acao)
        {
            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < quantidade; i++)
            {
                Thread thread = new Thread(() =>
                {
                    try
                    {
                        acao();
                    }
                    catch (Exception ex)
                    {
                        lock (erros)
                            erros.Add(ex);
                    }
                });
                thread.IsBackground = true;
                threads.Add(thread);
                thread.Start();
            }
            return threads;
        }

        private static void Aguardar(List<Thread> threads, List<Exception> erros)
        {
            foreach (Thread thread in threads)
                if (!thread.Join(Limite))
                    Assert.Fail("thread nao terminou em " + Limite.TotalSeconds + " s");
            if (erros.Count > 0)
                Assert.Fail(erros[0].ToString());
        }
    }
}
