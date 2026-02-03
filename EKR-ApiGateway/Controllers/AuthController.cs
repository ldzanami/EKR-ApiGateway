using EKR_Shared;
using EKR_Shared.Data;
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
            if (dto.Type != AuthCommands.Register)
                return BadRequest("Wrong command type");
            return await Route(dto);
        }

        [HttpPost("auth")]
        public async Task<IActionResult> Authorize([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.Authorize)
                return BadRequest("Wrong command type");
            return await Route(dto);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.Refresh)
                return BadRequest("Wrong command type");
            return await Route(dto);
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeSession([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.Revoke)
                return BadRequest("Wrong command type");
            return await Route(dto);
        }

        [HttpPost("revoke-all")]
        public async Task<IActionResult> RevokeAllSessions([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.RevokeAll)
                return BadRequest("Wrong command type");
            return await Route(dto);
        }

        [HttpPost("revoke-others")]
        public async Task<IActionResult> RevokeOtherSessions([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.RevokeOthers)
                return BadRequest("Wrong command type");
            return await Route(dto);
        }

        [HttpPost("get-active")]
        public async Task<IActionResult> GetActiveSessions([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.GetActive)
                return BadRequest("Wrong command type");
            return await Route(dto);
        }

        [HttpGet("get-public-key")]
        public async Task<IActionResult> GetPublicKey([FromQuery] Guid requestId)
        {
            return await Route(new GeneralPackageTemplate
            {
                RequestId = requestId,
                Type = AuthCommands.GetPublicKey
            });
        }

        private async Task<IActionResult> Route(GeneralPackageTemplate dto)
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
