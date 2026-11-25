using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class CnpjAlfanumericoTests
    {
        private const string Tomador = "12ABC34501DE35";
        private const string TomadorServicoTomado = "A1B2C3D4E5F668";
        private const string PrestadorDocumento = "ABC12345678923";
        private const string Item = "01.01";

        private ConsultaNfseServico _consulta = new ConsultaNfseServico();

        [TestMethod]
        public void Lote_TomadorAlfanumerico_GeraNotaSemMascaraEmMaiusculas()
        {
            Prestador prestador = Massa.CriarPrestador();
            List<RpsLote> rps = new List<RpsLote>
            {
                Massa.Rps(1, 1000m, Item, Tomador),
                Massa.Rps(2, 1000m, Item, "12.abc.345/01de-35")
            };

            string protocolo = Massa.Enviar(prestador, 1, rps);

            Assert.AreEqual(4, Consultas.SituacaoLote(protocolo), Consultas.ErrosLote(protocolo));
            string lote = _consulta.ConsultarLoteRps("<ConsultarLoteRpsEnvio>" + Massa.IdentificacaoPrestador(prestador) +
                                                     "<Protocolo>" + protocolo + "</Protocolo></ConsultarLoteRpsEnvio>");
            Assert.AreEqual(2, Massa.Contar(lote, "<CpfCnpj><Cnpj>" + Tomador + "</Cnpj></CpfCnpj>"));
        }

        [TestMethod]
        public void ConsultarNfse_FiltraPorTomadorAlfanumerico()
        {
            Prestador prestador = Massa.CriarPrestador();
            Massa.Enviar(prestador, 1, new List<RpsLote> { Massa.Rps(1, 1000m, Item, Tomador) });

            string resposta = _consulta.ConsultarNfse(
                "<ConsultarNfseEnvio>" + Massa.IdentificacaoPrestador(prestador) +
                Massa.Periodo(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)) +
                "<Tomador><CpfCnpj><Cnpj>" + Tomador + "</Cnpj></CpfCnpj></Tomador><Pagina>1</Pagina></ConsultarNfseEnvio>");

            StringAssert.Contains(resposta, "<InfNfse><Numero>1</Numero>");
            StringAssert.Contains(resposta, "<CpfCnpj><Cnpj>" + Tomador + "</Cnpj></CpfCnpj>");
        }

        [TestMethod]
        public void ServicoTomado_ConsulenteAlfanumerico()
        {
            Prestador prestador = Massa.CriarPrestador();
            Massa.Enviar(prestador, 1, new List<RpsLote> { Massa.Rps(1, 1000m, Item, TomadorServicoTomado) });

            string resposta = _consulta.ConsultarNfseServicoTomado(
                "<ConsultarNfseServicoTomadoEnvio><Consulente><CpfCnpj><Cnpj>" + TomadorServicoTomado + "</Cnpj></CpfCnpj></Consulente>" +
                Massa.Periodo(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)) +
                "<Pagina>1</Pagina></ConsultarNfseServicoTomadoEnvio>");

            StringAssert.Contains(resposta, "<InscricaoMunicipal>" + prestador.InscricaoMunicipal + "</InscricaoMunicipal>");
        }

        [TestMethod]
        public void Prestador_CnpjAlfanumerico_EmiteConsultaECancela()
        {
            Prestador prestador = Massa.CriarPrestador(PrestadorDocumento);
            string protocolo = Massa.Enviar(prestador, 1, new List<RpsLote> { Massa.Rps(1, 1000m, Item, "11222333000181") });
            Assert.AreEqual(4, Consultas.SituacaoLote(protocolo), Consultas.ErrosLote(protocolo));

            string resposta = new CancelamentoNfseServico().Cancelar(Massa.PedidoCancelamento(prestador, 1));

            StringAssert.Contains(resposta, "<IdentificacaoNfse><Numero>1</Numero><Cnpj>" + PrestadorDocumento + "</Cnpj>");
        }
    }
}
