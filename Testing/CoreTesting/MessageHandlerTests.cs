using System.ComponentModel;
using Abstractions.Commands;
using Moq;
using Abstractions.Core.MessageHandler;
using Abstractions.Core.MessageService;

namespace Testing.CoreTesting
{
    public class MessageHandlerTests
    {
        private readonly Mock<IMessageService> _service = new();
        private readonly MessageHandler _handler;

        public MessageHandlerTests()
        {
            _handler = new MessageHandler(_service.Object);
        }

        [Fact]
        [Description("Конструктор подписывается на событие MessageReceived сервиса")]
        public void Constructor_SubscribesToServiceEvent()
        {
            MessageReceivedEventArgs? received = null;
            _handler.MessageReceived += (_, args) => received = args;

            _service.Raise(s => s.MessageReceived += null, _service.Object,
                new ReceiveMessageCommand("hello", "Bob", DateTime.UtcNow));

            Assert.NotNull(received);
        }

        [Fact]
        [Description("Полученная команда преобразуется в аргументы события MessageReceivedEventArgs с сохранением полей")]
        public void ReceiveMessage_TranslatesCommandToEventArgs()
        {
            var time = DateTime.UtcNow;
            MessageReceivedEventArgs? received = null;
            _handler.MessageReceived += (_, args) => received = args;

            _service.Raise(s => s.MessageReceived += null, _service.Object,
                new ReceiveMessageCommand("hi", "Alice", time));

            Assert.NotNull(received);
            Assert.Equal("hi", received!.Message);
            Assert.Equal("Alice", received.Sender);
            Assert.Equal(time, received.SendTime);
        }

        [Fact]
        [Description("Событие MessageReceived вызывается для всех подписчиков")]
        public void ReceiveMessage_NotifiesAllSubscribers()
        {
            int count = 0;
            _handler.MessageReceived += (_, __) => count++;
            _handler.MessageReceived += (_, __) => count++;
            _handler.MessageReceived += (_, __) => count++;

            _service.Raise(s => s.MessageReceived += null, _service.Object,
                new ReceiveMessageCommand("x", "y", DateTime.UtcNow));

            Assert.Equal(3, count);
        }
    }
}
