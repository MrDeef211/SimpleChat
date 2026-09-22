using System.Collections.Concurrent;
using Abstractions.Commands;
using Abstractions.DTO;
using SimpleChat.Interfaces;

namespace SimpleChat.MessageService
{
    internal class MessageService(IConnector connector) : IMessageService, IConnectionService, IDisposable
    {
        public event EventHandler<ReceiveMessageCommand>? MessageReceived;

        private ConcurrentDictionary<string, Guid> receivers = new ConcurrentDictionary<string, Guid>();

        public void SendMessage(SendMessageCommand command)
        {
            connector.Send(CreateDTO(command));
        }

        public async Task SendMessageAsync(SendMessageCommand command)
        {
            var tokenSource = new CancellationTokenSource();
            await connector.SendAsync(CreateDTO(command), tokenSource.Token);
        }

        public int Connect(Guid id, string name)
        {
            var result = connector.Connect(id);

            if (result >= 200 && result < 300)
                if (receivers.TryAdd(name, id))
                    receivers[name] = id;
            return result;
        }

        public async Task<int> ConnectAsync(Guid id, string name)
        {
            var result = await connector.ConnectAsync(id);

            if (result >= 200 && result < 300)
                if (receivers.TryAdd(name, id))
                    receivers[name] = id;
            return result;
        }

        public void Dispose()
        {
            receivers.Clear();
            MessageReceived = null;
        }

        private MessageDTO CreateDTO(SendMessageCommand command) =>
            new MessageDTO(command.Message, command.Sender, command.SendTime);

    }
}
