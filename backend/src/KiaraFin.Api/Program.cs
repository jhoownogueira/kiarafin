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
using KiaraFin.Api.Features.Dashboard;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Installments;
using KiaraFin.Api.Features.RecurringTransactions;
using KiaraFin.Api.Features.Reports;
using KiaraFin.Api.Features.Transactions;
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
builder.Services.AddScoped<RecurringTransactionService>();
builder.Services.AddScoped<InstallmentService>();
builder.Services.AddScoped<CreditCardService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();

public partial class Program;
