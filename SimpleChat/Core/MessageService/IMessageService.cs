using Abstractions.Commands;
using Abstractions.DTO;

namespace SimpleChat.Core.MessageService
{
    // Для внутреннего использования IConnector, не использовать IConnector напрямую
    public interface IMessageService
    {
        event EventHandler<ReceiveMessageCommand> MessageReceived;

        void SendMessage(SendMessageCommand command);

        Task SendMessageAsync(SendMessageCommand command);
    }
}
