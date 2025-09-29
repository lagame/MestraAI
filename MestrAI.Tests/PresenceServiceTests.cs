using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RPGSessionManager.Data;
using RPGSessionManager.Services;
using System.Threading.Tasks;

namespace RPGSessionManager.Tests
{
    [TestFixture]
    public class PresenceServiceTests
    {
        private InMemoryPresenceService _presenceService;
        private DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private Mock<IUserResolverService> _mockUserResolverService;
        private ApplicationDbContext _context;

        [SetUp]
        public void Setup()
        {
            _mockUserResolverService = new Mock<IUserResolverService>();

            _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "PresenceTestDb_" + System.Guid.NewGuid())
                .Options;

            _context = new ApplicationDbContext(_dbContextOptions, _mockUserResolverService.Object);
            _presenceService = new InMemoryPresenceService(_context);
        }

        [TearDown]
        public void Teardown()
        {
            // Garante que o contexto do banco de dados seja descartado após cada teste
            _context.Dispose();
        }

        [Test]
        public async Task UserJoinedAsync_AddsUserAndConnection()
        {
            // Arrange
            int sessionId = 1;
            string userId = "user1";
            string connectionId = "conn1";
            string displayName = "Ronassic";

            // Act
            await _presenceService.UserJoinedAsync(sessionId, userId, displayName, connectionId);

            // Assert
            var participants = await _presenceService.GetParticipantsAsync(sessionId);

            // ASSERÇÃO CORRIGIDA: Verifica as propriedades do objeto anônimo
            Assert.That(participants, Has.Some.Matches<object>(p =>
                p.GetType().GetProperty("UserId")?.GetValue(p)?.ToString() == userId &&
                p.GetType().GetProperty("DisplayName")?.GetValue(p)?.ToString() == displayName
            ));
        }

        [Test]
        public async Task UserLeftAsync_RemovesUserConnection()
        {
            // Arrange
            int sessionId = 1;
            string userId = "user1";
            string connectionId = "conn1";
            string displayName = "Ronassic";
            await _presenceService.UserJoinedAsync(sessionId, userId, displayName, connectionId);

            // Act
            await _presenceService.UserLeftAsync(connectionId);

            // Assert
            var participants = await _presenceService.GetParticipantsAsync(sessionId);

            // ASSERÇÃO CORRIGIDA: Verifica que não há mais nenhum participante com aquele UserId
            Assert.That(participants, Has.None.Matches<object>(p =>
                 p.GetType().GetProperty("UserId")?.GetValue(p)?.ToString() == userId
            ));
        }

        [Test]
        public async Task UserDisconnectedAsync_RemovesAllUserConnectionsAndReturnsAffectedSessions()
        {
            // Arrange
            int sessionId1 = 1;
            int sessionId2 = 2;
            string userId = "user1";
            string connectionId1 = "conn1";
            string connectionId2 = "conn2";
            string displayName = "Ronassic";
            await _presenceService.UserJoinedAsync(sessionId1, userId, displayName, connectionId1);
            await _presenceService.UserJoinedAsync(sessionId2, userId, displayName, connectionId2);

            // Act
            var affectedSessions = await _presenceService.UserDisconnectedAsync(userId);

            // Assert
            Assert.That(affectedSessions, Does.Contain(sessionId1));
            Assert.That(affectedSessions, Does.Contain(sessionId2));

            var participants1 = await _presenceService.GetParticipantsAsync(sessionId1);
            var participants2 = await _presenceService.GetParticipantsAsync(sessionId2);

            // ASSERÇÃO CORRIGIDA: Verifica em ambas as sessões
            Assert.That(participants1, Has.None.Matches<object>(p => p.GetType().GetProperty("UserId")?.GetValue(p)?.ToString() == userId));
            Assert.That(participants2, Has.None.Matches<object>(p => p.GetType().GetProperty("UserId")?.GetValue(p)?.ToString() == userId));
        }

        [Test]
        public async Task GetParticipantsAsync_ReturnsCorrectParticipants()
        {
            // Arrange
            int sessionId = 1;
            string userId1 = "user1";
            string connectionId1 = "conn1";
            string userId2 = "user2";
            string connectionId2 = "conn2";
            string displayName = "Ronassic";
            await _presenceService.UserJoinedAsync(sessionId, userId1, displayName, connectionId1);
            await _presenceService.UserJoinedAsync(sessionId, userId2, displayName, connectionId2);

            // Act
            var participants = await _presenceService.GetParticipantsAsync(sessionId);

            // Assert
            Assert.That(participants.Count, Is.EqualTo(2));

            // ASSERÇÃO CORRIGIDA: Verifica a existência de cada participante
            Assert.That(participants, Has.Some.Matches<object>(p => p.GetType().GetProperty("UserId")?.GetValue(p)?.ToString() == userId1));
            Assert.That(participants, Has.Some.Matches<object>(p => p.GetType().GetProperty("UserId")?.GetValue(p)?.ToString() == userId2));
        }
    }
}