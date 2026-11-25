using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dados;
using NotaCarioca.Dominio;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class CH05Tests
    {
        private const string Tomador = "11222333000181";
        private const string Item = "01.01";

        private static readonly DateTime CompetenciaAtual = Competencia.PrimeiroDia(DateTime.Today);
        private static readonly DateTime CompetenciaAnterior = CompetenciaAtual.AddMonths(-1);
        private static readonly TimeSpan Limite = TimeSpan.FromSeconds(30);

        [TestMethod]
        public void Cancelamento_GuiaDescontaNotaCancelada()
        {
            Prestador prestador = EmitirNotas(CompetenciaAtual, 1000m, 500m);
            Assert.AreEqual(30.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));

            string resposta = new CancelamentoNfseServico().Cancelar(Massa.PedidoCancelamento(prestador, 1));

            StringAssert.Contains(resposta, "<Confirmacao>");
            Assert.AreEqual(10.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));
        }

        [TestMethod]
        public void Cancelamento_UnicaNotaZeraGuia()
        {
            Prestador prestador = EmitirNotas(CompetenciaAtual, 1000m);
            Assert.AreEqual(20.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));

            string resposta = new CancelamentoNfseServico().Cancelar(Massa.PedidoCancelamento(prestador, 1));

            StringAssert.Contains(resposta, "<Confirmacao>");
            Assert.AreEqual(0.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));
        }

        [TestMethod]
        public void Cancelamento_EmMassaAtualizaGuiasDasCompetencias()
        {
            Prestador prestador = Massa.CriarPrestador();
            Massa.Enviar(prestador, 1, new List<RpsLote>
            {
                Rps(1, 1000m, CompetenciaAtual),
                Rps(2, 500m, CompetenciaAtual),
                Rps(3, 2000m, CompetenciaAnterior),
                Rps(4, 300m, CompetenciaAnterior)
            });
            Assert.AreEqual(30.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));
            Assert.AreEqual(46.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAnterior));

            OracleHelper.ExecuteNonQuery(
                "UPDATE NFSE SET SITUACAO = 'C', DT_CANCELAMENTO = SYSDATE WHERE PRESTADOR_ID = :prest AND NUMERO IN (1, 3)",
                OracleHelper.P("prest", prestador.Id));

            Assert.AreEqual(10.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));
            Assert.AreEqual(6.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAnterior));
        }

        [TestMethod]
        public void Cancelamento_DesfeitoMantemGuia()
        {
            Prestador prestador = EmitirNotas(CompetenciaAtual, 1000m, 500m);

            using (OracleConnection conexao = OracleHelper.AbrirConexao())
            using (OracleTransaction transacao = conexao.BeginTransaction())
            {
                OracleHelper.ExecuteNonQuery(conexao, transacao,
                    "UPDATE NFSE SET SITUACAO = 'C', DT_CANCELAMENTO = SYSDATE WHERE PRESTADOR_ID = :prest AND NUMERO = 1",
                    OracleHelper.P("prest", prestador.Id));
                transacao.Rollback();
            }

            Assert.AreEqual(30.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));
        }

        [TestMethod]
        public void Cancelamento_RecalculoSimultaneoNaoVoltaACobrarNotaCancelada()
        {
            Prestador prestador = EmitirNotas(CompetenciaAtual, 1000m, 500m);

            using (OracleConnection conexao = OracleHelper.AbrirConexao())
            using (OracleTransaction cancelamento = conexao.BeginTransaction())
            {
                OracleHelper.ExecuteNonQuery(conexao, cancelamento,
                    "UPDATE NFSE SET SITUACAO = 'C', DT_CANCELAMENTO = SYSDATE WHERE PRESTADOR_ID = :prest AND NUMERO = 1",
                    OracleHelper.P("prest", prestador.Id));

                Task recalculo = Task.Run(() => new GuiaRepositorio().Recalcular(prestador.Id, CompetenciaAtual));
                AguardarBloqueada(conexao);
                cancelamento.Commit();
                Assert.IsTrue(recalculo.Wait(Limite), "recalculo nao terminou");
            }

            Assert.AreEqual(10.00m, Consultas.ValorGuia(prestador.Id, CompetenciaAtual));
        }

        private static void AguardarBloqueada(OracleConnection donaDaTrava)
        {
            int sid = Convert.ToInt32(OracleHelper.ExecuteScalar(donaDaTrava, null,
                "SELECT SYS_CONTEXT('USERENV', 'SID') FROM DUAL"));
            DateTime limite = DateTime.Now + Limite;
            while (Consultas.SessoesBloqueadasPor(sid) < 1)
            {
                if (DateTime.Now > limite)
                    Assert.Fail("concorrencia nao exercitada: o recalculo nao ficou bloqueado pelo cancelamento");
                Thread.Sleep(50);
            }
        }

        private static Prestador EmitirNotas(DateTime competencia, params decimal[] valores)
        {
            Prestador prestador = Massa.CriarPrestador();
            List<RpsLote> lote = new List<RpsLote>();
            for (int i = 0; i < valores.Length; i++)
                lote.Add(Rps(i + 1, valores[i], competencia));
            string protocolo = Massa.Enviar(prestador, 1, lote);
            Assert.AreEqual(4, Consultas.SituacaoLote(protocolo), Consultas.ErrosLote(protocolo));
            return prestador;
        }

        private static RpsLote Rps(long numero, decimal valor, DateTime competencia)
        {
            RpsLote rps = Massa.Rps(numero, valor, Item, Tomador);
            rps.DataEmissao = competencia;
            return rps;
        }
    }
}
