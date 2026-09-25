using System.ComponentModel;
using Abstractions.Commands;
using Abstractions.DTO;
using Moq;
using Abstractions.Core.MessageService;
using Abstractions.Core.UserRegistry;
using Abstractions.Interfaces;
using Abstractions.Model;

namespace Testing.CoreTesting
{
    public class MessageServiceTests : IDisposable
    {
        private readonly Mock<IConnector> _connector = new();
        private readonly UserRegistry _registry = new();
        private readonly UserInfo _user = new(Guid.NewGuid(), "Local");
        private readonly MessageService _service;

        private static readonly TimeSpan FastDiscovery = TimeSpan.FromMilliseconds(30);
        private static readonly TimeSpan FastSend = TimeSpan.FromMilliseconds(200);

        public MessageServiceTests()
        {

            _connector.Setup(c => c.GetKnownPeers()).Returns(Array.Empty<Guid>());

            _service = new MessageService(
                _connector.Object, _registry, _user,
                discoveryTimeout: FastDiscovery,
                sendTimeout: FastSend);
        }

        /// <summary>
        /// Помечает пира как «подключённого» в MessageService — так,
        /// как будто ConnectAsync завершился успешно.
        /// </summary>
        private string MarkPeerConnected(Guid peerId)
        {
            var name = _registry.GetOrAddName(peerId);
            _connector.Setup(c => c.Connect(peerId)).Returns(200);
            var result = _service.Connect(name);
            Assert.Equal(200, result);
            Assert.True(_service.IsConnected(name));
            return name;
        }

        public void Dispose() => _service.Dispose();

        // ================= Connect / Disconnect =================

        [Fact]
        [Description("Успешное подключение (код 200) отмечает пользователя как подключённого и вызывает событие UserConnected")]
        public void Connect_SuccessCode_MarksConnectedAndFiresEvent()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector.Setup(c => c.Connect(peerId)).Returns(200);

            string? firedFor = null;
            _service.UserConnected += (_, name) => firedFor = name;

            int result = _service.Connect(peerName);

            Assert.Equal(200, result);
            Assert.True(_service.IsConnected(peerName));
            Assert.Equal(peerName, firedFor);
        }

        [Fact]
        [Description("При неуспешном подключении (код 404) пользователь не отмечается подключённым, событие не вызывается")]
        public void Connect_FailureCode_DoesNotMarkOrFire()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector.Setup(c => c.Connect(peerId)).Returns(404);

            bool fired = false;
            _service.UserConnected += (_, __) => fired = true;

