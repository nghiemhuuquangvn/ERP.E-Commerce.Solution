namespace ERP.ACC.Domain
{
    /// <summary>
    /// A document could not be posted because the tenant's posting profile is incomplete (a key has no account, or it points
    /// to an inactive / unknown one). The document itself is still saved; the missing entry is logged so it can be fixed.
    /// </summary>
    public class PostingConfigurationException : Exception
    {
        public string PostingKey { get; }

        public PostingConfigurationException(string postingKey, string message) : base(message)
        {
            PostingKey = postingKey;
        }
    }
}
