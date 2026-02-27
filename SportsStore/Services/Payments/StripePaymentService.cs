using Microsoft.Extensions.Options;
using Serilog;
using SportsStore.Infrastructure;
using SportsStore.Models;
using Stripe;
using Stripe.Checkout;

namespace SportsStore.Services.Payments
{
    public sealed class StripePaymentService : IPaymentService
    {
        private readonly StripeSettings settings;

        public StripePaymentService(IOptions<StripeSettings> options)
        {
            settings = options.Value;

            // The Stripe .NET SDK uses a static configuration approach commonly
            // (we set it once here). Keep it server-side only.
            StripeConfiguration.ApiKey = settings.SecretKey;
        }

        public async Task<(string SessionId, string CheckoutUrl)> CreateCheckoutSessionAsync(
            Cart cart,
            string successUrl,
            string cancelUrl,
            string? correlationId)
        {
            // Build line items from cart (amount in minor units, e.g. cents)
            var lineItems = cart.Lines.Select(l => new SessionLineItemOptions
            {
                Quantity = l.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = settings.Currency,
                    UnitAmount = ToMinorUnits(l.Product.Price),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = l.Product.Name
                    }
                }
            }).ToList();

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl, // include {CHECKOUT_SESSION_ID}
                CancelUrl = cancelUrl,
                LineItems = lineItems
            };

            // Optional metadata (helps debug / trace)
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                options.Metadata = new Dictionary<string, string>
                {
                    ["CorrelationId"] = correlationId
                };
            }

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            // Structured log (Serilog)
            Log.Information(
                "Stripe Checkout Session created. SessionId={SessionId} PaymentStatus={PaymentStatus} LineItemCount={LineItemCount}",
                session.Id,
                session.PaymentStatus,
                lineItems.Count);

            return (session.Id, session.Url);
        }

        public Task<Stripe.Checkout.Session> GetCheckoutSessionAsync(string sessionId)
        {
            var service = new SessionService();
            return service.GetAsync(sessionId);
        }

        private static long ToMinorUnits(decimal amount)
        {
            // Stripe expects integer minor units (e.g. cents)
            // Using round to 2 decimals, then * 100
            var value = decimal.Round(amount, 2, MidpointRounding.AwayFromZero) * 100m;
            return (long)value;
        }
    }
}