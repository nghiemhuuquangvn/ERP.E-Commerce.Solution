namespace ERP.Identity.Infrastructure.Configurations
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string Acks { get; set; } = "All";
        public int Retries { get; set; } = 3;
        public int SocketTimeoutMs { get; set; } = 30000;
        public int BatchSize { get; set; } = 16384;
        public int RetryBackoffMs { get; set; } = 1000;
        public int ReconnectBackoffMs { get; set; } = 10000;
    }
}