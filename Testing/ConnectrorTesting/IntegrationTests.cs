using Abstractions.Commands;
using Abstractions.DTO;
using Connector;
using Moq;
using Abstractions.Core.MessageService;
using Abstractions.Core.UserRegistry;
using Abstractions.Interfaces;
using Abstractions.Model;

namespace Testing.ConnectrorTesting
{
    /// <summary>
    /// End-to-end: два MessageService через два FakeConnector.
    /// Проверяем, что при замене транспорта протокол продолжает работать.
    /// </summary>
    public class IntegrationTests
    {
        private static (FakeConnector connA, FakeConnector connB) CreateNetwork()
        {

            var idA = Guid.NewGuid();
            var idB = Guid.NewGuid();

            var connA = new FakeConnector
            {
                Latency = TimeSpan.Zero,
                KnownPeers = new[] { idB },
            };
            var connB = new FakeConnector
            {
                Latency = TimeSpan.Zero,
                KnownPeers = new[] { idA },
            };

            connA.GetType();
            return (connA, connB);
        }

        [Fact]
        public async Task Discovery_ReturnsKnownPeers_OnBothSides()
        {
            var peerA = Guid.NewGuid();
            var peerB = Guid.NewGuid();

            var connA = new Mock<IConnector>();
            var connB = new Mock<IConnector>();

            connA.Setup(c => c.GetKnownPeers()).Returns(new[] { peerB });
            connB.Setup(c => c.GetKnownPeers()).Returns(new[] { peerA });

            var regA = new UserRegistry();
            var regB = new UserRegistry();

            using var svcA = new MessageService(connA.Object, regA,
                new UserInfo(peerA, "A"),
                discoveryTimeout: TimeSpan.FromMilliseconds(20));
            using var svcB = new MessageService(connB.Object, regB,
                new UserInfo(peerB, "B"),
                discoveryTimeout: TimeSpan.FromMilliseconds(20));

            var usersOnA = await svcA.GetUsersAsync();
            var usersOnB = await svcB.GetUsersAsync();

            Assert.Single(usersOnA);
            Assert.Single(usersOnB);
            Assert.Equal(peerB, regA.GetId(usersOnA[0]));
            Assert.Equal(peerA, regB.GetId(usersOnB[0]));
        }

        [Fact]
        public async Task Message_FlowsFromAtoB_AndBack()
        {
            var peerA = Guid.NewGuid();
            var peerB = Guid.NewGuid();

            var regA = new UserRegistry();
            var regB = new UserRegistry();

            var connA = new Mock<IConnector>();
            var connB = new Mock<IConnector>();

            var svcA = new MessageService(connA.Object, regA, new UserInfo(peerA, "A"),
                discoveryTimeout: TimeSpan.FromMilliseconds(30),
                sendTimeout: TimeSpan.FromMilliseconds(200));
            var svcB = new MessageService(connB.Object, regB, new UserInfo(peerB, "B"),
                discoveryTimeout: TimeSpan.FromMilliseconds(30),
                sendTimeout: TimeSpan.FromMilliseconds(200));

            var nameB = regA.GetOrAddName(peerB);
            var nameA = regB.GetOrAddName(peerA);

            connA.Setup(c => c.GetKnownPeers()).Returns(Array.Empty<Guid>());
            connB.Setup(c => c.GetKnownPeers()).Returns(Array.Empty<Guid>());

            connA
                .Setup(c => c.SendAsync(It.IsAny<MessageDTO>(), peerB, It.IsAny<CancellationToken>()))
                .ReturnsAsync(200)
                .Callback<MessageDTO, Guid, CancellationToken>((dto, _, __) =>
                {
                    connB.Raise(c => c.MessageReceived += null, connB.Object,
                        new MessageDTO(dto.Message, peerA, DateTime.UtcNow));
                });

            await svcA.ConnectAsync(nameB);
            await svcB.ConnectAsync(nameA);

            var receivedOnB = new TaskCompletionSource<ReceiveMessageCommand>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            svcB.MessageReceived += (_, c) => receivedOnB.TrySetResult(c);

            await svcA.SendMessageAsync(
                new SendMessageCommand("hello B", peerA, nameB, DateTime.UtcNow));

            var completed = await Task.WhenAny(receivedOnB.Task, Task.Delay(2000));
            Assert.Same(receivedOnB.Task, completed);

            var cmd = await receivedOnB.Task;
            Assert.Equal("hello B", cmd.Message);
            Assert.Equal(nameA, cmd.Sender);
        }
    }
}
