using EKR_Shared;
using EKR_Shared.Data;
using EKR_Shared.Services.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using StackExchange.Redis;
using System.Text.Json;

namespace EKR_ApiGateway.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting("fixed")]
    public class AuthController(IKafkaProducerService kafkaProducerService,
                                IConfiguration configuration,
                                IConnectionMultiplexer redis) : ControllerBase
    {
        public static readonly Dictionary<string, TaskCompletionSource<string>> pending = [];
        public readonly IConfiguration _configuration = configuration;
        private readonly IKafkaProducerService _kafkaProducerService = kafkaProducerService;
        private readonly ISubscriber _sub = redis.GetSubscriber();

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] GeneralPackageTemplate dto)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС РЕГИСТРАЦИИ");
            if (dto.Type != AuthCommands.Register)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [HttpPost("auth")]
        public async Task<IActionResult> Authorize([FromBody] GeneralPackageTemplate dto)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС АВТОРИЗАЦИИ");
            if (dto.Type != AuthCommands.Authorize)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] GeneralPackageTemplate dto)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС РЕФРЕША");
            if (dto.Type != AuthCommands.Refresh)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeSession([FromBody] GeneralPackageTemplate dto)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС ОТМЕНЫ СЕССИИ");
            if (dto.Type != AuthCommands.Revoke)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("revoke-all")]
        public async Task<IActionResult> RevokeAllSessions([FromBody] GeneralPackageTemplate dto)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС ОТМЕНЫ ВСЕХ СЕССИЙ");
            if (dto.Type != AuthCommands.RevokeAll)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("revoke-others")]
        public async Task<IActionResult> RevokeOtherSessions([FromBody] GeneralPackageTemplate dto)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС ОТМЕНЫ ВСЕХ ДРУГИХ СЕССИЙ");
            if (dto.Type != AuthCommands.RevokeOthers)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [Authorize]
        [HttpPost("get-active")]
        public async Task<IActionResult> GetActiveSessions([FromBody] GeneralPackageTemplate dto)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС ПОЛУЧЕНИЯ АКТИВНЫХ СЕССИЙ");
            if (dto.Type != AuthCommands.GetActive)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }

        [HttpGet("get-public-key")]
        public async Task<IActionResult> GetPublicKey([FromQuery] Guid requestId)
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС ПОЛУЧЕНИЯ ПУБЛИЧНОГО КЛЮЧА");
            return await Route(new GeneralPackageTemplate
            {
                RequestId = Guid.NewGuid(),
                Type = AuthCommands.GetPublicKey
            }, _configuration["Kafka:AuthTopicName"]!);
        }

        [HttpGet("get-self-id")]
        public async Task<IActionResult> GetSelfId()
        {
            Log.Information("ПОЛУЧЕН ЗАПРОС ПОЛУЧЕНИЯ СВОЕГО ID");
            Log.Information("ЗАПРОС ПОЛУЧЕНИЯ СВОЕГО ID УСПЕШНО ОБРАБОТАН");
            return Ok(_configuration["SelfId"]);
        }

        [HttpPost("keys-rotation")]
        public async Task<IActionResult> KeysRotation([FromBody] GeneralPackageTemplate dto)
        {
            if (dto.Type != AuthCommands.KeysRotation)
            {
                Log.Information("НЕВЕРНЫЙ ТИП КОМАНДЫ: {@com} СОЕДИНЕНИЕ ЗАКРЫТО", dto.Type);
                return BadRequest("Wrong command type");
            }
            return await Route(dto, _configuration["Kafka:AuthTopicName"]!);
        }


        private async Task<IActionResult> Route(GeneralPackageTemplate dto, string topic)
        {
            var cst = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

            try
            {
                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending[dto.RequestId.ToString()] = tcs;
                Log.Information($"ПОДПИСКА НА ПОЛУЧЕНИЕ ОТВЕТА В REDIS");

                await _sub.SubscribeAsync($"response:{dto.RequestId}", (ch, msg) =>
                {
                    if (pending.Remove(dto.RequestId.ToString(), out var t))
                    {
                        t.SetResult(msg!);
                    }
                });
                Log.Information($"ПОДПИСКА НА ПОЛУЧЕНИЕ ОТВЕТА В REDIS ПРОШЛА УСПЕШНО");
                Log.Information("ОТПРАВКА ЗАПРОСА {@typ} В KAFKA", dto.Type);

                await _kafkaProducerService.GiveAnswerAsync(
                    JsonSerializer.Serialize(dto),
                    dto.RequestId.ToString(),
                    topic: topic
                );
                Log.Information("ОТПРАВКА ЗАПРОСА {@typ} В KAFKA ПРОШЛА УСПЕШНО", dto.Type);

                var response = await tcs.Task.WaitAsync(cst);
                Log.Information("ПОЛУЧЕНО СОБЫТИЕ ИЗ REDIS");
                Log.Information("ОТПИСКА ОТ СОБЫТИЯ В REDIS");

                await _sub.UnsubscribeAsync($"response:{dto.RequestId}");
                Log.Information("ОТПИСКА ОТ СОБЫТИЯ В REDIS ПРОШЛА УСПЕШНО");
                Log.Information("ЗАПРОС {@typ} УСПЕШНО ОБРАБОТАН", dto.Type);

                return Ok(response);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cst)
            {
                Log.Information("ВРЕМЯ ОЖИДАНИЯ ЗАПРОСА {@typ} ПРЕВЫШЕНО, СОЕДИНЕНИЕ ПРЕРВАНО", dto.Type);

                pending.Remove(dto.RequestId.ToString());
                Log.Information($"ПОПЫТКА ОТПИСАТЬСЯ ОТ ПОЛУЧЕНИЯ ОТВЕТА В REDIS");

                await _sub.UnsubscribeAsync($"response:{dto.RequestId}");
                Log.Information($"ПОПЫТКА ОТПИСАТЬСЯ ОТ ПОЛУЧЕНИЯ ОТВЕТА В REDIS ПРОШЛА УСПЕШНО");

                return BadRequest("Time out");
            }
            catch (Exception ex)
            {
                Log.Information("ЗАПРОС {@typ} ЗАВЕРШИЛСЯ С ОШИБКОЙ, {@err}", dto.Type, ex.Message);

                pending.Remove(dto.RequestId.ToString());
                Log.Information($"ПОПЫТКА ОТПИСАТЬСЯ ОТ ПОЛУЧЕНИЯ ОТВЕТА В REDIS");

                await _sub.UnsubscribeAsync($"response:{dto.RequestId}");
                Log.Information($"ПОПЫТКА ОТПИСАТЬСЯ ОТ ПОЛУЧЕНИЯ ОТВЕТА В REDIS ПРОШЛА УСПЕШНО");

                return BadRequest(ex.Message);
            }
        }
    }
}