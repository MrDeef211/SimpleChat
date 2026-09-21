using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleChat.Interfaces
{
    public interface IConnector
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns>result</returns>
        int Connect(string address);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns>result</returns>
        Task<int> ConnectAsync(string address, CancellationToken token = default);

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
        int Send(string message);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns>result</returns>
        Task<int> SendAsync(string message, CancellationToken token = default);

        /// <summary>
        /// Установить статус приёма сообщений
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
