using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SportsStore.Models;
using SportsStore.Services.Payments;
using System.Text.Json;

namespace SportsStore.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderRepository repository;
        private readonly Cart cart;
        private readonly IPaymentService paymentService;
        private readonly ILogger<OrderController> logger;

        private const string PendingOrderSessionKey = "PendingOrder";

        public OrderController(
            IOrderRepository repoService,
            Cart cartService,
            IPaymentService paymentService,
            ILogger<OrderController> logger)
        {
            repository = repoService;
            cart = cartService;
            this.paymentService = paymentService;
            this.logger = logger;
        }

        public ViewResult Checkout()
        {
            logger.LogInformation(
                "Checkout page opened. CartLineCount={CartLineCount} CartTotal={CartTotal}",
                cart.Lines.Count(),
                cart.ComputeTotalValue());

            return View(new Order());
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            logger.LogInformation(
                "Checkout submitted. CartLineCount={CartLineCount} CartTotal={CartTotal}",
                cart.Lines.Count(),
                cart.ComputeTotalValue());

            if (!cart.Lines.Any())
            {
                ModelState.AddModelError("", "Sorry, your cart is empty!");
                logger.LogWarning("Checkout blocked: empty cart.");
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("Checkout validation failed. ErrorCount={ErrorCount}", ModelState.ErrorCount);
                return View(order);
            }

            // Store order details temporarily until payment succeeds
            HttpContext.Session.SetString(PendingOrderSessionKey, JsonSerializer.Serialize(order));

            // Build success/cancel URLs (Stripe will redirect to these)
            var successBaseUrl = Url.Action(
                action: nameof(PaymentSuccess),
                controller: "Order",
                values: null,
                protocol: Request.Scheme)!;

            var successUrl = $"{successBaseUrl}?session_id={{CHECKOUT_SESSION_ID}}";

            var cancelUrl = Url.Action(
                action: nameof(PaymentCancelled),
                controller: "Order",
                values: null,
                protocol: Request.Scheme)!;

            var correlationId = HttpContext.Response.Headers["X-Correlation-ID"].ToString();

            logger.LogInformation(
                "Creating Stripe checkout session. SuccessUrl={SuccessUrl} CancelUrl={CancelUrl} CorrelationId={CorrelationId}",
                successUrl, cancelUrl, correlationId);

            try
            {
                var (sessionId, checkoutUrl) = await paymentService.CreateCheckoutSessionAsync(
                    cart, successUrl, cancelUrl, correlationId);

                logger.LogInformation(
                    "Redirecting to Stripe Checkout. SessionId={SessionId}",
                    sessionId);

                return Redirect(checkoutUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Stripe Checkout session.");
                return RedirectToAction(nameof(PaymentFailed));
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentSuccess([FromQuery] string session_id)
        {
            logger.LogInformation("Returned from Stripe success redirect. SessionId={SessionId}", session_id);

            try
            {
                var session = await paymentService.GetCheckoutSessionAsync(session_id);

                if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Stripe session not paid. SessionId={SessionId} PaymentStatus={PaymentStatus}",
                        session.Id, session.PaymentStatus);

                    HttpContext.Session.Remove(PendingOrderSessionKey);
                    return RedirectToAction(nameof(PaymentFailed));
                }

                var pendingJson = HttpContext.Session.GetString(PendingOrderSessionKey);
                if (string.IsNullOrWhiteSpace(pendingJson))
                {
                    logger.LogWarning(
                        "No pending order found in session after payment success. SessionId={SessionId}",
                        session.Id);

                    return RedirectToAction(nameof(PaymentFailed));
                }

                var order = JsonSerializer.Deserialize<Order>(pendingJson);
                if (order is null)
                {
                    logger.LogWarning(
                        "Failed to deserialize pending order from session. SessionId={SessionId}",
                        session.Id);

                    HttpContext.Session.Remove(PendingOrderSessionKey);
                    return RedirectToAction(nameof(PaymentFailed));
                }

                if (!cart.Lines.Any())
                {
                    logger.LogWarning(
                        "Payment succeeded but cart is empty. SessionId={SessionId}",
                        session.Id);

                    HttpContext.Session.Remove(PendingOrderSessionKey);
                    return RedirectToAction(nameof(PaymentFailed));
                }

                //  Create the order ONLY after payment is confirmed as paid
                order.Lines = cart.Lines.ToArray();

                //  Store Stripe confirmation data on the order
                order.StripeCheckoutSessionId = session.Id;
                order.StripePaymentIntentId = session.PaymentIntentId;
                order.StripePaymentStatus = session.PaymentStatus;
                order.PaymentConfirmedAtUtc = DateTime.UtcNow;

                logger.LogInformation(
                    "Payment confirmed. Saving order. SessionId={SessionId} PaymentIntentId={PaymentIntentId} CustomerName={CustomerName} LineCount={LineCount}",
                    session.Id, session.PaymentIntentId, order.Name, order.Lines.Count);

                repository.SaveOrder(order);

                logger.LogInformation(
                    "Order confirmed after payment. OrderId={OrderId} SessionId={SessionId}",
                    order.OrderID, session.Id);

              
                cart.Clear();
                HttpContext.Session.Remove(PendingOrderSessionKey);

                
                return RedirectToPage("/Completed", new { orderId = order.OrderID });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating Stripe session or saving order. SessionId={SessionId}", session_id);
                HttpContext.Session.Remove(PendingOrderSessionKey);
                return RedirectToAction(nameof(PaymentFailed));
            }
        }

        [HttpGet]
        public IActionResult PaymentCancelled()
        {
            logger.LogWarning("Stripe payment cancelled by user.");

            HttpContext.Session.Remove(PendingOrderSessionKey);

            return View("Cancelled");
        }

        [HttpGet]
        public IActionResult PaymentFailed()
        {
            logger.LogWarning("Stripe payment failed or could not be confirmed.");

            HttpContext.Session.Remove(PendingOrderSessionKey);

            return View("Failed");
        }
    }
}