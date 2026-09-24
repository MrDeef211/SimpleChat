namespace SimpleChat.Core.MessageFactory
{
    public interface IMessageFactory
    {
        /// <summary>
        /// Формирует сообщения для отправки
        /// </summary>
        void SendMessage(string message, string receiver);

        /// <summary>
        /// Формирует сообщение для отправки
        /// </summary>
        Task SendMessageAsync(string message, string receiver);
    }
}
