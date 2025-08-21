using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Newser.Api.Services;

namespace Newser.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        builder.Services.AddSingleton<IWorkerTrigger, WorkerTrigger>();
        builder.Services.AddHostedService<GathererBackgroundService>();
        var inMemoryLoggerProvider = new InMemoryLoggerProvider();
        builder.Services.AddSingleton(inMemoryLoggerProvider);
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(inMemoryLoggerProvider);
            logging.AddConsole();
        });
        
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // app.UseHttpsRedirection();
        app.UseCors(); // <-- dodaj to PRZED UseAuthorization
        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}