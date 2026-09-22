namespace SimpleChat.MessageFactory
{
    internal interface IMessageFactory
    {
        /// <summary>
        /// Формирует сообщения для отправки
        /// </summary>
        /// <param name="message"></param>
        /// <param name="receiver"></param>
        void SendMessage(string message, string receiver);

        /// <summary>
        /// Формирует сообщение для отправки
        /// </summary>
        /// <param name="message"></param>
        /// <param name="receiver"></param>
        /// <returns></returns>
        Task SendMessageAsync(string message, string receiver);
    }
}
