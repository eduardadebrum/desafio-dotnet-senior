using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dados;
using NotaCarioca.Dominio;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class CH01Tests
    {
        private const string Tomador = "11222333000181";

        [TestMethod]
        public void Guia_IgualSomaDasNotas()
        {
            DateTime competencia = Competencia.PrimeiroDia(DateTime.Today);
            Prestador prestador = EmitirNotasComMeioCentavo();

            decimal valorGuia = Consultas.ValorGuia(prestador.Id, competencia);

            Assert.AreEqual(66.38m, valorGuia);
            Assert.AreEqual(valorGuia, Consultas.SomaIssNotas(prestador.Id, competencia));
        }

        [TestMethod]
        public void Nota_IssMeioCentavoArredondaParaCima()
        {
            Prestador prestador = EmitirNotasComMeioCentavo();

            Dictionary<decimal, decimal> iss = Consultas.IssPorValorServico(prestador.Id, Competencia.PrimeiroDia(DateTime.Today));

            Dictionary<decimal, decimal> esperado = new Dictionary<decimal, decimal>
            {
                { 10.50m, 0.53m },
                { 1234.50m, 37.04m },
                { 250.50m, 12.53m },
                { 375.50m, 11.27m },
                { 100.10m, 5.01m }
            };
            Assert.AreEqual(esperado.Count, iss.Count);

            foreach (KeyValuePair<decimal, decimal> e in esperado)
                Assert.AreEqual(e.Value, iss[e.Key], "servico " + e.Key);
        }

        [TestMethod]
        public void Guia_RecalculaIssPelaBase()
        {
            DateTime competencia = Competencia.PrimeiroDia(DateTime.Today);
            Prestador prestador = Massa.CriarPrestador();
            RpsLote rps = Massa.Rps(1, 1000m, "01.01", Tomador);
            rps.DataEmissao = DateTime.Today;
            Massa.Enviar(prestador, 1, new List<RpsLote> { rps });

            Massa.GravarIss(prestador.Id, 1, 19.99m);
            new GuiaRepositorio().Recalcular(prestador.Id, competencia);

            Assert.AreEqual(19.99m, Consultas.SomaIssNotas(prestador.Id, competencia));
            Assert.AreEqual(20.00m, Consultas.ValorGuia(prestador.Id, competencia));
        }

        private static Prestador EmitirNotasComMeioCentavo()
        {
            Prestador prestador = Massa.CriarPrestador();
            List<RpsLote> rps = new List<RpsLote>
            {
                Massa.Rps(1, 10.50m, "17.01", Tomador),
                Massa.Rps(2, 1234.50m, "01.03", Tomador),
                Massa.Rps(3, 250.50m, "17.01", Tomador),
                Massa.Rps(4, 375.50m, "01.03", Tomador),
                Massa.Rps(5, 100.10m, "17.01", Tomador)
            };
            foreach (RpsLote r in rps)
                r.DataEmissao = DateTime.Today;

            string protocolo = Massa.Enviar(prestador, 1, rps);
            Assert.AreEqual(4, Consultas.SituacaoLote(protocolo), "lote " + protocolo);
            return prestador;
        }
    }
}
