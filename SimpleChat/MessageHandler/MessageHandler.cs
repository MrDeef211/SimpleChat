using Abstractions.Commands;
using SimpleChat.MessageService;

namespace SimpleChat.MessageHandler
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
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(command.Message, command.Sendler, command.SendTime));
        }
    }
}
