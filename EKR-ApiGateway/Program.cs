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
