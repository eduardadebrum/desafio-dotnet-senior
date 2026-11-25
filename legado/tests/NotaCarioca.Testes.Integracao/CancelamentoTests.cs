using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class CancelamentoTests
    {
        private CancelamentoNfseServico _cancelamento = new CancelamentoNfseServico();

        private void AssertExemplo(string nome)
        {
            Assert.AreEqual(Exemplo.Resposta(nome), _cancelamento.Cancelar(Exemplo.Pedido(nome)));
        }

        [TestMethod]
        public void Cancelar_PrazoExpirado()
        {
            AssertExemplo("06c-cancelar-nfse-prazo-expirado");
        }

        [TestMethod]
        public void Cancelar_NotaSubstituida()
        {
            AssertExemplo("06d-cancelar-nfse-substituida");
        }

        [TestMethod]
        public void Cancelar_NotaInexistente()
        {
            AssertExemplo("06e-cancelar-nfse-inexistente");
        }

        [TestMethod]
        public void Cancelar_GuiaPaga()
        {
            AssertExemplo("06f-cancelar-nfse-guia-paga");
        }

        private static Regex Confirmacao(Prestador prestador, long numero)
        {
            return new Regex(
                "^<CancelarNfseResposta><Cancelamento><Confirmacao><Pedido><InfPedidoCancelamento>" +
                "<IdentificacaoNfse><Numero>" + numero + "</Numero><Cnpj>" + prestador.Documento + "</Cnpj>" +
                "<InscricaoMunicipal>" + prestador.InscricaoMunicipal + "</InscricaoMunicipal></IdentificacaoNfse>" +
                "<CodigoCancelamento>1</CodigoCancelamento></InfPedidoCancelamento></Pedido>" +
                "<DataHora>(\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2})</DataHora></Confirmacao></Cancelamento></CancelarNfseResposta>$");
        }

        [TestMethod]
        public void Cancelar_DentroDoPrazo()
        {
            Prestador prestador = Massa.CriarPrestador();
            Massa.Enviar(prestador, 1, new List<RpsLote> { Massa.Rps(1, 1000m, "01.01", "11222333000181") });

            string resposta = _cancelamento.Cancelar(Massa.PedidoCancelamento(prestador, 1));

            Match m = Confirmacao(prestador, 1).Match(resposta);
            Assert.IsTrue(m.Success, resposta);
            string consulta = new ConsultaNfseServico().ConsultarNfse(
                "<ConsultarNfseEnvio>" + Massa.IdentificacaoPrestador(prestador) +
                Massa.Periodo(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)) + "</ConsultarNfseEnvio>");
            StringAssert.Contains(consulta, "</InfNfse><NfseCancelamento><Confirmacao><DataHora>" + m.Groups[1].Value +
                                            "</DataHora></Confirmacao></NfseCancelamento></Nfse></CompNfse>");
        }

        [TestMethod]
        public void Cancelar_Repetido()
        {
            Prestador prestador = Massa.CriarPrestador();
            Massa.Enviar(prestador, 1, new List<RpsLote> { Massa.Rps(1, 1000m, "01.01", "11222333000181") });

            string primeira = _cancelamento.Cancelar(Massa.PedidoCancelamento(prestador, 1));
            Thread.Sleep(1100);
            string segunda = _cancelamento.Cancelar(Massa.PedidoCancelamento(prestador, 1));

            Assert.IsTrue(Confirmacao(prestador, 1).IsMatch(primeira), primeira);
            Assert.AreEqual(primeira, segunda);
        }

        [TestMethod]
        public void Cancelar_PrestadorInexistente()
        {
            string pedido = Exemplo.Pedido("06e-cancelar-nfse-inexistente").Replace("10000025", "99999999");
            StringAssert.Contains(_cancelamento.Cancelar(pedido), "<Codigo>E010</Codigo>");
        }

        [TestMethod]
        public void Cancelar_XmlInvalido()
        {
            StringAssert.Contains(_cancelamento.Cancelar("<CancelarNfseEnvio><Pedido/></CancelarNfseEnvio>"), "<Codigo>E001</Codigo>");
        }
    }
}
