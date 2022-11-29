using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FurnitureApi.Data;
using FurnitureApi.DTOs.SMSModule;
using FurnitureApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FurnitureApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IConfiguration config;

        private FurnitureDbContext _dbContext;
        public OrdersController(IConfiguration config, FurnitureDbContext dbContext)
        {
            _dbContext = dbContext;

            this.config = config;
        }

        // For Admin
        // GET: api/Orders/PendingOrders
        [Authorize(Roles = "Admin")]
        [HttpGet("[action]")]
        public IActionResult PendingOrders()
        {
            var orders = _dbContext.Orders.Where(order => order.IsOrderCompleted == false);
            return Ok(orders);
        }

        // GET: api/Orders/CompletedOrders
        [Authorize(Roles = "Admin")]
        [HttpGet("[action]")]
        public IActionResult CompletedOrders()
        {
            var orders = _dbContext.Orders.Where(order => order.IsOrderCompleted == true);
            return Ok(orders);
        }

        // GET: api/Orders/OrderDetails/5
        [HttpGet("[action]/{orderId}")]
        public IActionResult OrderDetails(int orderId)
        {
            var orderDetails = from orderDetail in _dbContext.OrderDetails
                               join order in _dbContext.Orders on orderDetail.OrderId equals order.Id
                               join product in _dbContext.Products on orderDetail.ProductId equals product.Id
                               where orderDetail.OrderId == orderId

                               select new
                               {
                                   Id = orderDetail.Id,
                                   Qty = orderDetail.Qty,
                                   SubTotal = orderDetail.TotalAmount,
                                   ProductName = product.Name,
                                   ProductPrice = product.Price,
                               };

            return Ok(orderDetails);
        }


        // GET: api/Orders/OrdersCount
        [Authorize(Roles = "Admin")]
        [HttpGet("[action]")]
        public IActionResult OrdersCount()
        {
            var orders = (from order in _dbContext.Orders
                          where order.IsOrderCompleted == false
                          select order.IsOrderCompleted).Count();
            return Ok(new { PendingOrders = orders });
        }


        // GET: api/Orders/OrdersByUser/5
        [HttpGet("[action]/{userId}")]
        public IActionResult OrdersByUser(int userId)
        {
            var orders = from order in _dbContext.Orders
                         where order.UserId == userId
                         orderby order.OrderPlaced descending
                         select new
                         {
                             Id = order.Id,
                             FullName = order.FullName,
                             Address = order.Address,
                             Phone = order.Phone,
                             OrderTotal = order.OrderTotal,
                             OrderPlaced = order.OrderPlaced,
                         };

            return Ok(orders);
        }

        // POST: api/Orders
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Order order)
        {
            order.IsOrderCompleted = false;
            order.OrderPlaced = DateTime.Now;
            _dbContext.Orders.Add(order);
            _dbContext.SaveChanges();

            var shoppingCartItems = _dbContext.ShoppingCartItems.Where(cart => cart.CustomerId == order.UserId);
            foreach (var item in shoppingCartItems)
            {
                var orderDetail = new OrderDetail()
                {
                    Price = item.Price,
                    TotalAmount = item.TotalAmount,
                    Qty = item.Qty,
                    ProductId = item.ProductId,
                    OrderId = order.Id,
                };
                _dbContext.OrderDetails.Add(orderDetail);
            }

            SMSDTO sMSDTO = new SMSDTO();

            sMSDTO.Name = order.FullName;

            sMSDTO.PhoneNumber = order.Phone;

            sMSDTO.OrderId = order.Id;

            await SendOrderSMS(sMSDTO);

            _dbContext.SaveChanges();
            _dbContext.ShoppingCartItems.RemoveRange(shoppingCartItems);
            _dbContext.SaveChanges();

            return Ok(new { OrderId = order.Id });
        }

        // PUT: api/Orders/MarkOrderComplete/5
        [Authorize(Roles = "Admin")]
        [HttpPut("[action]/{orderId}")]
        public IActionResult MarkOrderComplete(int orderId, [FromBody] Order order)
        {
            var entity = _dbContext.Orders.Find(orderId);
            if (entity == null)
            {
                return NotFound("No order found against this id...");
            }
            else
            {
                entity.IsOrderCompleted = order.IsOrderCompleted;
                _dbContext.SaveChanges();
                return Ok("Order completed");
            }
        }




        public async Task<SMSDTO> SendOrderSMS(SMSDTO sMSDTO)
        {
            try
            {
                var url = "http://167.172.14.50:4002/v1/send-sms";

                var txtMessage = "Dear  " + sMSDTO.Name + " Your order no " + sMSDTO.OrderId + " , has been successfully placed, one of our agents will contact you within an hour";

                var key = config.GetValue<string>("SMS_Settings:BongaSMSKey");

                var secrete = config.GetValue<string>("SMS_Settings:BongaSMSSecrete");

                var apiClientID = config.GetValue<string>("SMS_Settings:BongaSMSApiClientID");

                var serviceID = config.GetValue<string>("SMS_Settings:BongaSMSServiceID");

                var msisdn = formatPhoneNumber(sMSDTO.PhoneNumber);

                var formContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("apiClientID", apiClientID),
                new KeyValuePair<string, string>("secret", secrete),
                new KeyValuePair<string, string>("key", key),
                new KeyValuePair<string, string>("txtMessage", txtMessage),
                new KeyValuePair<string, string>("MSISDN", msisdn),
                new KeyValuePair<string, string>("serviceID", serviceID),
                new KeyValuePair<string, string>("enqueue", "yes"),
            });

                HttpClient client = new HttpClient();

                HttpResponseMessage apiResult = await client.PostAsync(url, formContent);

                apiResult.EnsureSuccessStatusCode();

                var response = apiResult.Content.ReadAsStringAsync();

                return sMSDTO;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return null;
            }
        }

        public string formatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return string.Empty;

            string formatted = "";

            if (phoneNumber.StartsWith("0"))
                formatted = "+254" + phoneNumber.Substring(1, phoneNumber.Length - 1);

            if (phoneNumber.StartsWith("7"))
                formatted = "+254" + phoneNumber;

            if (phoneNumber.StartsWith("+254"))
                formatted = phoneNumber;

            if (phoneNumber.StartsWith("254"))
                formatted = "+" + phoneNumber;

            return formatted;
        }
    }
}
