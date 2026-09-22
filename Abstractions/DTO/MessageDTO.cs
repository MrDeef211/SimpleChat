namespace Abstractions.DTO
{
    public class MessageDTO
    {
        public string Message { get; set; }

        public Guid Sendler { get; set; }

        public DateTime SendTime { get; set; }

        public MessageDTO(string message, Guid sendler, DateTime dateTime)
        {
            Message = message;
            Sendler = sendler;
            SendTime = dateTime;
        }

    }
}
