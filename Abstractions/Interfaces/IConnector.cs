using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abstractions.DTO;

namespace SimpleChat.Interfaces
{
    // Не использовать напрямую, только через IMessageService
    public interface IConnector
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns>result</returns>
        int Connect(Guid address);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns>result</returns>
        Task<int> ConnectAsync(Guid address, CancellationToken token = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reason"></param>
        void Disconect(string reason);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        Task DisconnectAsync(string reason);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns>result</returns>
        int Send(MessageDTO message);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns>result</returns>
        Task<int> SendAsync(MessageDTO message, CancellationToken token = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recive">Принимать?</param>
        /// <returns></returns>
        Task StartReciveAsync(CancellationToken token = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task StopReciveAsync();

    }
}
