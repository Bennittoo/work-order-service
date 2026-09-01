using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using WorkOrderService.Api.Endpoints;
using WorkOrderService.Api.Persistence;
using WorkOrderService.Api.Processing;
using WorkOrderService.Api.Security;
using WorkOrderService.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkOrderDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("WorkOrders"),
        // Transient database faults are retried by the provider, which is why the background
        // processor only has to handle the two application-level cases: a duplicate event and a
        // concurrency conflict.
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUniqueConstraintDetector, SqlServerUniqueConstraintDetector>();

builder.Services.Configure<ProgressEventOptions>(
    builder.Configuration.GetSection(ProgressEventOptions.SectionName));
builder.Services.AddSingleton<IProgressEventQueue, ChannelProgressEventQueue>();
builder.Services.AddHostedService<ProgressEventProcessor>();

// Validated at startup rather than on first use: a service that starts with no key configured would
// be silently unprotected, which is a worse failure than not starting.
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Value),
        $"{ApiKeyOptions.SectionName}:{nameof(ApiKeyOptions.Value)} must be configured.")
    .ValidateOnStart();

builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Work Order Service",
        Version = "v1",
        Description = "Work orders for network infrastructure rollouts, and asynchronous ingestion "
                      + "of progress events from external systems."
    });

    options.AddSecurityDefinition(ApiKeySecurityOperationFilter.SchemeId, new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Required on write endpoints."
    });

    // Applied per operation rather than document-wide, so the open read endpoints are not documented
    // as needing a key.
    options.OperationFilter<ApiKeySecurityOperationFilter>();

    // The API emits enums as strings; without this the document would describe them as integers.
    options.SchemaFilter<StringEnumSchemaFilter>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

// Anyone who opens the service in a browser lands on the root. Sending them to the API document is
// more useful than a 404 they have to interpret.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();
app.MapWorkOrderEndpoints();
app.MapProgressEventEndpoints();

app.Run();

/// <summary>
/// Exposed so the integration tests can drive the real application through
/// <c>WebApplicationFactory</c> rather than a hand-built host.
/// </summary>
public partial class Program;
