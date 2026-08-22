using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ThinhPhat.Api.Data;
using ThinhPhat.Api.Features.Auth;
using ThinhPhat.Api.Features.Customers;
using ThinhPhat.Api.Features.DeliveryOrders;
using ThinhPhat.Api.Features.Ghtk;
using ThinhPhat.Api.Features.Orders;
using ThinhPhat.Api.Features.Products;
using ThinhPhat.Api.Features.Warehouses;
using ThinhPhat.Api.Shared.Constants;
using ThinhPhat.Api.Shared.Errors;
using ThinhPhat.Api.Shared.Security;
using ThinhPhat.Api.Shared.Services.Cloudinary;
using ThinhPhat.Api.Shared.Services.Ghtk;

var builder = WebApplication.CreateBuilder(args);
//test deploy
// ==========================================
// 1. PERFORMANCE & PAYLOAD TUNING
// ==========================================
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Bật nén Brotli & Gzip để tiết kiệm băng thông mạng
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// ==========================================
// 2. RATE LIMITING (50 req / 1 phút per IP)
// ==========================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
            Detail = "Rate limit exceeded. Maximum 50 requests per minute allowed per IP address."
        };

        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;

        // Bypass cho HealthCheck và Swagger UI
        if (path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("BypassLimiter");
        }

        var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous_client";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// ==========================================
// 3. CORS (BẮT BUỘC CHO PUBLIC API)
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://your-frontend-domain.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ==========================================
// 4. DATABASE CONFIGURATION
// ==========================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string is not configured.");

builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        npgsqlOptions.CommandTimeout(15);
    });
}, poolSize: 64);

// ==========================================
// 5. SECURITY & JWT AUTHENTICATION
// ==========================================
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing in appsettings.");

builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.AddSingleton<IJwtProvider, JwtProvider>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = false; // Tiết kiệm RAM: không cache raw token trong HttpContext
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        ClockSkew = TimeSpan.Zero // Triệt tiêu 5 phút chênh lệch mặc định, hết hạn là block ngay
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(RoleConstants.Admin));
});

// ==========================================
// 6. SERVICE REGISTRATIONS
// ==========================================
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Register Cloudinary Service
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection(CloudinaryOptions.SectionName));
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();

// Register GHTK Service
builder.Services.Configure<GhtkOptions>(builder.Configuration.GetSection(GhtkOptions.SectionName));
builder.Services.AddHttpClient<IGhtkService, GhtkService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GhtkOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://services.giaohangtietkiem.vn" : options.BaseUrl.TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// Chỉ đăng ký Swagger trong môi trường Development (tiết kiệm RAM & reflection ở Production)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "ThinhPhat API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "Nhập trực tiếp chuỗi JWT Token vào ô bên dưới (Swagger sẽ tự động thêm tiền tố 'Bearer ').",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference("Bearer", doc),
                new List<string>()
            }
        });
    });
}

var app = builder.Build();

// ==========================================
// 7. PIPELINE MIDDLEWARES (Thứ tự CỰC KỲ QUAN TRỌNG)
// ==========================================
app.UseResponseCompression();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors("AllowFrontend");

app.UseAuthentication(); // 1. Xác thực danh tính
app.UseAuthorization();  // 2. Phân quyền

app.MapHealthChecks("/healthz");

// Register Feature Slices
app.MapAuthEndpoints();
app.MapCustomerEndpoints();
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapWarehouseEndpoints();
app.MapDeliveryOrderEndpoints();
app.MapGhtkEndpoints();

app.Run();