using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dados;
using NotaCarioca.Dominio;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public class LoteTests
    {
        private const string Tomador = "11222333000181";

        private RecepcaoLoteServico _recepcao = new RecepcaoLoteServico();
        private ConsultaNfseServico _consulta = new ConsultaNfseServico();

        private string ConsultarLote(Prestador prestador, string protocolo)
        {
            return _consulta.ConsultarLoteRps("<ConsultarLoteRpsEnvio>" + Massa.IdentificacaoPrestador(prestador) +
                                              "<Protocolo>" + protocolo + "</Protocolo></ConsultarLoteRpsEnvio>");
        }

        private string ConsultarSituacao(Prestador prestador, string protocolo)
        {
            return _consulta.ConsultarSituacaoLoteRps("<ConsultarSituacaoLoteRpsEnvio>" + Massa.IdentificacaoPrestador(prestador) +
                                                      "<Protocolo>" + protocolo + "</Protocolo></ConsultarSituacaoLoteRpsEnvio>");
        }

        private static string Erro(long numero, string codigo, string mensagem, string correcao)
        {
            return "<MensagemRetorno><IdentificacaoRps><Numero>" + numero + "</Numero><Serie>" + Massa.Serie +
                   "</Serie></IdentificacaoRps><Codigo>" + codigo + "</Codigo><Mensagem>" + mensagem +
                   "</Mensagem><Correcao>" + correcao + "</Correcao></MensagemRetorno>";
        }

        [TestMethod]
        public void Recepcionar_DevolveProtocolo()
        {
            Prestador prestador = Massa.CriarPrestador();
            List<RpsLote> rps = new List<RpsLote> { Massa.Rps(1, 1000m, "01.01", Tomador) };

            string resposta = _recepcao.Recepcionar(Massa.LoteXml(prestador, 7, rps));

            StringAssert.Matches(resposta, new Regex(
                "^<EnviarLoteRpsResposta><NumeroLote>7</NumeroLote><DataRecebimento>\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}</DataRecebimento>" +
                "<Protocolo>NC\\d{14}</Protocolo></EnviarLoteRpsResposta>$"));
            string protocolo = Regex.Match(resposta, "NC\\d{14}").Value;
            Assert.AreEqual("<ConsultarSituacaoLoteRpsResposta><NumeroLote>7</NumeroLote><Situacao>1</Situacao></ConsultarSituacaoLoteRpsResposta>",
                            ConsultarSituacao(prestador, protocolo));
            StringAssert.Contains(ConsultarLote(prestador, protocolo), "<Codigo>E071</Codigo>");

            new ProcessadorLoteRps().ProcessarPendentes();
        }

        [TestMethod]
        public void Recepcionar_PrestadorInexistente()
        {
            Prestador prestador = new Prestador();
            prestador.Documento = "11222333000181";
            prestador.InscricaoMunicipal = "99999999";
            string resposta = _recepcao.Recepcionar(Massa.LoteXml(prestador, 1, new List<RpsLote>()));
            StringAssert.Contains(resposta, "<Codigo>E010</Codigo>");
        }

        [TestMethod]
        public void Recepcionar_PrestadorInativo()
        {
            Prestador prestador = new Prestador();
            prestador.Documento = "11222333000181";
            prestador.InscricaoMunicipal = "10000030";
            string resposta = _recepcao.Recepcionar(Massa.LoteXml(prestador, 1, new List<RpsLote>()));
            StringAssert.Contains(resposta, "<Codigo>E010</Codigo>");
        }

        [TestMethod]
        public void Recepcionar_XmlInvalido()
        {
            StringAssert.Contains(_recepcao.Recepcionar("<EnviarLoteRpsEnvio><LoteRps>"), "<Codigo>E001</Codigo>");
            StringAssert.Contains(_recepcao.Recepcionar("<OutroEnvio/>"), "<Codigo>E001</Codigo>");
        }

        [TestMethod]
        public void Lote_ProcessadoSemErros()
        {
            Prestador prestador = Massa.CriarPrestador();
            RpsLote comDeducao = Massa.Rps(2, 2500m, "17.01", Tomador);
            comDeducao.ValorDeducoes = 500m;
            RpsLote retido = Massa.Rps(3, 1234.56m, "01.03", Tomador);
            retido.IssRetido = true;
            List<RpsLote> rps = new List<RpsLote> { Massa.Rps(1, 1000m, "01.01", Tomador), comDeducao, retido };

            string protocolo = Massa.Enviar(prestador, 1, rps);

            Assert.AreEqual("<ConsultarSituacaoLoteRpsResposta><NumeroLote>1</NumeroLote><Situacao>4</Situacao></ConsultarSituacaoLoteRpsResposta>",
                            ConsultarSituacao(prestador, protocolo));
            string lote = ConsultarLote(prestador, protocolo);
            Assert.AreEqual(3, Massa.Contar(lote, "<CompNfse>"));
            StringAssert.Contains(lote, "<Numero>1</Numero>");
            StringAssert.Contains(lote, "<Numero>2</Numero>");
            StringAssert.Contains(lote, "<Numero>3</Numero>");
            StringAssert.Contains(lote, "<ValorServicos>1000.00</ValorServicos><ValorDeducoes>0.00</ValorDeducoes><BaseCalculo>1000.00</BaseCalculo><Aliquota>0.02</Aliquota><ValorIss>20.00</ValorIss><IssRetido>2</IssRetido>");
            StringAssert.Contains(lote, "<ValorServicos>2500.00</ValorServicos><ValorDeducoes>500.00</ValorDeducoes><BaseCalculo>2000.00</BaseCalculo><Aliquota>0.05</Aliquota><ValorIss>100.00</ValorIss><IssRetido>2</IssRetido>");
            StringAssert.Contains(lote, "<ValorServicos>1234.56</ValorServicos><ValorDeducoes>0.00</ValorDeducoes><BaseCalculo>1234.56</BaseCalculo><Aliquota>0.03</Aliquota><ValorIss>37.04</ValorIss><IssRetido>1</IssRetido>");
            StringAssert.Contains(lote, "<IdentificacaoPrestador><Cnpj>" + prestador.Documento + "</Cnpj><InscricaoMunicipal>" + prestador.InscricaoMunicipal + "</InscricaoMunicipal></IdentificacaoPrestador><RazaoSocial>" + prestador.RazaoSocial + "</RazaoSocial>");
            StringAssert.Contains(lote, "<TomadorServico><IdentificacaoTomador><CpfCnpj><Cnpj>11222333000181</Cnpj></CpfCnpj></IdentificacaoTomador><RazaoSocial>TOMADOR TESTE 1</RazaoSocial></TomadorServico>");
            Assert.IsFalse(lote.Contains("<ListaMensagemRetorno>"));

            DataTable guia = new GuiaRepositorio().ObterGuia(prestador.Id, Competencia.PrimeiroDia(DateTime.Now));
            Assert.AreEqual(1, guia.Rows.Count);
            Assert.AreEqual(120.00m, Convert.ToDecimal(guia.Rows[0]["VALOR_ISS"]));
            Assert.AreEqual("A", Convert.ToString(guia.Rows[0]["SITUACAO"]));
            Assert.AreEqual(Competencia.Vencimento(DateTime.Now), Convert.ToDateTime(guia.Rows[0]["DT_VENCIMENTO"]));
        }

        [TestMethod]
        public void Lote_ComErros()
        {
            Prestador prestador = Massa.CriarPrestador();
            List<RpsLote> primeiro = new List<RpsLote>
            {
                Massa.Rps(1, 1000m, "01.01", Tomador),
                Massa.Rps(2, 1000m, "01.01", "11222333000182"),
                Massa.Rps(3, 1000m, "99.99", Tomador),
                Massa.Rps(4, 0m, "01.01", Tomador)
            };
            List<RpsLote> segundo = new List<RpsLote>
            {
                Massa.Rps(1, 1000m, "01.01", Tomador),
                Massa.Rps(5, 1000m, "01.01", Tomador)
            };

            string protocolo1 = Massa.Enviar(prestador, 1, primeiro);
            string protocolo2 = Massa.Enviar(prestador, 2, segundo);

            StringAssert.Contains(ConsultarSituacao(prestador, protocolo1), "<Situacao>3</Situacao>");
            string lote1 = ConsultarLote(prestador, protocolo1);
            Assert.AreEqual(1, Massa.Contar(lote1, "<CompNfse>"));
            StringAssert.EndsWith(lote1,
                "</ListaNfse><ListaMensagemRetorno>" +
                Erro(2, "E020", "CPF/CNPJ inválido.", "Informe um CPF ou CNPJ válido.") +
                Erro(3, "E061", "Item de serviço inexistente.", "Informe um item da lista de serviços (LC 116/2003).") +
                Erro(4, "E062", "Valor de serviços inválido.", "O valor de serviços deve ser maior que zero e maior ou igual às deduções.") +
                "</ListaMensagemRetorno></ConsultarLoteRpsResposta>");

            StringAssert.Contains(ConsultarSituacao(prestador, protocolo2), "<Situacao>3</Situacao>");
            string lote2 = ConsultarLote(prestador, protocolo2);
            Assert.AreEqual(1, Massa.Contar(lote2, "<CompNfse>"));
            StringAssert.Contains(lote2, "<InfNfse><Numero>2</Numero>");
            StringAssert.EndsWith(lote2,
                "</ListaNfse><ListaMensagemRetorno>" +
                Erro(1, "E060", "RPS já informado.", "Um RPS só pode originar uma NFS-e.") +
                "</ListaMensagemRetorno></ConsultarLoteRpsResposta>");
        }

        [TestMethod]
        public void Lote_TomadorCpf()
        {
            Prestador prestador = Massa.CriarPrestador();
            List<RpsLote> rps = new List<RpsLote>
            {
                Massa.Rps(1, 1000m, "01.01", "52998224725"),
                Massa.Rps(2, 1000m, "01.01", "01234567890")
            };

            string lote = ConsultarLote(prestador, Massa.Enviar(prestador, 1, rps));

            StringAssert.Contains(lote, "<CpfCnpj><Cpf>52998224725</Cpf></CpfCnpj>");
            StringAssert.Contains(lote, "<CpfCnpj><Cpf>1234567890</Cpf></CpfCnpj>");
        }
    }
}
