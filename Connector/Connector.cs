using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Connector
{
    public class Connector
    {
        private Socket? _socket;
        private bool _disposed;

        public Connector()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        private static (IPAddress? ip, int port) ParseAddress(string address)
        {
            var parts = address.Split(':');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out IPAddress? ip) && int.TryParse(parts[1], out int port))
            {
                return (ip, port);
            }
            else
            {
                throw new ArgumentException("Неверный формат адреса");
            }
        }

        #region Connect/Disconnect

        public int Connect(string address)
        {
            try
            {
                if (_socket == null || _disposed)
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _disposed = false;
                }
                var (ip, port) = ParseAddress(address);
                _socket.Connect(ip, port);

                return 0;
            }
            catch
            {
                return -1;
            }
        }

        public async Task<int> ConnectAsync(string address, CancellationToken token = default)
        {
            try
            {
                if (_socket == null || _disposed)
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _disposed = false;
                }

                var (ip, port) = ParseAddress(address);

                await _socket.ConnectAsync(ip, port, token);

                return 0;
            }
            catch
            {
                return -1;
            }
        }

        public void Disconnect(string reason)
        {
            if (_socket == null || _disposed)
            {
                return;
            }

            try
            {
                StopReciveAsync().GetAwaiter().GetResult();

                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            catch
            {

            }
            finally
            {
                _disposed = true;
            }
        }

        public async Task DisconnectAsync(string reson)
        {
            await Task.Run(() => Disconnect(reson));

        }

        #endregion

        #region Send

        public int Send(string message)
        {
            if (_socket == null || _disposed || !_socket.Connected)
            {
                return -2; // нет подключения
            }
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);

                int bytesSent = _socket.Send(data);

                return bytesSent;
            }
            catch
            {
                return -1; // сбой при отправке
            }
        }

        public async Task<int> SendAsync(string message, CancellationToken token = default)
        {
            if (_socket == null || _disposed || !_socket.Connected)
            {
                return -2;
            }
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);

                int byteSent = await _socket.SendAsync(data, SocketFlags.None, token);

                return byteSent;
            }
            catch
            {
                return -1;
            }
        }

        #endregion

        #region Recive

        public async Task StartReciveAsync(CancellationToken token = default)
        {

        }

        public async Task StopReciveAsync()
        {

        }

        #endregion
        public void Dispose()
        {
            if (!_disposed)
            {
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
                _socket?.Dispose();
                _disposed = true;
            }
        }

    }
}
