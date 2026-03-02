using SportsStore.Models;

namespace SportsStore.Services.Payments
{
    public interface IPaymentService
    {
        Task<(string SessionId, string CheckoutUrl)> CreateCheckoutSessionAsync(
            Cart cart,
            string successUrl,
            string cancelUrl,
            string? correlationId);

        Task<Stripe.Checkout.Session> GetCheckoutSessionAsync(string sessionId);
    }
}