using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Shared;
using Api.Shared.Authentication;
using Api.Shared.ErrorHandling;
using FastEndpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddFastEndpoints(o => o.IncludeAbstractValidators = true);
builder.Services.AddMemoryCache();
builder.Services.AddDependencyInjection(builder);

var app = builder.Build();

// Set up middleware pipeline
app.UseHttpsRedirection();
app.UseFastEndpoints(c =>
{
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Serializer.Options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    c.Serializer.Options.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Endpoints.Configurator = ep => { ep.PostProcessors(Order.After, new PostProcessors()); };
});

app.UseMiddleware<SessionMiddleware>();
app.UseMiddleware<ErrorHandlerMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.Run();