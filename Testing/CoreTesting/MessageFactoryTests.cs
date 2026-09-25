using System.ComponentModel;
using Abstractions.Commands;
using Moq;
using Abstractions.Core.MessageFactory;
using Abstractions.Core.MessageService;
using Abstractions.Model;

namespace Testing.CoreTesting
{
    public class MessageFactoryTests
    {
        private readonly Mock<IMessageService> _service = new();
        private readonly UserInfo _user = new(Guid.NewGuid(), "Local");
        private readonly MessageFactory _factory;

        public MessageFactoryTests()
        {
            _factory = new MessageFactory(_service.Object, _user);
        }

        [Fact]
        [Description("SendMessage формирует команду с заполненными полями и вызывает метод SendMessage сервиса")]
        public void SendMessage_CallsServiceWithPopulatedCommand()
        {
            _factory.SendMessage("hello", "Bob");

            _service.Verify(s => s.SendMessage(It.Is<SendMessageCommand>(c =>
                c.Message == "hello" &&
                c.Sender == _user.UserId &&
                c.Receiver == "Bob" &&
                c.SendTime != default)), Times.Once);
        }

        [Fact]
        [Description("SendMessageAsync формирует команду и вызывает асинхронный метод SendMessageAsync сервиса")]
        public async Task SendMessageAsync_CallsServiceWithPopulatedCommand()
        {
            _service
                .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageCommand>()))
                .Returns(Task.CompletedTask);

            await _factory.SendMessageAsync("hi", "Alice");

            _service.Verify(s => s.SendMessageAsync(It.Is<SendMessageCommand>(c =>
                c.Message == "hi" &&
                c.Sender == _user.UserId &&
                c.Receiver == "Alice")), Times.Once);
        }

        [Fact]
        [Description("SendMessage устанавливает время отправки в UTC")]
        public void SendMessage_UsesUtcTime()
        {
            DateTime captured = default;
            _service
                .Setup(s => s.SendMessage(It.IsAny<SendMessageCommand>()))
                .Callback<SendMessageCommand>(c => captured = c.SendTime);

            _factory.SendMessage("x", "y");

            Assert.True((DateTime.UtcNow - captured).Duration() < TimeSpan.FromMinutes(1));
        }
    }
}
