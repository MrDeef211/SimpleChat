namespace SimpleChat.Core.MessageService
{
    public interface IConnectionService
    {
        /// <summary>
        /// Подключение к ранее обнаруженному пользователю по имени
        /// </summary>
        int Connect(string user);

        /// <summary>
        /// Подключение к ранее обнаруженному пользователю по имени
        /// </summary>
        Task<int> ConnectAsync(string user);

        /// <summary>
        /// Отключение пользователя
        /// </summary>
        void Disconnect(string user, string reason);

        /// <summary>
        /// Отключение пользователя
        /// </summary>
        Task DisconnectAsync(string user, string reason);

        /// <summary>
        /// Подключён ли пользователь в данный момент
        /// </summary>
        bool IsConnected(string user);

        /// <summary>
        /// Список имён, к которым сейчас установлено соединение
        /// </summary>
        IReadOnlyCollection<string> GetConnectedUsers();

        /// <summary>
        /// Пользователь успешно подключён
        /// </summary>
        event EventHandler<string>? UserConnected;

        /// <summary>
        /// Пользователь отключён
        /// </summary>
        event EventHandler<string>? UserDisconnected;

        /// <summary>
        /// Обнаружение — возвращает только имена
        /// </summary>
        List<string> GetUsers();

        /// <summary>
        /// Обнаружение — возвращает только имена
        /// </summary>
        Task<List<string>> GetUsersAsync();

        /// <summary>
        /// Необязательное переименование для UI
        /// </summary>
        bool TryRename(string oldName, string newName);
    }
}
