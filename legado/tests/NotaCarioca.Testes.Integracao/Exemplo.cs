using System.IO;
using System.Xml;

namespace NotaCarioca.Testes.Integracao
{
    public static class Exemplo
    {
        public static string Pedido(string nome)
        {
            return LerConteudo(nome + ".xml", "inputXML");
        }

        public static string Resposta(string nome)
        {
            return LerConteudo(nome + ".resposta.xml", "outputXML");
        }

        private static string LerConteudo(string arquivo, string elemento)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine(Banco.Raiz, "tools", "exemplos", arquivo));
            XmlNodeList nos = doc.GetElementsByTagName(elemento, "http://notacarioca.rio.gov.br/");
            return nos[0].InnerText;
        }
    }
}
