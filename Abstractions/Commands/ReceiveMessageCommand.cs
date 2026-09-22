namespace Abstractions.Commands
{
    public class ReceiveMessageCommand
    {
        public string Message { get; set; }

        public string Sender { get; set; }

        public DateTime SendTime { get; set; }

        public ReceiveMessageCommand(string message, string sender, DateTime time)
        {
            Message = message;
            Sender = sender;
            SendTime = time;
        }
    }
}
