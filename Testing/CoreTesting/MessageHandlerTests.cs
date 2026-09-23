using Abstractions.Commands;
using Moq;
using SimpleChat.Core.MessageHandler;
using SimpleChat.Core.MessageService;

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
        public void Constructor_SubscribesToServiceEvent()
        {
            MessageReceivedEventArgs? received = null;
            _handler.MessageReceived += (_, args) => received = args;

            _service.Raise(s => s.MessageReceived += null, _service.Object,
                new ReceiveMessageCommand("hello", "Bob", DateTime.UtcNow));

            Assert.NotNull(received);
        }

        [Fact]
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
