namespace Abstractions.Commands
{
    public class SendMessageCommand
    {
        public string Message { get; set; }

        public Guid Sendler { get; set; }

        public string Receiver { get; set; }

        public DateTime SendTime { get; set; }
    }
}
