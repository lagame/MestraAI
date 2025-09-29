// SwaggerSmokeTests.cs (Setup robusto + ambiente Testing + TearDown null-safe)
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using RPGSessionManager.Services;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RPGSessionManager.Tests
{
    [TestFixture]
    public class SwaggerSmokeTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    // Força o ambiente de testes → Program.cs não registra a fila real
                    builder.UseEnvironment("Testing");

                    builder.ConfigureTestServices(services =>
                    {
                        // 1) (Cinturão e suspensório) Remover QUALQUER hosted service
                        //    Evita subir workers reais em ambiente de teste
                        var hosted = services
                            .Where(d => d.ServiceType == typeof(IHostedService))
                            .ToList();
                        foreach (var d in hosted)
                            services.Remove(d);

                        // 2) Remover TODOS os registros de IAiResponseQueue (se algum sobrou)
                        var queuesToRemove = services
                            .Where(d => d.ServiceType == typeof(IAiResponseQueue))
                            .ToList();
                        foreach (var d in queuesToRemove)
                            services.Remove(d);

                        // 3) Registrar um fake simples
                        services.AddSingleton<IAiResponseQueue, FakeAiResponseQueue>();
                    });
                });

            _client = _factory.CreateClient();
        }

        [TearDown]
        public void Teardown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task SwaggerJsonEndpointReturnsSuccessAndCorrectContentType()
        {
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            response.EnsureSuccessStatusCode();
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        }

        [Test]
        public async Task SwaggerUiEndpointReturnsSuccessAndContainsSwaggerUi()
        {
            var response = await _client.GetAsync("/swagger");
            response.EnsureSuccessStatusCode();
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));

            var html = await response.Content.ReadAsStringAsync();
            Assert.That(html, Does.Contain("Swagger UI"));
        }
    }

    /// <summary>
    /// Fake mínimo para satisfazer a interface do app nos testes.
    /// </summary>
    internal sealed class FakeAiResponseQueue : IAiResponseQueue
    {
        public Task QueueAiResponseAsync(AiResponseRequest request) => Task.CompletedTask;
        public Task<int> GetQueueSizeAsync() => Task.FromResult(0);
        public Task<bool> IsHealthyAsync() => Task.FromResult(true);
    }
}
