namespace Abstractions.Commands
{
    public class ReceiveMessageCommand
    {
        public string Message { get; set; }

        public string Sendler { get; set; }

        public DateTime SendTime { get; set; }
    }
}
