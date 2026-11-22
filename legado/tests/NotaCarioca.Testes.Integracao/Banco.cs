using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotaCarioca.Dados;

namespace NotaCarioca.Testes.Integracao
{
    [TestClass]
    public static class Banco
    {
        private const string Imagem = "gvenzl/oracle-free:23-slim";

        private static IContainer _oracle;

        public static string Raiz { get; private set; }

        [AssemblyInitialize]
        public static async Task Iniciar(TestContext contexto)
        {
            Raiz = LocalizarRaiz();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NFSE_CONNECTION_STRING")))
                return;

            _oracle = new ContainerBuilder(Imagem)
                .WithEnvironment("TZ", "America/Sao_Paulo")
                .WithEnvironment("ORACLE_PASSWORD", "oracle_dev_123")
                .WithEnvironment("APP_USER", "NFSE_OWNER")
                .WithEnvironment("APP_USER_PASSWORD", "nfse_dev_123")
                .WithBindMount(Path.Combine(Raiz, "db"), "/opt/nfse/db", AccessMode.ReadOnly)
                .WithBindMount(Path.Combine(Raiz, "db", "init.sh"), "/container-entrypoint-initdb.d/10_nfse.sh", AccessMode.ReadOnly)
                .WithPortBinding(1521, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("DATABASE IS READY TO USE!"))
                .Build();

            await _oracle.StartAsync();

            OracleHelper.ConnectionString = string.Format(
                "User Id=NFSE_OWNER;Password=nfse_dev_123;Data Source={0}:{1}/FREEPDB1;Min Pool Size=2;Connection Timeout=30",
                _oracle.Hostname, _oracle.GetMappedPublicPort(1521));
        }

        [AssemblyCleanup]
        public static async Task Encerrar()
        {
            if (_oracle != null)
                await _oracle.DisposeAsync();
        }

        private static string LocalizarRaiz()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "db", "init.sh")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException("Raiz do repositorio nao encontrada a partir de " + AppContext.BaseDirectory);
            return dir.FullName;
        }
    }
}
