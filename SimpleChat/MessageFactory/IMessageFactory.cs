namespace SimpleChat.MessageFactory
{
    internal interface IMessageFactory
    {
        void SendMessage(string message, string receiver);

        Task SendMessageAsync(string message, string receiver);
    }
}
