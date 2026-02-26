using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SportsStore.Models;

namespace SportsStore.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderRepository repository;
        private readonly Cart cart;
        private readonly ILogger<OrderController> logger;

        public OrderController(IOrderRepository repoService, Cart cartService, ILogger<OrderController> logger)
        {
            repository = repoService;
            cart = cartService;
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
        public IActionResult Checkout(Order order)
        {
            logger.LogInformation(
                "Checkout submitted. CartLineCount={CartLineCount} CartTotal={CartTotal}",
                cart.Lines.Count(),
                cart.ComputeTotalValue());

            if (cart.Lines.Count() == 0)
            {
                ModelState.AddModelError("", "Sorry, your cart is empty!");
                logger.LogWarning("Checkout blocked: empty cart.");
            }

            if (ModelState.IsValid)
            {
                order.Lines = cart.Lines.ToArray();

                logger.LogInformation(
                    "Creating order from checkout. CustomerName={CustomerName} LineCount={LineCount} Total={Total}",
                    order.Name,
                    order.Lines.Count,
                    cart.ComputeTotalValue());

                repository.SaveOrder(order);

                logger.LogInformation(
                    "Checkout successful. OrderId={OrderId} CustomerName={CustomerName} LineCount={LineCount} Total={Total}",
                    order.OrderID,
                    order.Name,
                    order.Lines.Count,
                    cart.ComputeTotalValue());

                cart.Clear();
                return RedirectToPage("/Completed", new { orderId = order.OrderID });
            }

            logger.LogWarning(
                "Checkout validation failed. ErrorCount={ErrorCount}",
                ModelState.ErrorCount);

            return View(order);
        }
    }
}