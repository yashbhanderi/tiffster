using System.Reflection;
using Api.Consumers;
using Api.Shared.Authentication;
using Api.Shared.Caching;
using Api.Shared.Dtos;
using Api.Shared.ErrorHandling;
using Api.Shared.Messaging;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

namespace Api.Shared;

public static class DependencyInjection
{
    public static void AddDependencyInjection(this IServiceCollection services, WebApplicationBuilder webAppBuilder)
    {
        // Fluent Validation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        webAppBuilder.Host.UseSerilog(
            (context, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration);
                config.Enrich.FromLogContext();
                config.WriteTo.Console();
            });

        // Authentication
        services.AddAuthentication(webAppBuilder);

        // Redis
        services.AddRedisCache(webAppBuilder);

        // RabbitMQ
        services.AddRabbitMqMessaging(webAppBuilder);
        services.AddGoogleDriveServices(webAppBuilder);

        // Error Handling
        services.AddSingleton<IErrorResponseProvider, ErrorResponseProvider>();
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
            options.SuppressMapClientErrors = true;
        });

        // Add other DI services here
        services.AddScoped<ITiffFileHelper, TiffFileHelper>();
        services.AddScoped<IRetryHelper, RetryHelper>();
    }

    public static void AddGoogleDriveServices(this IServiceCollection services, WebApplicationBuilder webAppBuilder)
    {
        webAppBuilder.Services.Configure<GoogleDriveConfigs>(webAppBuilder.Configuration.GetSection("GoogleDrive"));
        webAppBuilder.Services.AddSingleton<IGoogleDriveService, GoogleDriveService>();

        // Configure Kestrel server options
        webAppBuilder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 314572800; // 300 MB
        });

        // Configure IIS server options if using IIS
        webAppBuilder.Services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = 314572800; // 300 MB
        });

        // Configure JSON options for large objects
        webAppBuilder.Services.Configure<JsonOptions>(options =>
        {
            options.JsonSerializerOptions.WriteIndented = true;
        });

        // Configure form options
        webAppBuilder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 314572800; // 300 MB
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });
    }

    public static void AddAuthentication(this IServiceCollection services, WebApplicationBuilder builder)
    {
        services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
        services.Configure<SessionSettings>(builder.Configuration.GetSection("SessionSettings"));
        services.Configure<HeartbeatSettings>(builder.Configuration.GetSection("HeartbeatSettings"));
        builder.Services.AddSingleton<JwtService>();
        builder.Services.AddSingleton<SessionTrackingService>();
        builder.Services.AddHostedService<SessionHeartbeatService>();

        // Add CORS if needed
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowSpecificOrigins",
                policy => policy
                    .WithOrigins("http://localhost:3000") // Add your frontend origin
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Token-Changed", "X-New-Token")); // Expose token headers
        });
    }

    public static void AddRedisCache(this IServiceCollection services, WebApplicationBuilder webAppBuilder)
    {
        services.Configure<RedisSettings>(webAppBuilder.Configuration.GetSection("RedisSettings"));
        webAppBuilder.Services.AddSingleton<ConnectionMultiplexer>(sp =>
        {
            var redisSettings = sp.GetRequiredService<IOptions<RedisSettings>>().Value;
            return ConnectionMultiplexer.Connect(redisSettings.ConnectionString);
        });
    }

    private static void AddRabbitMqMessaging(this IServiceCollection services, WebApplicationBuilder webAppBuilder)
    {
        // Register publisher
        services.Configure<RabbitMqConfig>(webAppBuilder.Configuration.GetSection("RabbitMqConfig"));
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        // Configure MassTransit
        services.AddMassTransit(config =>
        {
            // Auto-discover consumers
            config.AddConsumers(typeof(BaseEvent).Assembly);

            // Configure RabbitMQ
            config.UsingRabbitMq((context, cfg) =>
            {
                cfg.UseConsumeFilter(typeof(AuthTokenFilter<>), context);
                var rabbitConfig = webAppBuilder.Configuration.GetSection("RabbitMq");

                cfg.Host(
                    rabbitConfig["Host"] ?? "localhost",
                    rabbitConfig["VirtualHost"] ?? "/",
                    h =>
                    {
                        h.Username(rabbitConfig["Username"] ?? "guest");
                        h.Password(rabbitConfig["Password"] ?? "guest");
                    });

                cfg.ReceiveEndpoint(nameof(PageChangedEventConsumer), e =>
                {
                    e.ConfigureConsumer<PageChangedEventConsumer>(context);
                    e.PrefetchCount = 1; // Match your original logic
                });
            });
        });
    }

    // Simple helper to register event consumers
    public static IServiceCollection AddEventConsumer<TEvent, TConsumer>(this IServiceCollection services)
        where TEvent : BaseEvent
        where TConsumer : class, IEventConsumer<TEvent>
    {
        services.AddScoped<IEventConsumer<TEvent>, TConsumer>();
        services.AddScoped<MassTransitConsumerAdapter<TEvent>>();
        return services;
    }
}