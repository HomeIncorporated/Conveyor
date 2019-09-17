using System;
using System.Threading.Tasks;
using Convey.CQRS.Commands;
using Convey.MessageBrokers;
using Convey.Persistence.MongoDB;
using Conveyor.Services.Orders.Clients;
using Conveyor.Services.Orders.Domain;
using Conveyor.Services.Orders.Events;
using Conveyor.Services.Orders.RabbitMQ;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace Conveyor.Services.Orders.Commands.Handlers
{
    public class CreateOrderHandler : ICommandHandler<CreateOrder>
    {
        private readonly IMongoRepository<Order, Guid> _repository;
        private readonly IBusPublisher _publisher;
        private readonly IPricingServiceClient _pricingServiceClient;
        private readonly ILogger<CreateOrderHandler> _logger;

        public CreateOrderHandler(IMongoRepository<Order, Guid> repository, IBusPublisher publisher,
            IPricingServiceClient pricingServiceClient, ILogger<CreateOrderHandler> logger)
        {
            _repository = repository;
            _publisher = publisher;
            _pricingServiceClient = pricingServiceClient;
            _logger = logger;
        }

        public async Task HandleAsync(CreateOrder command)
        {
            var exists = await _repository.ExistsAsync(o => o.Id == command.OrderId);
            if (exists)
            {
                throw new OperationException($"Order with given id: {command.OrderId} already exists!");
            }

            _logger.LogInformation($"Fetching a price for order with id: {command.OrderId}...");
            var pricingDto = await _pricingServiceClient.GetOrderPricingAsync(command.OrderId);
            _logger.LogInformation($"Order with id: {command.OrderId} will cost: {pricingDto.TotalAmount}$.");
            var order = new Order(command.OrderId, command.CustomerId, pricingDto.TotalAmount);

            await _repository.AddAsync(order);
            _logger.LogInformation($"Created an order with id: {command.OrderId}.");
            await _publisher.PublishAsync(new OrderCreated(order.Id), new CorrelationContext());
        }
    }
}