            Assert.Equal(404, _service.Connect(peerName));
            Assert.False(_service.IsConnected(peerName));
            Assert.False(fired);
        }

        [Fact]
        [Description("Попытка подключиться к неизвестному имени выбрасывает KeyNotFoundException")]
        public void Connect_UnknownName_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => _service.Connect("ghost"));
        }

        [Fact]
        [Description("Повторное подключение того же пользователя не вызывает событие UserConnected повторно")]
        public void Connect_Twice_DoesNotFireEventTwice()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector.Setup(c => c.Connect(peerId)).Returns(200);

            int fired = 0;
            _service.UserConnected += (_, __) => fired++;

            _service.Connect(peerName);
            _service.Connect(peerName);

            Assert.Equal(1, fired);
        }

        [Fact]
        [Description("Отключение отмечает пользователя как отключённого, вызывает событие UserDisconnected, имя в реестре сохраняется")]
        public void Disconnect_MarksDisconnectedAndPreservesRegistryName()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector.Setup(c => c.Connect(peerId)).Returns(200);

            _service.Connect(peerName);

            string? firedFor = null;
            _service.UserDisconnected += (_, name) => firedFor = name;

            _service.Disconnect(peerName, "bye");

            Assert.False(_service.IsConnected(peerName));
            Assert.Equal(peerName, firedFor);
            Assert.Equal(peerId, _registry.GetId(peerName));
        }

        [Fact]
        [Description("Попытка отключить не подключённого пользователя не вызывает событие UserDisconnected")]
        public void Disconnect_WhenNotConnected_DoesNotFire()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);

            bool fired = false;
            _service.UserDisconnected += (_, __) => fired = true;

            _service.Disconnect(peerName, "bye");

            Assert.False(fired);
        }

        [Fact]
        [Description("Асинхронное подключение с успешным кодом отмечает подключение и вызывает событие")]
        public async Task ConnectAsync_SuccessCode_MarksConnectedAndFiresEvent()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector
                .Setup(c => c.ConnectAsync(peerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(200);

            string? firedFor = null;
            _service.UserConnected += (_, name) => firedFor = name;

            Assert.Equal(200, await _service.ConnectAsync(peerName));
            Assert.True(_service.IsConnected(peerName));
            Assert.Equal(peerName, firedFor);
        }

        [Fact]
        [Description("Список подключённых пользователей корректно отражает подключения и отключения")]
        public void GetConnectedUsers_ReflectsConnectsAndDisconnects()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            string nameA = _registry.GetOrAddName(a);
            string nameB = _registry.GetOrAddName(b);
            _connector.Setup(c => c.Connect(a)).Returns(200);
            _connector.Setup(c => c.Connect(b)).Returns(200);

            _service.Connect(nameA);
            _service.Connect(nameB);
            Assert.Equal(2, _service.GetConnectedUsers().Count);

            _service.Disconnect(nameA, "bye");
            var remaining = _service.GetConnectedUsers();
            Assert.Single(remaining);
            Assert.Contains(nameB, remaining);
        }

        // ================= Send =================

        [Fact]
        [Description("Отправка сообщения зарегистрированному получателю вызывает метод Send коннектора с правильными параметрами")]
        public void SendMessage_RegisteredReceiver_CallsConnectorSend()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector.Setup(c => c.Send(It.IsAny<MessageDTO>(), peerId)).Returns(200);

            _service.SendMessage(new SendMessageCommand("hi", _user.UserId, peerName, DateTime.UtcNow));

            _connector.Verify(c => c.Send(
                It.Is<MessageDTO>(d => d.Message == "hi" && d.Sender == _user.UserId),
                peerId), Times.Once);
        }

        [Fact]
        [Description("Отправка сообщения неизвестному получателю выбрасывает KeyNotFoundException")]
        public void SendMessage_UnknownReceiver_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() =>
                _service.SendMessage(new SendMessageCommand("hi", _user.UserId, "ghost", DateTime.UtcNow)));
        }

        [Fact]
        [Description("Асинхронная отправка сообщения зарегистрированному получателю вызывает SendAsync коннектора")]
        public async Task SendMessageAsync_RegisteredReceiver_CallsConnectorSendAsync()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector
                .Setup(c => c.SendAsync(It.IsAny<MessageDTO>(), peerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(200);

            await _service.SendMessageAsync(
                new SendMessageCommand("hi", _user.UserId, peerName, DateTime.UtcNow));

            _connector.Verify(c => c.SendAsync(
                It.Is<MessageDTO>(d => d.Message == "hi"),
                peerId,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Description("При превышении таймаута асинхронной отправки выбрасывается TimeoutException")]
        public async Task SendMessageAsync_Timeout_ThrowsTimeoutException()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);

            _connector
                .Setup(c => c.SendAsync(It.IsAny<MessageDTO>(), peerId, It.IsAny<CancellationToken>()))
                .Returns<MessageDTO, Guid, CancellationToken>(async (_, __, ct) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    return 200;
                });

            await Assert.ThrowsAsync<TimeoutException>(() =>
                _service.SendMessageAsync(
                    new SendMessageCommand("hi", _user.UserId, peerName, DateTime.UtcNow)));
        }

        // ================= Входящие сообщения =================

        [Fact]
        [Description("При получении сообщения от известного отправителя событие MessageReceived вызывается с разрешённым именем отправителя")]
        public void MessageReceived_FromKnownSender_RaisesWithResolvedName()
        {
            var peerId = Guid.NewGuid();
            string peerName = MarkPeerConnected(peerId);

            ReceiveMessageCommand? received = null;
            _service.MessageReceived += (_, c) => received = c;

            _connector.Raise(c => c.MessageReceived += null, _connector.Object,
                new MessageDTO("hi", peerId, DateTime.UtcNow));

            Assert.NotNull(received);
            Assert.Equal("hi", received!.Message);
            Assert.Equal(peerName, received.Sender);
        }

        [Fact]
        [Description("При получении сообщения от неизвестного отправителя он регистрируется в реестре с новым именем")]
        public void MessageReceived_FromUnknownSender_RegistersNewName()
        {
            var peerId = Guid.NewGuid();
            string peerName = MarkPeerConnected(peerId);
            ReceiveMessageCommand? received = null;
            _service.MessageReceived += (_, c) => received = c;

            _connector.Raise(c => c.MessageReceived += null, _connector.Object,
                new MessageDTO("hi", peerId, DateTime.UtcNow));

            Assert.NotNull(received);
            Assert.StartsWith("User-", received!.Sender);
            Assert.Equal(peerId, _registry.GetId(received.Sender));
        }

        [Fact]
        [Description("Время отправки полученного сообщения сохраняется в UTC")]
        public void MessageReceived_PreservesUtcTime()
        {
            var peerId = Guid.NewGuid();
            string peerName = MarkPeerConnected(peerId);
            _registry.GetOrAddName(peerId);
            var time = new DateTime(2026, 9, 22, 10, 30, 0, DateTimeKind.Utc);

            ReceiveMessageCommand? received = null;
            _service.MessageReceived += (_, c) => received = c;

            _connector.Raise(c => c.MessageReceived += null, _connector.Object,
                new MessageDTO("hi", peerId, time));

            Assert.NotNull(received);
            Assert.Equal(time, received!.SendTime);
            Assert.Equal(DateTimeKind.Utc, received.SendTime.Kind);
        }

        // ================= Протокол пинга (новое) =================


        // ================= Discovery =================

        [Fact]
        public async Task GetUsersAsync_ReturnsKnownPeersExcludingSelf()
        {
            var peerA = Guid.NewGuid();
            var peerB = Guid.NewGuid();

            _connector
                .Setup(c => c.GetKnownPeers())
                .Returns(new[] { peerA, peerB, _user.UserId });   // self тоже в списке

            var users = await _service.GetUsersAsync();

            Assert.Equal(2, users.Count);
            Assert.Contains(users, u => _registry.GetId(u) == peerA);
            Assert.Contains(users, u => _registry.GetId(u) == peerB);
            Assert.DoesNotContain(users, u => _registry.GetId(u) == _user.UserId);
        }

        [Fact]
        public async Task GetUsersAsync_AssignsNamesToUnknownPeers()
        {
            var peerId = Guid.NewGuid();
            _connector.Setup(c => c.GetKnownPeers()).Returns(new[] { peerId });

            var users = await _service.GetUsersAsync();

            Assert.Single(users);
            Assert.StartsWith("User-", users[0]);
            // Имя зарегистрировано в реестре под этим Guid.
            Assert.Equal(peerId, _registry.GetId(users[0]));
        }


        public void GetUsers_ReturnsKnownPeersExcludingSelf()
        {
            var peerId = Guid.NewGuid();
            _connector
                .Setup(c => c.GetKnownPeers())
                .Returns(new[] { peerId, _user.UserId });

            var users = _service.GetUsers();

            Assert.Single(users);
            Assert.Equal(peerId, _registry.GetId(users[0]));
        }

        public async Task GetUsersAsync_EmptyNetwork_ReturnsEmptyList()
        {
            var users = await _service.GetUsersAsync();

            Assert.Empty(users);
        }

        // ================= Dispose =================

        [Fact]
        [Description("После Dispose события MessageReceived от коннектора больше не обрабатываются")]
        public void Dispose_UnsubscribesFromConnectorMessages()
        {
            var peerId = Guid.NewGuid();
            _registry.GetOrAddName(peerId);

            bool raised = false;
            _service.MessageReceived += (_, __) => raised = true;

            _service.Dispose();

            _connector.Raise(c => c.MessageReceived += null, _connector.Object,
                new MessageDTO("x", peerId, DateTime.UtcNow));

            Assert.False(raised);
        }

        [Fact]
        [Description("После Dispose входящие пинги игнорируются, ответ не отправляется")]
        public void Dispose_UnsubscribesFromPings()
        {
            var peerId = Guid.NewGuid();
            _service.Dispose();

            _connector.Raise(c => c.PingReceived += null, _connector.Object,
                new PingDTO(peerId, DateTime.UtcNow, "discover"));

            Assert.False(_registry.TryGetName(peerId, out _));
            _connector.Verify(c => c.PingAsync(It.IsAny<PingDTO>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        [Description("После Dispose список подключённых пользователей пуст")]
        public void Dispose_ClearsConnectedState()
        {
            var peerId = Guid.NewGuid();
            string peerName = _registry.GetOrAddName(peerId);
            _connector.Setup(c => c.Connect(peerId)).Returns(200);
            _service.Connect(peerName);

            _service.Dispose();

            Assert.Empty(_service.GetConnectedUsers());
        }
    }
}
