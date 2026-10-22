using System.Text.Json.Serialization;
using Messaging;
using Messaging.Telemetry;
using Microsoft.EntityFrameworkCore;
using Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using PaymentService.Application.Services;
using PaymentService.Domain.Ports;
using PaymentService.Infrastructure.Data;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Repositories;
using PaymentService.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddOdontoLogging();

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Eventos de pagamento: outbox no PaymentDbContext, publicado no tópico "payments".
builder.Services.AddKafkaMessaging(builder.Configuration)
    .AddOutbox<PaymentDbContext>();

builder.Services.AddScoped<IPaymentRepository, PostgresPaymentRepository>();
builder.Services.AddScoped<IPaymentEventPublisher, OutboxPaymentEventPublisher>();
builder.Services.AddScoped<IPaymentService, PaymentService.Application.Services.PaymentService>();
builder.Services.AddControllers(options =>
    {
        // Captura 400 de validação de modelo que acontecem antes da action.
        options.Filters.Add<ModelValidationMetricsFilter>();
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// ──────────────────────────────────────────────
// Observabilidade: métricas de negócio + Prometheus
// ──────────────────────────────────────────────
builder.Services.AddSingleton<PaymentMetrics>();
builder.Services.AddHostedService<PaymentMetricsSampler>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("payment-service"))
    .WithMetrics(metrics => metrics
        .AddMeter(PaymentMetrics.MeterName)
        .AddMeter(MessagingMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>("postgres");

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Em produção mantém o redirecionamento para HTTPS. Em desenvolvimento ele é
    // dispensado para que o Prometheus possa raspar /metrics via HTTP.
    app.UseHttpsRedirection();
}

app.UseOdontoObservability();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();
