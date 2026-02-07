namespace APIGateway.Config
{
    public static class ServiceRegistry
    {
        // Key = service name, Value = base URL
        public static Dictionary<string, string> Services { get; } = new()
        {
            { "Repos", "http://localhost:5000/" },
            { "api/tickets", "https://localhost:5070/" },
        };
    }
}
