namespace Abstractions.DTO
{
    public sealed class HelloDTO
    {
        public Guid Id { get; set; }
        public int TcpPort { get; set; }

        public HelloDTO() { }
        public HelloDTO(Guid id, int tcpPort) { Id = id; TcpPort = tcpPort; }
    }
}
