using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class ServicoTomadoTests
    {
        private ConsultaNfseServico _consulta = new ConsultaNfseServico();

        private string Consultar(string tag, string documento, int pagina)
        {
            return _consulta.ConsultarNfseServicoTomado(
                "<ConsultarNfseServicoTomadoEnvio><Consulente><CpfCnpj><" + tag + ">" + documento + "</" + tag + "></CpfCnpj></Consulente>" +
                Massa.Periodo(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)) +
                "<Pagina>" + pagina + "</Pagina></ConsultarNfseServicoTomadoEnvio>");
        }

        private static string RespostaNota21()
        {
            return Exemplo.Resposta("04e-consultar-nfse-por-tomador").Replace("ConsultarNfseResposta", "ConsultarNfseServicoTomadoResposta");
        }

        [TestMethod]
        public void Tomador_ZeroAEsquerda()
        {
            Assert.AreEqual(RespostaNota21(), _consulta.ConsultarNfseServicoTomado(Exemplo.Pedido("05-consultar-nfse-servico-tomado")));
        }

        [TestMethod]
        public void Tomador_PeloElementoTomador()
        {
            string pedido = Exemplo.Pedido("05-consultar-nfse-servico-tomado").Replace("Consulente>", "Tomador>");
            Assert.AreEqual(RespostaNota21(), _consulta.ConsultarNfseServicoTomado(pedido));
        }

        [TestMethod]
        public void Tomador_Cpf()
        {
            Prestador prestador = Massa.CriarPrestador();
            Massa.Enviar(prestador, 1, new List<RpsLote> { Massa.Rps(1, 1000m, "01.01", "09876543229") });

            string resposta = Consultar("Cpf", "09876543229", 1);

            StringAssert.Contains(resposta, "<InscricaoMunicipal>" + prestador.InscricaoMunicipal + "</InscricaoMunicipal>");
            StringAssert.Contains(resposta, "<CpfCnpj><Cpf>9876543229</Cpf></CpfCnpj>");
        }

        [TestMethod]
        public void Tomador_DocumentoInvalido()
        {
            string pedido = Exemplo.Pedido("05-consultar-nfse-servico-tomado").Replace("01234567000195", "01234567000196");
            StringAssert.Contains(_consulta.ConsultarNfseServicoTomado(pedido), "<Codigo>E020</Codigo>");
        }

        [TestMethod]
        public void Tomador_SemDocumento()
        {
            string pedido = "<ConsultarNfseServicoTomadoEnvio>" + Massa.Periodo(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)) +
                            "</ConsultarNfseServicoTomadoEnvio>";
            StringAssert.Contains(_consulta.ConsultarNfseServicoTomado(pedido), "<Codigo>E020</Codigo>");
        }

        [TestMethod]
        public void Periodo_MaiorQue31Dias()
        {
            string pedido = Exemplo.Pedido("05-consultar-nfse-servico-tomado").Replace("2026-10-01", "2026-09-29");
            StringAssert.Contains(_consulta.ConsultarNfseServicoTomado(pedido), "<Codigo>E030</Codigo>");
        }

        [TestMethod]
        public void Tomador_SemResultado()
        {
            string pedido = Exemplo.Pedido("05-consultar-nfse-servico-tomado").Replace("2026-10-01", "2025-01-01").Replace("2026-10-31", "2025-01-31");
            StringAssert.Contains(_consulta.ConsultarNfseServicoTomado(pedido), "<Codigo>E040</Codigo>");
        }

        [TestMethod]
        public void Tomador_Paginacao()
        {
            Prestador prestador = Massa.CriarPrestador();
            string tomador = Massa.Cnpj(prestador.Id + 50000000);
            List<RpsLote> rps = new List<RpsLote>();
            for (int i = 1; i <= 51; i++) rps.Add(Massa.Rps(i, 100m + i, "01.01", tomador));
            Massa.Enviar(prestador, 1, rps);

            string pagina1 = Consultar("Cnpj", tomador, 1);
            string pagina2 = Consultar("Cnpj", tomador, 2);

            Assert.AreEqual(50, Massa.Contar(pagina1, "<CompNfse>"));
            StringAssert.EndsWith(pagina1, "</ListaNfse><ProximaPagina>true</ProximaPagina></ConsultarNfseServicoTomadoResposta>");
            Assert.AreEqual(1, Massa.Contar(pagina2, "<CompNfse>"));
            StringAssert.Contains(pagina2, "<InfNfse><Numero>51</Numero>");
            StringAssert.EndsWith(pagina2, "</ListaNfse><ProximaPagina>false</ProximaPagina></ConsultarNfseServicoTomadoResposta>");
        }
    }
}
