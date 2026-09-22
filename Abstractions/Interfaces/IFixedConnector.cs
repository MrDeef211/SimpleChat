using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abstractions.Commands;
using Abstractions.DTO;
using SimpleChat.Interfaces;

namespace Abstractions.Interfaces
{
    /// <summary>
    /// Временная замена IConnector
    /// </summary>
    public interface IFixedConnector
    {
        /// <summary>
        /// События получения сообщения
        /// </summary>
        event EventHandler<MessageDTO> MessageReceived;

        /// <summary>
        /// Событие получения пинга
        /// </summary>
        event EventHandler<PingDTO> PingReceived; 

        /// <summary>
        /// Пингануть пользователя
        /// </summary>
        /// <param name="ping"></param>
        /// <param name="receiver"></param>
        void Ping(PingDTO ping, Guid receiver);

        /// <summary>
        /// Пингануть пользователя
        /// </summary>
        /// <param name="ping"></param>
        /// <param name="receiver"></param>
        /// <returns></returns>
        Task PingAsync(PingDTO ping, Guid receiver);

        /// <summary>
        /// Получить список доступных пользователей в сети
        /// </summary>
        /// <param name="ping"></param>
        void GetUsers(PingDTO ping);

        /// <summary>
        /// Получить список доступных пользователей в сети
        /// </summary>
        /// <param name="ping"></param>
        Task GetUsersAsync(PingDTO ping);

        /// <summary>
        /// Подключится к некоторому пользователю
        /// </summary>
        /// <remarks>
        /// IConnector не должно получать сообщений от неподключённых пользователей (кроме ping), 
        /// отправка неподключённым пользователям опциональна
        /// </remarks>
        /// <param name="address"></param>
        /// <returns>result</returns>
        int Connect(Guid address);

        /// <summary>
        /// Подключится к некоторому пользователю
        /// </summary>
        /// <remarks>
        /// IConnector не должно получать сообщений от неподключённых пользователей (кроме ping), 
        /// отправка неподключённым пользователям опциональна
        /// </remarks>
        /// <param name="address"></param>
        /// <returns>result</returns>
        Task<int> ConnectAsync(Guid address, CancellationToken token = default);

        /// <summary>
        /// Отключится от пользователя
        /// </summary>
        /// <param name="reason"></param>
        void Disconect(string reason);

        /// <summary>
        /// Отключится от пользователя
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        Task DisconnectAsync(string reason);

        /// <summary>
        /// Отправить сообщение
        /// </summary>
        /// <param name="message"></param>
        /// <returns>result</returns>
        int Send(MessageDTO message, Guid receiver);

        /// <summary>
        /// Отправить сообщение
        /// </summary>
        /// <param name="message"></param>
        /// <returns>result</returns>
        Task<int> SendAsync(MessageDTO message, Guid receiver, CancellationToken token = default);

        /// <summary>
        /// Начать получать сообщение от всех пользователей
        /// </summary>
        /// <param name="recive">Принимать?</param>
        /// <returns></returns>
        Task StartReciveAsync(CancellationToken token = default);

        /// <summary>
        /// Прекратить получать сообщение от всех пользователей
        /// </summary>
        /// <param name="source"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task StopReciveAsync();
    }
}
