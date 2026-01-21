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
        private readonly IKafkaProducerService _kafkaProducerService = kafkaProducerService;
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] GeneralPackageTemplate dto)
        {
            await _kafkaProducerService.GiveAnswerAsync(JsonSerializer.Serialize(dto), topic: "auth-requests");
            return Ok();
        }
    }
}
