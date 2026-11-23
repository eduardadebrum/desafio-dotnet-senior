using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NotaCarioca.Dados;
using NotaCarioca.Servicos;
using NotaCarioca.Servicos.Modelos;

namespace NotaCarioca.Testes.Integracao
{
    public static class Massa
    {
        public const string Serie = "T1";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static Prestador CriarPrestador()
        {
            long id = Convert.ToInt64(OracleHelper.ExecuteScalar("SELECT SEQ_CONTRIBUINTES.NEXTVAL FROM DUAL"));
            Prestador p = new Prestador();
            p.Id = id;
            p.InscricaoMunicipal = "9" + (id % 10000000).ToString("D7", Inv);
            p.Documento = Cnpj(id);
            p.RazaoSocial = "PRESTADORA TESTE " + id;
            p.Regime = "N";
            p.Ativo = true;
            OracleHelper.ExecuteNonQuery(
                "INSERT INTO CONTRIBUINTES (ID, INSCRICAO_MUNICIPAL, DOCUMENTO, RAZAO_SOCIAL, REGIME, ATIVO) " +
                "VALUES (:id, :im, :doc, :razao, 'N', 'S')",
                OracleHelper.P("id", p.Id),
                OracleHelper.P("im", p.InscricaoMunicipal),
                OracleHelper.P("doc", p.Documento),
                OracleHelper.P("razao", p.RazaoSocial));
            return p;
        }

        public static void GravarIss(long prestadorId, long numero, decimal valorIss)
        {
            OracleHelper.ExecuteNonQuery(
                "UPDATE NFSE SET VALOR_ISS = :iss WHERE PRESTADOR_ID = :prest AND NUMERO = :num",
                OracleHelper.P("iss", valorIss),
                OracleHelper.P("prest", prestadorId),
                OracleHelper.P("num", numero));
        }

        public static string Cnpj(long semente)
        {
            string base12 = (10000000 + semente % 89999999).ToString(Inv) + "0001";
            int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int s = 0;
            for (int i = 0; i < 12; i++) s += (base12[i] - '0') * p1[i];
            int d1 = s % 11 < 2 ? 0 : 11 - s % 11;
            s = 0;
            for (int i = 0; i < 12; i++) s += (base12[i] - '0') * p2[i];
            s += d1 * p2[12];
            int d2 = s % 11 < 2 ? 0 : 11 - s % 11;
            return base12 + d1 + d2;
        }

        public static RpsLote Rps(long numero, decimal valor, string item, string tomador)
        {
            RpsLote r = new RpsLote();
            r.Numero = numero;
            r.Serie = Serie;
            r.Tipo = 1;
            r.DataEmissao = DateTime.Now;
            r.ValorServicos = valor;
            r.ValorDeducoes = 0m;
            r.IssRetido = false;
            r.ItemListaServico = item;
            r.Discriminacao = "Servico de teste " + numero;
            r.TomadorDocumento = tomador;
            r.TomadorRazaoSocial = "TOMADOR TESTE " + numero;
            return r;
        }

        public static string LoteXml(Prestador prestador, long numeroLote, IList<RpsLote> rps)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<EnviarLoteRpsEnvio><LoteRps Id=\"lote").Append(numeroLote).Append("\">");
            sb.Append("<NumeroLote>").Append(numeroLote).Append("</NumeroLote>");
            sb.Append("<Prestador><Cnpj>").Append(prestador.Documento).Append("</Cnpj><InscricaoMunicipal>")
              .Append(prestador.InscricaoMunicipal).Append("</InscricaoMunicipal></Prestador>");
            sb.Append("<QuantidadeRps>").Append(rps.Count).Append("</QuantidadeRps><ListaRps>");
            foreach (RpsLote r in rps)
            {
                sb.Append("<Rps><InfRps Id=\"rps").Append(r.Numero).Append("\">");
                sb.Append("<IdentificacaoRps><Numero>").Append(r.Numero).Append("</Numero><Serie>").Append(r.Serie)
                  .Append("</Serie><Tipo>").Append(r.Tipo).Append("</Tipo></IdentificacaoRps>");
                sb.Append("<DataEmissao>").Append(r.DataEmissao.ToString("yyyy-MM-dd'T'HH:mm:ss", Inv)).Append("</DataEmissao>");
                sb.Append("<Servico><Valores><ValorServicos>").Append(r.ValorServicos.ToString("0.00", Inv))
                  .Append("</ValorServicos><ValorDeducoes>").Append(r.ValorDeducoes.ToString("0.00", Inv))
                  .Append("</ValorDeducoes><IssRetido>").Append(r.IssRetido ? "1" : "2").Append("</IssRetido></Valores>");
                sb.Append("<ItemListaServico>").Append(r.ItemListaServico).Append("</ItemListaServico>");
                sb.Append("<Discriminacao>").Append(XmlNfseBuilder.Esc(r.Discriminacao)).Append("</Discriminacao></Servico>");
                string tag = r.TomadorDocumento.Length == 11 ? "Cpf" : "Cnpj";
                sb.Append("<Tomador><IdentificacaoTomador><CpfCnpj><").Append(tag).Append(">").Append(r.TomadorDocumento)
                  .Append("</").Append(tag).Append("></CpfCnpj></IdentificacaoTomador>");
                sb.Append("<RazaoSocial>").Append(XmlNfseBuilder.Esc(r.TomadorRazaoSocial)).Append("</RazaoSocial></Tomador>");
                sb.Append("</InfRps></Rps>");
            }
            sb.Append("</ListaRps></LoteRps></EnviarLoteRpsEnvio>");
            return sb.ToString();
        }

        public static string RecepcionarLoteRps(Prestador prestador, long numeroLote, IList<RpsLote> rps)
        {
            string resposta = new RecepcaoLoteServico().Recepcionar(LoteXml(prestador, numeroLote, rps));
            string protocolo = Regex.Match(resposta, "<Protocolo>(NC[0-9]{14})</Protocolo>").Groups[1].Value;
            if (protocolo.Length == 0)
                throw new InvalidOperationException("Lote recusado: " + resposta);
            return protocolo;
        }

        public static string Enviar(Prestador prestador, long numeroLote, IList<RpsLote> rps)
        {
            string protocolo = RecepcionarLoteRps(prestador, numeroLote, rps);
            new ProcessadorLoteRps().ProcessarPendentes();
            return protocolo;
        }

        public static string Periodo(DateTime ini, DateTime fim)
        {
            return "<PeriodoEmissao><DataInicial>" + ini.ToString("yyyy-MM-dd", Inv) + "</DataInicial><DataFinal>" +
                   fim.ToString("yyyy-MM-dd", Inv) + "</DataFinal></PeriodoEmissao>";
        }

        public static string IdentificacaoPrestador(Prestador prestador)
        {
            return "<Prestador><Cnpj>" + prestador.Documento + "</Cnpj><InscricaoMunicipal>" + prestador.InscricaoMunicipal +
                   "</InscricaoMunicipal></Prestador>";
        }

        public static int Contar(string texto, string trecho)
        {
            return Regex.Matches(texto, Regex.Escape(trecho)).Count;
        }
    }
}
