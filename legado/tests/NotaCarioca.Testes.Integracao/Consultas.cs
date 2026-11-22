using System;
using System.Collections.Generic;
using System.Data;
using NotaCarioca.Dados;
using Oracle.ManagedDataAccess.Client;

namespace NotaCarioca.Testes.Integracao
{
    public static class Consultas
    {
        private const string NotasDaGuia =
            "FROM NFSE WHERE PRESTADOR_ID = :prest AND COMPETENCIA = :comp AND SITUACAO = 'N' AND ISS_RETIDO = 'N'";

        public static decimal SomaIssNotas(long prestadorId, DateTime competencia)
        {
            return Convert.ToDecimal(OracleHelper.ExecuteScalar(
                "SELECT NVL(SUM(VALOR_ISS), 0) " + NotasDaGuia,
                OracleHelper.P("prest", prestadorId),
                OracleHelper.P("comp", OracleDbType.Date, competencia)));
        }

        public static Dictionary<decimal, decimal> IssPorValorServico(long prestadorId, DateTime competencia)
        {
            DataTable dt = OracleHelper.ExecuteDataTable(
                "SELECT VALOR_SERVICOS, VALOR_ISS " + NotasDaGuia,
                OracleHelper.P("prest", prestadorId),
                OracleHelper.P("comp", OracleDbType.Date, competencia));

            Dictionary<decimal, decimal> iss = new Dictionary<decimal, decimal>();
            foreach (DataRow r in dt.Rows)
                iss.Add(Convert.ToDecimal(r["VALOR_SERVICOS"]), Convert.ToDecimal(r["VALOR_ISS"]));
            return iss;
        }

        public static decimal ValorGuia(long prestadorId, DateTime competencia)
        {
            DataTable guia = new GuiaRepositorio().ObterGuia(prestadorId, competencia);
            return Convert.ToDecimal(guia.Rows[0]["VALOR_ISS"]);
        }

        public static int SituacaoLote(string protocolo)
        {
            return Convert.ToInt32(new LoteRepositorio().Obter(protocolo).Rows[0]["SITUACAO"]);
        }
    }
}
