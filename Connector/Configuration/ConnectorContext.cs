namespace Connector.Configuration
{
    /// <summary>
    /// Контекст запуска транспорта: кто этот узел.
    /// Заполняется приложением (SimpleChat) при старте.
    /// </summary>
    public sealed record ConnectorContext(Guid UserId, string DisplayName);
}