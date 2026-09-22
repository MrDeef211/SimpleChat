using Abstractions.Commands;

namespace SimpleChat.Core.MessageHandler
{
    public interface IMessageHandler
    {
        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public void ReceiveMessage(object sender, ReceiveMessageCommand command);
    }
}
