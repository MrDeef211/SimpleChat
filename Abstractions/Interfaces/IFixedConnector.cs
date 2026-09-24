using Abstractions.DTO;

namespace Abstractions.Interfaces
{
    /// <summary>
    /// Транспортный слой: доставляет <see cref="MessageDTO"/> и <see cref="PingDTO"/>
    /// между узлами по <see cref="Guid"/>-адресам.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Зона ответственности.</b> Коннектор не знает ничего о протоколе:
    /// <list type="bullet">
    ///   <item>не решает, отвечать ли на пинг;</item>
    ///   <item>не хранит имена пользователей;</item>
    ///   <item>не управляет повторными попытками на уровне приложения;</item>
    ///   <item>не определяет, кто «свои», а кто «чужие».</item>
    /// </list>
    /// Всё это — работа <c>MessageService</c>.
    /// </para>
    /// <para>
    /// <b>Что должен делать коннектор.</b>
    /// <list type="bullet">
    ///   <item>отправлять DTO по указанному адресу и возвращать код результата;</item>
    ///   <item>эмитить <see cref="MessageReceived"/>, когда пришло сообщение;</item>
    ///   <item>эмитить <see cref="PingReceived"/>, когда пришёл любой пинг (без интерпретации);</item>
    ///   <item>управлять жизненным циклом соединения с конкретным пиром.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Потокобезопасность.</b> События могут эмититься из любых потоков —
    /// подписчик обязан сам синхронизироваться. Возвращаемые int-коды должны
    /// использовать единую семантику: 2xx — успех, остальное — ошибка
    /// (см. <see cref="Connect"/>, <see cref="Send"/>).
    /// </para>
    /// </remarks>
    public interface IFixedConnector
    {
        /// <summary>
        /// Сообщение получено от удалённого узла.
        /// </summary>
        /// <remarks>
        /// <see cref="MessageDTO.Sender"/> — Guid отправителя из сети;
        /// интерпретация (имя, доверенность, регистрация) — задача
        /// <c>MessageService</c>, а не коннектора.
        /// </remarks>
        event EventHandler<MessageDTO>? MessageReceived;

        /// <summary>
        /// Пинг получен от удалённого узла.
        /// </summary>
        /// <remarks>
        /// Коннектор не анализирует <see cref="PingDTO.reason"/> — он обязан
        /// просто передать пинг подписчику как есть. Значения
        /// <c>"discover"</c>, <c>"pong"</c> и т.п. — часть протокола верхнего
        /// уровня. Также коннектор <b>не отвечает</b> на пинг: решение об
        /// ответе принимает <c>MessageService</c>.
        /// </remarks>
        event EventHandler<PingDTO>? PingReceived;

        // ======================== Сообщения ========================

        /// <summary>
        /// Синхронно отправить сообщение конкретному узлу.
        /// </summary>
        /// <param name="message">Тело сообщения и метаданные отправителя.</param>
        /// <param name="receiver">Guid-адрес получателя.</param>
        /// <returns>
        /// Код результата: 2xx — доставлено; иное — ошибка
        /// (не подключён, недоступен, отказ сети).
        /// </returns>
        /// <remarks>
        /// Метод может блокироваться. Если сетевой стек асинхронный,
        /// реализуйте через <see cref="Task.Run(Action)"/> или сделайте
        /// синхронную обёртку над <see cref="SendAsync"/>.
        /// </remarks>
        int Send(MessageDTO message, Guid receiver);

        /// <summary>
        /// Асинхронно отправить сообщение конкретному узлу.
        /// </summary>
        /// <param name="message">Тело сообщения и метаданные отправителя.</param>
        /// <param name="receiver">Guid-адрес получателя.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>
        /// Код результата: 2xx — доставлено; иное — ошибка.
        /// </returns>
        /// <remarks>
        /// При отмене через <paramref name="token"/> метод должен бросить
        /// <see cref="OperationCanceledException"/>.
        /// </remarks>
        Task<int> SendAsync(MessageDTO message, Guid receiver, CancellationToken token = default);

        // ======================== Пинги ========================

