using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System;
using System.Threading.Tasks;

namespace RPGSessionManager.Tests
{
    [TestFixture]
    public class NpcTelemetryServiceTests
    {
        private Mock<ILogger<NpcTelemetryService>> _mockLogger;
        private Mock<IUserResolverService> _mockUserResolver;
        private Mock<ISignalRNotificationService> _mockSignalR;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<NpcTelemetryService>>();
            _mockUserResolver = new Mock<IUserResolverService>();
            _mockSignalR = new Mock<ISignalRNotificationService>();

            // Satisfaz o ApplicationDbContext
            _mockUserResolver.Setup(s => s.GetUserId()).Returns("test-user-id");
        }

        private static DbContextOptions<ApplicationDbContext> CreateDbOptions() =>
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("NpcTelemetryTestDb_" + Guid.NewGuid())
                .Options;

        [Test]
        public async Task RecordInteractionAsync_RecordsInteractionAndCalculatesMetrics()
        {
            var options = CreateDbOptions();
            await using var context = new ApplicationDbContext(options, _mockUserResolver.Object);

            // <<< construtor com 3 args
            var service = new NpcTelemetryService(
                _mockLogger.Object,
                context,
                _mockSignalR.Object);

            int sessionId = 1;
            int characterId = 1;
            double responseTimeMs = 150.5;

            await service.RecordInteractionAsync(sessionId, characterId, responseTimeMs);

            var saved = await context.NpcInteractions.FirstOrDefaultAsync();
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.SessionId, Is.EqualTo(sessionId));
            Assert.That(saved.CharacterId, Is.EqualTo(characterId));
            Assert.That(saved.ResponseTimeMs, Is.EqualTo(responseTimeMs));
        }

        [Test]
        public async Task GetNpcMetricsAsync_ReturnsCorrectMetrics()
        {
            var options = CreateDbOptions();
            await using var context = new ApplicationDbContext(options, _mockUserResolver.Object);

            context.NpcInteractions.AddRange(
                new NpcInteraction { SessionId = 1, CharacterId = 1, ResponseTimeMs = 100 },
                new NpcInteraction { SessionId = 1, CharacterId = 1, ResponseTimeMs = 200 }
            );
            await context.SaveChangesAsync();

            // <<< construtor com 3 args
            var service = new NpcTelemetryService(
                _mockLogger.Object,
                context,
                _mockSignalR.Object);

            var metrics = await service.GetNpcMetricsAsync(1, 1);

            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics.TotalInteractions, Is.EqualTo(2));
            Assert.That(metrics.AverageResponseTimeMs, Is.EqualTo(150));
        }

        [Test]
        public async Task GetSystemMetricsAsync_ReturnsCorrectSystemMetrics()
        {
            var options = CreateDbOptions();
            await using var context = new ApplicationDbContext(options, _mockUserResolver.Object);

            context.NpcInteractions.AddRange(
                new NpcInteraction { SessionId = 1, CharacterId = 1, ResponseTimeMs = 100 },
                new NpcInteraction { SessionId = 2, CharacterId = 2, ResponseTimeMs = 200 }
            );
            await context.SaveChangesAsync();

            // <<< construtor com 3 args
            var service = new NpcTelemetryService(
                _mockLogger.Object,
                context,
                _mockSignalR.Object);

            var system = await service.GetSystemMetricsAsync();

            Assert.That(system, Is.Not.Null);
            Assert.That(system.OverallAverageResponseTimeMs, Is.EqualTo(150));
        }
    }
}
