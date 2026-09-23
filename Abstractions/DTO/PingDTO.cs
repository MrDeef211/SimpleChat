namespace Abstractions.DTO
{
    public class PingDTO
    {
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
