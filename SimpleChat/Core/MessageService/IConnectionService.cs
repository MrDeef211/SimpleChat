using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
