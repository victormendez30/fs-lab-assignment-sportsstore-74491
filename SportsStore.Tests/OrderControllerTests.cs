using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SportsStore.Controllers;
using SportsStore.Models;
using SportsStore.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SportsStore.Tests
{
    public class OrderControllerTests
    {
        private static OrderController CreateController(Mock<IOrderRepository> mockRepo, Cart cart)
        {
            var controller = new OrderController(
                mockRepo.Object,
                cart,
                new FakePaymentService(),
                NullLogger<OrderController>.Instance);

            // Needed because the controller uses Session for PendingOrder
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            return controller;
        }

        [Fact]
        public async Task Cannot_Checkout_Empty_Cart()
        {
            // Arrange
            var mock = new Mock<IOrderRepository>();
            var cart = new Cart();
            var order = new Order();
            var target = CreateController(mock, cart);

            // Act
            var actionResult = await target.Checkout(order);
            var result = Assert.IsType<ViewResult>(actionResult);

            // Assert - order not stored
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);

            // Assert - default view
            Assert.True(string.IsNullOrEmpty(result.ViewName));

            // Assert - model state invalid (empty cart adds an error)
            Assert.False(result.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Cannot_Checkout_Invalid_ShippingDetails()
        {
            // Arrange
            var mock = new Mock<IOrderRepository>();
            var cart = new Cart();
            cart.AddItem(new Product { Name = "Test", Price = 10m }, 1);

            var target = CreateController(mock, cart);

            // Add model error
            target.ModelState.AddModelError("error", "error");

            // Act
            var actionResult = await target.Checkout(new Order());
            var result = Assert.IsType<ViewResult>(actionResult);

            // Assert
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            Assert.True(string.IsNullOrEmpty(result.ViewName));
            Assert.False(result.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Can_Checkout_And_Start_Stripe_Payment()
        {
            // Arrange
            var mock = new Mock<IOrderRepository>();
            var cart = new Cart();
            cart.AddItem(new Product { Name = "Test", Price = 10m }, 1);

            var target = CreateController(mock, cart);

            // Act
            var actionResult = await target.Checkout(new Order());

            // Now Checkout POST redirects to Stripe (not Completed)
            var redirect = Assert.IsType<RedirectResult>(actionResult);
            Assert.Equal("https://example.com/stripe-checkout", redirect.Url);

            // Order should NOT be saved yet (only saved in PaymentSuccess after paid)
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
        }
    }

    /// <summary>
    /// Minimal in-memory ISession implementation for unit tests.
    /// </summary>
    internal sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> store = new();

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => store.Keys;

        public void Clear() => store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => store.Remove(key);

        public void Set(string key, byte[] value) => store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => store.TryGetValue(key, out value!);
    }
}