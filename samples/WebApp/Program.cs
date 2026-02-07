using EventBus.InMemory.Tests.Implementations;
using Softoverse.EventBus.InMemory;
using WebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddEventBus(builder.Configuration, typeof(Program).Assembly, typeof(TestScheduledHandler).Assembly);
builder.Services.AddSingleton(new EventTracker());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapSwagger();
    app.UseSwaggerUI(options =>
    {
        options.EnableTryItOutByDefault();
    });
}

app.UseHttpsRedirection();

app.MapEndpoints();

await app.RunAsync();