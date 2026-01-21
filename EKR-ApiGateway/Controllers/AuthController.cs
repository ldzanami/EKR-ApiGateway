using EKR_Shared;
using EKR_Shared.Services.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EKR_ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IKafkaProducerService kafkaProducerService) : ControllerBase
    {
        public static readonly Dictionary<string, TaskCompletionSource<string>> pending = [];
        private readonly IKafkaProducerService _kafkaProducerService = kafkaProducerService;
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] GeneralPackageTemplate dto)
        {
            try
            {
                await _kafkaProducerService.GiveAnswerAsync(JsonSerializer.Serialize(dto), partition: dto.RequestId.ToString(), topic: "auth-requests");
                var tcs = new TaskCompletionSource<string>();
                pending[dto.RequestId.ToString()] = tcs;
                var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
