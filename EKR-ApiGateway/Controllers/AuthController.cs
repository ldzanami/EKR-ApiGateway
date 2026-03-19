using EKR_Shared;
using EKR_Shared.Data;
using EKR_Shared.Services.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace EKR_ApiGateway.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting("fixed")]
    public class AuthController(IKafkaProducerService kafkaProducerService, IConfiguration configuration) : ControllerBase
    {
        public static readonly Dictionary<string, TaskCompletionSource<string>> pending = [];
        public readonly IConfiguration _configuration = configuration;
        private readonly IKafkaProducerService _kafkaProducerService = kafkaProducerService;

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.Register)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [HttpPost("auth")]
        public async Task<IActionResult> Authorize([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.Authorize)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.Refresh)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeSession([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.Revoke)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("revoke-all")]
        public async Task<IActionResult> RevokeAllSessions([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.RevokeAll)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("revoke-others")]
        public async Task<IActionResult> RevokeOtherSessions([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.RevokeOthers)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("get-active")]
        public async Task<IActionResult> GetActiveSessions([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.GetActive)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }


        [HttpGet("get-public-key")]
        public async Task<IActionResult> GetPublicKey([FromQuery] Guid requestId)
        {
            return await Route(new GeneralPackageTemplate
            {
                RequestId = requestId,
                Type = AuthCommands.GetPublicKey
            }, _configuration["Kafka:AuthTopicName"]!);
        }

        [HttpGet("get-self-id")]
        public async Task<IActionResult> GetSelfId() => Ok(_configuration["SelfId"]);

        [HttpPost("keys-rotation")]
        public async Task<IActionResult> KeysRotation([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.KeysRotation)
                return BadRequest("Wrong command type");
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }


        private async Task<IActionResult> Route(GeneralPackageTemplate dto, string topic)
        {
            var cst = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
            try
            {
                await _kafkaProducerService.GiveAnswerAsync(JsonSerializer.Serialize(dto), partition: dto.RequestId.ToString(), topic: topic);
                var tcs = new TaskCompletionSource<string>();
                pending[dto.RequestId.ToString()] = tcs;
                var response = await tcs.Task.WaitAsync(cst);

                return Ok(response);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cst)
            {
                return BadRequest("Time out");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}