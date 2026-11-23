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

        public static string ErrosLote(string protocolo)
        {
            return Convert.ToString(new LoteRepositorio().Obter(protocolo).Rows[0]["ERROS"]);
        }

        public static int QuantidadeNotas(long prestadorId)
        {
            return Convert.ToInt32(OracleHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM NFSE WHERE PRESTADOR_ID = :prest",
                OracleHelper.P("prest", prestadorId)));
        }

        public static int QuantidadeRps(long prestadorId)
        {
            return Convert.ToInt32(OracleHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM RPS WHERE PRESTADOR_ID = :prest",
                OracleHelper.P("prest", prestadorId)));
        }

        public static long MaiorNumero(long prestadorId)
        {
            return Convert.ToInt64(OracleHelper.ExecuteScalar(
                "SELECT NVL(MAX(NUMERO), 0) FROM NFSE WHERE PRESTADOR_ID = :prest",
                OracleHelper.P("prest", prestadorId)));
        }

        public static int RpsComNota(long prestadorId)
        {
            return Convert.ToInt32(OracleHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM (SELECT r.SERIE, r.NUMERO FROM RPS r JOIN NFSE n ON n.RPS_ID = r.ID " +
                "WHERE r.PRESTADOR_ID = :prest GROUP BY r.SERIE, r.NUMERO)",
                OracleHelper.P("prest", prestadorId)));
        }

        public static int SessoesBloqueadasPor(int sid)
        {
            return Convert.ToInt32(OracleHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM V$SESSION WHERE BLOCKING_SESSION = :sid",
                OracleHelper.P("sid", sid)));
        }

        public static int RpsComMaisDeUmaNota(long prestadorId)
        {
            return Convert.ToInt32(OracleHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM (SELECT r.SERIE, r.NUMERO FROM RPS r JOIN NFSE n ON n.RPS_ID = r.ID " +
                "WHERE r.PRESTADOR_ID = :prest GROUP BY r.SERIE, r.NUMERO HAVING COUNT(*) > 1)",
                OracleHelper.P("prest", prestadorId)));
        }
    }
}
