using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Servicos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class CH04Tests
    {
        private ConsultaNfseServico _consulta = new ConsultaNfseServico();

        private string Consultar(string documento, DateTime ini, DateTime fim)
        {
            return _consulta.ConsultarNfseServicoTomado(
                "<ConsultarNfseServicoTomadoEnvio><Consulente><CpfCnpj><Cnpj>" + documento + "</Cnpj></CpfCnpj></Consulente>" +
                Massa.Periodo(ini, fim) + "<Pagina>1</Pagina></ConsultarNfseServicoTomadoEnvio>");
        }

        [TestMethod]
        public void ServicoTomado_UsaIndiceDoTomador()
        {
            Consultar("11444777000161", new DateTime(2026, 8, 1), new DateTime(2026, 9, 1));

            string plano = Consultas.PlanoServicoTomado();

            StringAssert.Contains(
                plano,
                "INDEX RANGE SCAN IX_NFSE_TOMADOR_DT");
        }

        [TestMethod]
        public void ServicoTomado_UltimoDiaDoPeriodoContinuaExcluido()
        {
            string documento = "01234567000195";

            string respostaExcluida = Consultar(
                documento,
                new DateTime(2026, 10, 1),
                new DateTime(2026, 10, 4));

            string respostaIncluida = Consultar(
                documento,
                new DateTime(2026, 10, 4),
                new DateTime(2026, 10, 5));

            StringAssert.Contains(
                respostaExcluida, "<Codigo>E040</Codigo>");

            StringAssert.Contains(
                respostaIncluida, "<InfNfse><Numero>21</Numero>");
        }
    }
}