        /// <summary>
        /// Синхронно отправить пинг конкретному узлу.
        /// </summary>
        /// <param name="ping">Тело пинга (sender, время, reason).</param>
        /// <param name="receiver">Guid-адрес получателя.</param>
        /// <remarks>
        /// Пингует <b>один</b> узел. Ответ (если он будет) вернётся
        /// асинхронно через <see cref="PingReceived"/> — коннектор
        /// не ждёт ответа и не решает, надо ли отвечать.
        /// </remarks>
        void Ping(PingDTO ping, Guid receiver);

        /// <summary>
        /// Асинхронно отправить пинг конкретному узлу.
        /// </summary>
        /// <param name="ping">Тело пинга.</param>
        /// <param name="receiver">Guid-адрес получателя.</param>
        /// <remarks>См. <see cref="Ping"/>.</remarks>
        Task PingAsync(PingDTO ping, Guid receiver);

        /// <summary>
        /// Синхронно разослать пинг всем узлам сети (broadcast).
        /// </summary>
        /// <param name="ping">Тело пинга для рассылки.</param>
        /// <remarks>
        /// <para>
        /// Каждая реализация может использовать свой механизм broadcast
        /// (UDP multicast, обход таблицы peer'ов, gRPC stream).
        /// </para>
        /// <para>
        /// Коннектор <b>не собирает</b> ответы и <b>не возвращает</b> список
        /// узлов. Все ответы придут асинхронно через <see cref="PingReceived"/>.
        /// Сбор и фильтрация — задача <c>MessageService</c>.
        /// </para>
        /// </remarks>
        void Broadcast(PingDTO ping);

        /// <summary>
        /// Асинхронно разослать пинг всем узлам сети.
        /// </summary>
        /// <param name="ping">Тело пинга.</param>
        /// <remarks>См. <see cref="Broadcast"/>.</remarks>
        Task BroadcastAsync(PingDTO ping);

        // ======================== Соединение ========================

        /// <summary>
        /// Установить соединение с узлом.
        /// </summary>
        /// <param name="address">Guid-адрес узла.</param>
        /// <returns>
        /// 2xx — соединение установлено; иное — отказ (узел недоступен,
        /// не существует, сеть недоступна).
        /// </returns>
        /// <remarks>
        /// После успешного <c>Connect</c> коннектор обязан принимать от узла
        /// сообщения и эмитить их через <see cref="MessageReceived"/>.
        /// </remarks>
        int Connect(Guid address);

        /// <summary>
        /// Асинхронно установить соединение с узлом.
        /// </summary>
        /// <param name="address">Guid-адрес узла.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>2xx — успех; иное — отказ.</returns>
        Task<int> ConnectAsync(Guid address, CancellationToken token = default);

        /// <summary>
        /// Разорвать соединение с узлом.
        /// </summary>
        /// <param name="address">Guid-адрес узла.</param>
        /// <param name="reason">Причина (для логов и/или передачи партнёру).</param>
        /// <remarks>
        /// Повторный вызов для уже отключённого узла не должен бросать
        /// исключение — операция идемпотентна.
        /// </remarks>
        void Disconnect(Guid address, string reason);

        /// <summary>
        /// Асинхронно разорвать соединение с узлом.
        /// </summary>
        /// <param name="address">Guid-адрес узла.</param>
        /// <param name="reason">Причина отключения.</param>
        Task DisconnectAsync(Guid address, string reason);

        // ======================== Приём ========================

        /// <summary>
        /// Начать приём входящих сообщений и пингов.
        /// </summary>
        /// <param name="token">Токен отмены приёма.</param>
        /// <returns>Задача, завершающаяся после запуска цикла приёма.</returns>
        /// <remarks>
        /// <para>
        /// После запуска коннектор обязан эмитить <see cref="MessageReceived"/>
        /// и <see cref="PingReceived"/> по мере поступления данных.
        /// </para>
        /// <para>
        /// Повторный вызов, пока приём уже запущен, должен быть no-op
        /// (не запускать второй цикл).
        /// </para>
        /// </remarks>
        Task StartReciveAsync(CancellationToken token = default);

        /// <summary>
        /// Прекратить приём входящих сообщений и пингов.
        /// </summary>
        /// <returns>Задача, завершающаяся после остановки цикла приёма.</returns>
        /// <remarks>
        /// Идемпотентно: повторный вызов на остановленном коннекторе — no-op.
        /// </remarks>
        Task StopReciveAsync();
    }
}