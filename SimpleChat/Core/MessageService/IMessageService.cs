using Abstractions.Commands;

namespace Abstractions.Core.MessageService
{
    // Для внутреннего использования IConnector, не использовать IConnector напрямую
    public interface IMessageService
    {
        /// <summary>
        /// Событие получения сообщения
        /// </summary>
        event EventHandler<ReceiveMessageCommand> MessageReceived;

        /// <summary>
        /// Отправить сообщение в сеть
        /// </summary>
        void SendMessage(SendMessageCommand command);

        /// <summary>
        /// Отправить сообщение в сеть
        /// </summary>
        Task SendMessageAsync(SendMessageCommand command);
    }
}
