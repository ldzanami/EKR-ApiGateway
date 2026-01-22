using Confluent.Kafka;
using EKR_ApiGateway.Controllers;
using EKR_Shared.Handlers.Interfaces;
using Serilog;

namespace EKR_ApiGateway.Handlers
{
    public class KafkaMessageHandler : IKafkaMessageHandler<string, string>
    {
        public async Task HandleAsync(Message<string, string> message, CancellationToken ct)
        {
            Log.Information("Received: {@Message}", message);
            if (AuthController.pending.Remove(message.Key, out var tcs))
            {
                tcs.SetResult(message.Value);
            }
        }
    }
}
