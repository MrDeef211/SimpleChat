namespace Abstractions.DTO
{
    public class MessageDTO
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

        public DateTime SendTime { get; set; }

        public MessageDTO(string message, Guid sender, DateTime sendTime)
        {
            Message = message;
            Sender = sender;
            SendTime = sendTime;
        }

    }
}
