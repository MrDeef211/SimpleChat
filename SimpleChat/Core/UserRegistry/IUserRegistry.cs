using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleChat.Core.UserRegistry
{
    public interface IUserRegistry
    {
        /// <summary>
        /// Возвращает имя для Guid, автоматически регистрируя новое при отсутствии.
        /// </summary>
        string GetOrAddName(Guid id);

        /// <summary>
        /// Возвращает Guid по имени или бросает KeyNotFoundException.
        /// </summary>
        Guid GetId(string name);

        /// <summary>
        /// Возвращает Guid по имени или бросает KeyNotFoundException.
        /// </summary>
        bool TryGetId(string name, out Guid id);

        /// <summary>
        /// Возвращает Guid по имени или бросает KeyNotFoundException.
        /// </summary>
        bool TryGetName(Guid id, out string name);

        /// <summary>
        /// Удаляет пользователя (отключение, таймаут, потеря связи).
        /// </summary>
        void Remove(string name);

        /// <summary>
        /// Удаляет пользователя (отключение, таймаут, потеря связи).
        /// </summary>
        void Remove(Guid id);

        /// <summary>
        /// Переименование по инициативе UI.
        /// </summary>
        bool TryRename(string oldName, string newName);

        IReadOnlyCollection<string> Names { get; }
    }
}
