using Confluent.Kafka;
using EKR_Shared.Handlers.Interfaces;
using Serilog;
using StackExchange.Redis;
using System.Text;

namespace EKR_ApiGateway.Handlers
{
    public class KafkaMessageHandler(IConnectionMultiplexer redis) : IKafkaMessageHandler<string, string>
    {
        private readonly ISubscriber _sub = redis.GetSubscriber();

        public async Task<bool> HandleAsync(Message<string, string> message, CancellationToken ct)
        {
            Log.Information("ОБРАБОТКА ОТВЕТА ОТ СЕРВИСА");

            var requestId = Encoding.UTF8.GetString(message.Headers.GetLastBytes("request-id"));

            Log.Information("ПУБЛИКАЦИЯ ОТВЕТА ОТ СЕРВИСА В REDIS");

            await _sub.PublishAsync
                  (
                      $"response:{requestId}",
                      message.Value
                  );

            Log.Information("ПУБЛИКАЦИЯ ОТВЕТА ОТ СЕРВИСА В REDIS ПРОШЛА УСПЕШНО");

            return true;
        }
    }
}
