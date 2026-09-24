namespace Abstractions.DTO
{
    public class PingDTO
    {
        /// <summary>
        /// Сетевой id отправителя
        /// </summary>
        /// <remarks>
        /// Нужен для идентификации пользователя в сети, 
        /// для обнаружения пользователя по адрессу используются 
        /// внутренние протоколы реализации IConnector
        /// </remarks>
        public Guid Sender { get; set; }

        public DateTime PingTime { get; set; }

        public string? reason { get; set; }

        public PingDTO(Guid sender, DateTime time, string? reason = null)
        {
            Sender = sender;
            PingTime = time;
            this.reason = reason;
        }
    }
}
