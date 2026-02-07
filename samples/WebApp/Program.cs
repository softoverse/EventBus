using System.Reflection;
using SampleCore;
using Softoverse.EventBus.InMemory;
using WebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

List<Assembly> assemblies = [
    typeof(Program).Assembly, 
    typeof(TestScheduledHandler).Assembly
];

if (Convert.ToBoolean(builder.Configuration["UseCustomEventProcessor"]))
{
    builder.Services.AddEventBus<CustomEventProcessor>(builder.Configuration, assemblies);
}
else
{
    builder.Services.AddEventBus(builder.Configuration, assemblies);
}

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
