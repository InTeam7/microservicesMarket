using System;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Basket.API.Entities;
using Basket.API.GrpcService;
using Basket.API.Repositories;
using EventBus.Messages.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Basket.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class BasketController : ControllerBase
    {
        private readonly IBasketRepository _repository;

        private readonly ILogger<BasketController> _logger;

        private readonly DiscountGrpcService _discountGrpcService;

        private readonly IMapper _mapper;

        private readonly IPublishEndpoint _publishEndpoint;

        public BasketController(IBasketRepository repository,
            ILogger<BasketController> logger,
            DiscountGrpcService discountGrpcService,
            IMapper mapper,
            IPublishEndpoint publishEndpoint)
        {
            _repository = repository ?? throw  new ArgumentNullException(nameof(repository));
            _mapper = mapper ?? throw  new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _publishEndpoint = publishEndpoint ?? throw  new ArgumentNullException(nameof(publishEndpoint));
            _discountGrpcService = discountGrpcService ?? throw new ArgumentNullException(nameof(discountGrpcService));
        }
        [HttpGet("{userName}",Name = "GetBasket")]
        [ProducesResponseType(typeof(ShoppingCart),(int)HttpStatusCode.OK)]
        public async Task<ActionResult<ShoppingCart>> GetBasket(string userName)
        {
            var basket = await _repository.GetBasket(userName);
            return Ok(basket ?? new ShoppingCart(userName));
        }
        [HttpPost]
        [ProducesResponseType(typeof(ShoppingCart),(int)HttpStatusCode.OK)]
        public async Task<ActionResult<ShoppingCart>> UpdateBasket([FromBody] ShoppingCart basket)
        {
            //TODO : Communicate with Discount.Grpc and Calculate latest prices of product into shopping cart
            //consume discount grpc

            foreach (var item in basket.Items)
            {
                var coupon = await _discountGrpcService.GetDiscount(item.ProductName);
                item.Price -= coupon.Amount;
            }
            
            return Ok(await _repository.UpdateBasket(basket));
        }
        [HttpDelete("{userName}",Name = "DeleteBasket")]
        [ProducesResponseType(typeof(ShoppingCart),(int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteBasket(string userName)
        {
            await _repository.DeleteBasket(userName);
            return Ok();
        }
        [Route("[action]")]
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Checkout([FromBody] BasketCheckout basketCheckout)
        {
            var basket = await _repository.GetBasket(basketCheckout.UserName);
            if (basket == null)
            { 
                return BadRequest();
            }

            var eventMessage = _mapper.Map<BasketCheckoutEvent>(basketCheckout);
            eventMessage.TotalPrice = basket.TotalPrice;
            await _publishEndpoint.Publish(eventMessage);
            

            await _repository.DeleteBasket(basket.UserName);
            return Accepted();
        }
    }
}