using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class ConsultaNfseTests
    {
        private ConsultaNfseServico _consulta = new ConsultaNfseServico();

        private void AssertExemplo(string nome)
        {
            Assert.AreEqual(Exemplo.Resposta(nome), _consulta.ConsultarNfse(Exemplo.Pedido(nome)));
        }

        [TestMethod]
        public void Periodo_MesInteiro()
        {
            AssertExemplo("04-consultar-nfse");
        }

        [TestMethod]
        public void Periodo_UltimoDiaExcluido()
        {
            AssertExemplo("04b-consultar-nfse-periodo-curto");
        }

        [TestMethod]
        public void Periodo_UmDiaAMais()
        {
            AssertExemplo("04c-consultar-nfse-periodo-curto-mais-um-dia");
        }

        [TestMethod]
        public void Pagina_SemResultado()
        {
            AssertExemplo("04d-consultar-nfse-sem-resultado");
        }

        [TestMethod]
        public void Tomador_ZeroAEsquerda()
        {
            AssertExemplo("04e-consultar-nfse-por-tomador");
        }

        [TestMethod]
        public void Periodo_MaiorQue31Dias()
        {
            string pedido = Exemplo.Pedido("04-consultar-nfse").Replace("2026-10-01", "2026-09-29");
            StringAssert.Contains(_consulta.ConsultarNfse(pedido), "<Codigo>E030</Codigo>");
        }

        [TestMethod]
        public void Periodo_InicialDepoisDaFinal()
        {
            string pedido = Exemplo.Pedido("04-consultar-nfse").Replace("2026-10-01", "2026-11-01");
            StringAssert.Contains(_consulta.ConsultarNfse(pedido), "<Codigo>E030</Codigo>");
        }

        [TestMethod]
        public void Tomador_DocumentoInvalido()
        {
            string pedido = Exemplo.Pedido("04e-consultar-nfse-por-tomador").Replace("01234567000195", "01234567000196");
            StringAssert.Contains(_consulta.ConsultarNfse(pedido), "<Codigo>E020</Codigo>");
        }

        [TestMethod]
        public void Prestador_Inexistente()
        {
            string pedido = Exemplo.Pedido("04-consultar-nfse").Replace("10000001", "99999999");
            StringAssert.Contains(_consulta.ConsultarNfse(pedido), "<Codigo>E010</Codigo>");
        }

        [TestMethod]
        public void Prestador_Inativo()
        {
            string pedido = Exemplo.Pedido("04-consultar-nfse").Replace("10000001", "10000030");
            StringAssert.Contains(_consulta.ConsultarNfse(pedido), "<Codigo>E010</Codigo>");
        }

        [TestMethod]
        public void Xml_Invalido()
        {
            StringAssert.Contains(_consulta.ConsultarNfse("<ConsultarNfseEnvio>"), "<Codigo>E001</Codigo>");
        }

        [TestMethod]
        public void Prestador_Paginacao()
        {
            Prestador prestador = Massa.CriarPrestador();
            List<RpsLote> rps = new List<RpsLote>();
            for (int i = 1; i <= 51; i++) rps.Add(Massa.Rps(i, 100m + i, "01.01", "11222333000181"));
            Massa.Enviar(prestador, 1, rps);

            string pagina1 = Consultar(prestador, 1);
            string pagina2 = Consultar(prestador, 2);
            string pagina3 = Consultar(prestador, 3);

            Assert.AreEqual(50, Massa.Contar(pagina1, "<CompNfse>"));
            StringAssert.StartsWith(pagina1, "<ConsultarNfseResposta><ListaNfse><CompNfse><Nfse><InfNfse><Numero>1</Numero>");
            StringAssert.EndsWith(pagina1, "</ListaNfse><ProximaPagina>true</ProximaPagina></ConsultarNfseResposta>");
            Assert.AreEqual(1, Massa.Contar(pagina2, "<CompNfse>"));
            StringAssert.StartsWith(pagina2, "<ConsultarNfseResposta><ListaNfse><CompNfse><Nfse><InfNfse><Numero>51</Numero>");
            StringAssert.EndsWith(pagina2, "</ListaNfse><ProximaPagina>false</ProximaPagina></ConsultarNfseResposta>");
            StringAssert.Contains(pagina3, "<Codigo>E040</Codigo>");
        }

        [TestMethod]
        public void Prestador_PaginaInvalidaViraPrimeira()
        {
            Assert.AreEqual(Exemplo.Resposta("04-consultar-nfse"),
                            _consulta.ConsultarNfse(Exemplo.Pedido("04-consultar-nfse").Replace("<Pagina>1</Pagina>", "<Pagina>0</Pagina>")));
        }

        private string Consultar(Prestador prestador, int pagina)
        {
            return _consulta.ConsultarNfse("<ConsultarNfseEnvio>" + Massa.IdentificacaoPrestador(prestador) +
                                           Massa.Periodo(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)) +
                                           "<Pagina>" + pagina + "</Pagina></ConsultarNfseEnvio>");
        }
    }
}
