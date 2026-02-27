using SportsStore.Models;
using SportsStore.Services.Payments;
using Stripe.Checkout;
using System.Threading.Tasks;

namespace SportsStore.Tests.Fakes
{
    internal sealed class FakePaymentService : IPaymentService
    {
        public Task<(string SessionId, string CheckoutUrl)> CreateCheckoutSessionAsync(
            Cart cart,
            string successUrl,
            string cancelUrl,
            string? correlationId)
        {
            // Predictable URL for unit tests
            return Task.FromResult(("cs_test_fake", "https://example.com/stripe-checkout"));
        }

        public Task<Session> GetCheckoutSessionAsync(string sessionId)
        {
            // Default: pretend payment succeeded
            var session = new Session
            {
                Id = sessionId,
                PaymentStatus = "paid",
                PaymentIntentId = "pi_test_fake"
            };

            return Task.FromResult(session);
        }
    }
}