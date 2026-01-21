using EKR_Shared.Handlers;
using Serilog;

namespace EKR_ApiGateway.Handlers
{
    public class KafkaMessageHandler : IKafkaMessageHandler
    {
        public async Task HandleAsync(string message, CancellationToken ct)
        {
            Log.Information(message);
        }
    }
}
