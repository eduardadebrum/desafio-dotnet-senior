using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class ConsultaLoteTests
    {
        private ConsultaNfseServico _consulta = new ConsultaNfseServico();

        [TestMethod]
        public void Situacao_LoteProcessado()
        {
            string nome = "02-consultar-situacao-lote-rps";
            Assert.AreEqual(Exemplo.Resposta(nome), _consulta.ConsultarSituacaoLoteRps(Exemplo.Pedido(nome)));
        }

        [TestMethod]
        public void Lote_Processado()
        {
            string nome = "03-consultar-lote-rps";
            Assert.AreEqual(Exemplo.Resposta(nome), _consulta.ConsultarLoteRps(Exemplo.Pedido(nome)));
        }

        [TestMethod]
        public void Lote_ComErros()
        {
            string nome = "03b-consultar-lote-rps-com-erros";
            Assert.AreEqual(Exemplo.Resposta(nome), _consulta.ConsultarLoteRps(Exemplo.Pedido(nome)));
        }

        [TestMethod]
        public void Lote_EmProcessamento()
        {
            string nome = "03c-consultar-lote-rps-em-processamento";
            Assert.AreEqual(Exemplo.Resposta(nome), _consulta.ConsultarLoteRps(Exemplo.Pedido(nome)));
        }

        [TestMethod]
        public void Situacao_LoteDeOutroPrestador()
        {
            string pedido = Exemplo.Pedido("02-consultar-situacao-lote-rps").Replace("10000001", "10000002");
            StringAssert.Contains(_consulta.ConsultarSituacaoLoteRps(pedido), "<Codigo>E070</Codigo>");
        }
    }
}
