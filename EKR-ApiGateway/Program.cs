using EKR_ApiGateway.Handlers;
using EKR_Shared.Handlers.Interfaces;
using EKR_Shared.Middlewares;
using EKR_Shared.Services.Infrastructure;
using EKR_Shared.Services.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

namespace EKR_ApiGateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Information()
                                                  .WriteTo.Console()
                                                  .WriteTo.File(
                                                                    "logs/app-.log",
                                                                    rollingInterval: RollingInterval.Day,
                                                                    retainedFileCountLimit: 7,
                                                                    fileSizeLimitBytes: 10_000_000,
                                                                    rollOnFileSizeLimit: true
                                                                )
                                                  .CreateLogger();
            try
            {
                Log.Information("Starting web application");
                var builder = WebApplication.CreateBuilder(args);

                builder.Configuration["Kafka:Address"] = Environment.GetEnvironmentVariable("KAFKA_ADDRESS") ?? builder.Configuration["Kafka:Address"];
                builder.Configuration["Kafka:GroupId"] = Environment.GetEnvironmentVariable("KAFKA_GROUP_ID") ?? builder.Configuration["Kafka:GroupId"];
                builder.Configuration["Kafka:ConsumerTopicName"] = Environment.GetEnvironmentVariable("KAFKA_CONSUMER_TOPIC_NAME") ?? builder.Configuration["Kafka:ConsumerTopicName"];
                builder.Configuration["Kafka:ProducerTopicName"] = Environment.GetEnvironmentVariable("KAFKA_PRODUCER_TOPIC_NAME") ?? builder.Configuration["Kafka:ProducerTopicName"];
                builder.Configuration["Kafka:Timeout"] = Environment.GetEnvironmentVariable("KAFKA_TIMEOUT") ?? builder.Configuration["Kafka:Timeout"];
                builder.Configuration["Jwt:Issuer"] = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? builder.Configuration["Jwt:Issuer"];
                builder.Configuration["Jwt:Audience"] = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? builder.Configuration["Jwt:Audience"];
                builder.Configuration["Jwt:Key"] = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"];
                builder.Configuration["Jwt:AccessTokenLifetimeMinutes"] = Environment.GetEnvironmentVariable("JWT_ACCESS_TOKEN_LIFETIME") ?? builder.Configuration["Jwt:AccessTokenLifetimeMinutes"];
                builder.Configuration["Jwt:RefreshTokenLifetimeDays"] = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_LIFETIME") ?? builder.Configuration["Jwt:RefreshTokenLifetimeDays"];
                builder.Configuration["AllowedHosts"] = Environment.GetEnvironmentVariable("ALLOWED_HOSTS") ?? builder.Configuration["AllowedHosts"];

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowFrontend", policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    });
                });

                builder.Services.AddControllers();

                builder.Logging.ClearProviders();
                builder.Services.AddSerilog();
                builder.Services.AddSwaggerGen();


                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                                .AddJwtBearer(o =>
                                {
                                    o.TokenValidationParameters = new TokenValidationParameters
                                    {
                                        ValidateIssuer = true,
                                        ValidIssuer = builder.Configuration["Jwt:Issuer"],

                                        ValidateAudience = true,
                                        ValidAudience = builder.Configuration["Jwt:Audience"],

                                        ValidateLifetime = true,
                                        ClockSkew = TimeSpan.Zero,

                                        ValidateIssuerSigningKey = true,
                                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
                                    };
                                });

                builder.Services.AddAuthorization();
                builder.Services.AddHostedService<KafkaConsumerService>();
                builder.Services.AddScoped<IKafkaProducerService, KafkaProducerService>();
                builder.Services.AddScoped<IKafkaMessageHandler<string, string>, KafkaMessageHandler>();

                var app = builder.Build();

                app.UseCors("AllowFrontend");


                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseSwagger();
                app.UseSwaggerUI();

                app.MapControllers();
                app.UseHttpsRedirection();
                app.UseRouting();

                app.Run();

            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
