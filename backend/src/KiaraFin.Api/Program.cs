using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Auth;
using KiaraFin.Api.Features.Accounts;
using KiaraFin.Api.Features.Categories;
using KiaraFin.Api.Features.CreditCards;
using KiaraFin.Api.Features.CreditCardSubscriptions;
using KiaraFin.Api.Features.Dashboard;
using KiaraFin.Api.Features.Forecasts;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Installments;
using KiaraFin.Api.Features.Notifications;
using KiaraFin.Api.Features.Reports;
using KiaraFin.Api.Features.Transactions;
using KiaraFin.Api.Features.Pluggy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "Jwt:Issuer não configurado.")
    .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "Jwt:Audience não configurado.")
    .Validate(x => Encoding.UTF8.GetByteCount(x.Key) >= 32, "Jwt:Key deve possuir pelo menos 32 bytes.")
    .Validate(x => x.AccessTokenMinutes is > 0 and <= 60, "Jwt:AccessTokenMinutes inválido.")
    .Validate(x => x.RefreshTokenDays is > 0 and <= 90, "Jwt:RefreshTokenDays inválido.")
    .ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Configuração JWT ausente.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton<IRequestOriginValidator, RequestOriginValidator>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HouseholdService>();
builder.Services.AddScoped<HouseholdAccessService>();
builder.Services.AddScoped<DefaultCategorySeeder>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<ForecastService>();
builder.Services.AddScoped<InstallmentService>();
builder.Services.AddScoped<CreditCardService>();
builder.Services.AddScoped<CreditCardSubscriptionService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddOptions<TelegramNotificationOptions>()
    .Bind(builder.Configuration.GetSection(TelegramNotificationOptions.SectionName))
    .Validate(x => !x.Enabled || !string.IsNullOrWhiteSpace(x.BotToken),
        "TelegramNotifications:BotToken é obrigatório quando as notificações estão habilitadas.")
    .Validate(x => !x.Enabled || x.ChatId == 734302182,
        "TelegramNotifications:ChatId deve identificar o destinatário autorizado.")
    .Validate(x => !x.Enabled || string.Equals(x.AuthorizedEmail, "jhoownogueira@outlook.com", StringComparison.OrdinalIgnoreCase),
        "TelegramNotifications:AuthorizedEmail deve identificar o usuário autorizado.")
    .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
        "TelegramNotifications:BaseUrl deve ser uma URL HTTPS.")
    .ValidateOnStart();
builder.Services.AddSingleton<IAccountNotificationService>(services =>
{
    var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramNotificationOptions>>();
    var client = new HttpClient
    {
        BaseAddress = new Uri(settings.Value.BaseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(10)
    };
    return new TelegramNotificationService(client, settings,
        services.GetRequiredService<ILogger<TelegramNotificationService>>());
});
builder.Services.AddHostedService<ApplicationStartupNotificationService>();
builder.Services.AddOptions<PluggyOptions>()
    .Bind(builder.Configuration.GetSection(PluggyOptions.SectionName))
    .Validate(x => !x.Enabled || (!string.IsNullOrWhiteSpace(x.ClientId) && !string.IsNullOrWhiteSpace(x.ClientSecret)
        && !string.IsNullOrWhiteSpace(x.ItemId) && !string.IsNullOrWhiteSpace(x.WebhookSecret)),
        "Credenciais, ItemId e segredo de webhook Pluggy são obrigatórios quando a integração está habilitada.")
    .Validate(x => x.SyncIntervalHours is >= 1 and <= 168, "Pluggy:SyncIntervalHours inválido.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IPluggyClient, PluggyClient>((services, client) =>
{
    var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PluggyOptions>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<PluggyIntegrationService>();
builder.Services.AddHostedService<PluggyBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    return;
}

app.Run();

public partial class Program;
