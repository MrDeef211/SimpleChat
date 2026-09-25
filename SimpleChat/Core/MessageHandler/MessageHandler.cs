using Abstractions.Commands;
using Abstractions.Core.MessageService;

namespace Abstractions.Core.MessageHandler
{
    public class MessageHandler : IMessageHandler
    {
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public MessageHandler(IMessageService service)
        {
            service.MessageReceived += ReceiveMessage;
        }

        public void ReceiveMessage(object? sender, ReceiveMessageCommand command)
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(command.Message, command.Sender, command.SendTime));
        }
    }
}
