using Abstractions.Commands;
using SimpleChat.Core.MessageService;

namespace SimpleChat.Core.MessageHandler
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
