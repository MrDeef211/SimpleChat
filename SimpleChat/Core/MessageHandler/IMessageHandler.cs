using Abstractions.Commands;

namespace Abstractions.Core.MessageHandler
{
    public interface IMessageHandler
    {
        /// <summary>
        /// Событие завершения обработки сообщения
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Метод обработки сообщений
        /// </summary>
        public void ReceiveMessage(object sender, ReceiveMessageCommand command);
    }
}
