using Abstractions.Commands;

namespace SimpleChat.MessageHandler
{
    internal interface IMessageHandler
    {
        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public void ReceiveMessage(object sender, ReceiveMessageCommand command);
    }
}
