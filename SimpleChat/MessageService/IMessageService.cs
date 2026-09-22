using Abstractions.Commands;
using Abstractions.DTO;

namespace SimpleChat.MessageService
{
    // Для внутреннего использования IConnector, не использовать IConnector напрямую
    internal interface IMessageService
    {
        event EventHandler<ReceiveMessageCommand> MessageReceived;

        void SendMessage(SendMessageCommand command);

        Task SendMessageAsync(SendMessageCommand command);
    }
}
