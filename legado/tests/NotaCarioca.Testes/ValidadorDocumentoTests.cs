using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dominio;

namespace NotaCarioca.Testes
{
    [TestClass]
    public class ValidadorDocumentoTests
    {
        [TestMethod]
        public void Cnpj_Valido()
        {
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("11222333000181"));
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("11.222.333/0001-81"));
        }

        [TestMethod]
        public void Cnpj_DigitoErrado()
        {
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("11222333000182"));
        }

        [TestMethod]
        public void Cnpj_TodosIguais()
        {
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("11111111111111"));
        }

        [TestMethod]
        public void Cpf_Valido()
        {
            Assert.IsTrue(ValidadorDocumento.ValidarCpf("52998224725"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpf("52998224726"));
        }

        [TestMethod]
        public void ParaNumero_RemoveMascara()
        {
            Assert.AreEqual(1234567000189L, ValidadorDocumento.ParaNumero("01.234.567/0001-89"));
        }

        [TestMethod]
        public void SomenteDigitos_Nulo()
        {
            Assert.AreEqual("", ValidadorDocumento.SomenteDigitos(null));
        }

        [TestMethod]
        public void CnpjAlfanumerico_ExemploDaReceita()
        {
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("12ABC34501DE35"));
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("12.ABC.345/01DE-35"));
        }

        [TestMethod]
        public void CnpjAlfanumerico_OutrosValidos()
        {
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("ABC12345678923"));
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("A1B2C3D4E5F668"));
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("0000000000ZZ03"));
        }

        [TestMethod]
        public void CnpjAlfanumerico_Minusculo()
        {
            Assert.IsTrue(ValidadorDocumento.ValidarCnpj("12abc34501de35"));
        }

        [TestMethod]
        public void CnpjAlfanumerico_DigitoErrado()
        {
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501DE36"));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501DE45"));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABD34501DE35"));
        }

        [TestMethod]
        public void CnpjAlfanumerico_LetraNoDigitoVerificador()
        {
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501DE3A"));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501DEA5"));
        }

        [TestMethod]
        public void CnpjAlfanumerico_CaractereOuTamanhoInvalido()
        {
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501D@35"));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501D\u00C935"));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501DE3"));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj("12ABC34501DE355"));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj(""));
            Assert.IsFalse(ValidadorDocumento.ValidarCnpj(null));
        }

        [TestMethod]
        public void CpfOuCnpj_AceitaCnpjAlfanumerico()
        {
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("12ABC34501DE35"));
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("12.ABC.345/01DE-35"));
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("ABC12345678923"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj("12ABC34501DE36"));
        }

        [TestMethod]
        public void CpfOuCnpj_NumericosContinuamIguais()
        {
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("11222333000181"));
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("11.222.333/0001-81"));
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("01234567000195"));
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("52998224725"));
            Assert.IsTrue(ValidadorDocumento.ValidarCpfOuCnpj("529.982.247-25"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj("11222333000182"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj("52998224726"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj("11111111111111"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj("00000000000000"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj("123"));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj(""));
            Assert.IsFalse(ValidadorDocumento.ValidarCpfOuCnpj(null));
        }
    }
}
