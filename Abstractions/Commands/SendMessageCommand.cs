namespace Abstractions.Commands
{
    public class SendMessageCommand
    {
        public string Message { get; set; }

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
