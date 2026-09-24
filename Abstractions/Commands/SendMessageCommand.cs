namespace Abstractions.Commands
{
    public class SendMessageCommand
    {
        public string Message { get; set; }

        /// <summary>
        /// Сетевой id отправителя
        /// </summary>
        /// <remarks>
        /// Нужен для идентификации пользователя в сети, 
        /// для обнаружения пользователя по адрессу используются 
        /// внутренние протоколы реализации IConnector
        /// </remarks>
        public Guid Sender { get; set; }

        public string Receiver { get; set; }

        public DateTime SendTime { get; set; }

        public SendMessageCommand(string message, Guid sendler, string receiver, DateTime dateTime)
        {
            Message = message;
            Sender = sendler;
            Receiver = receiver;
            SendTime = dateTime;
        }
    }
}
