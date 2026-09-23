using Abstractions.Commands;
using SimpleChat.Core.MessageService;
using SimpleChat.Model;

namespace SimpleChat.Core.MessageFactory
{
    public class MessageFactory(IMessageService service, UserInfo user) : IMessageFactory
    {
        public void SendMessage(string message, string receiver)
        {
            service.SendMessage(new SendMessageCommand(message, user.UserId, receiver, DateTime.UtcNow));
        }

        public async Task SendMessageAsync(string message, string receiver)
        {
            await service.SendMessageAsync(new SendMessageCommand(message, user.UserId, receiver, DateTime.UtcNow));
        }
    }
}
