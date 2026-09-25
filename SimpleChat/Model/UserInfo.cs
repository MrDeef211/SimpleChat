using System.Text.Json.Serialization;

namespace Abstractions.Model
{
    public class UserInfo
    {
        /// <summary>
        /// Сетевой id пользователя
        /// </summary>
        /// <remarks>
        /// Нужен для идентификации пользователя в сети, 
        /// для обнаружения пользователя по адрессу используются 
        /// внутренние протоколы реализации IConnector
        /// </remarks>
        public Guid UserId { get; set; }

        /// <summary>
        /// Локальное имя пользователя для отображения у себя (не передаётся в сеть автоматически)
        /// </summary>
        public string LocalName { get; set; }

        [JsonConstructor]
        public UserInfo(Guid userId, string localName)
        {
            UserId = userId;
            LocalName = localName;
        }
    }
}
