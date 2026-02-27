namespace SportsStore.Infrastructure
{
    public sealed class StripeSettings
    {
        public string SecretKey { get; set; } = "";
        public string PublishableKey { get; set; } = "";
        public string Currency { get; set; } = "eur"; // change to "usd" if you prefer
    }
}