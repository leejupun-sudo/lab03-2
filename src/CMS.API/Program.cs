using System.Text.Json.Serialization;
using CMS.API.Data;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Dapper type handlers for DateOnly / TimeOnly columns.
DapperTypeHandlers.Register();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CMS API",
        Version = "v1",
        Description = "CMS 後台管理 API"
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Every endpoint but /api/auth/login needs a bearer token, so Swagger UI needs the 授權 box.
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "貼上 /api/auth/login 回傳的 accessToken (不必加 Bearer 前綴)。"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = JwtBearerDefaults.AuthenticationScheme
            }
        }] = []
    });
});

// JWT bearer authentication. The signing key is not in configuration — SysConfigSigningKeys
// reads it from the SysConfig 'appConfig' row per request, exactly as token issuing does.
builder.Services.AddSingleton<SysConfigSigningKeys>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Short claim names ('role', 'userId', 'userName') must survive validation unchanged —
        // the default inbound mapping would rewrite the long forms into ClaimTypes.* URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // The tokens carry no iss/aud (spec/auth/Login.md); a check against values that are
            // never written would either always fail or be decoration.
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtTokenService.UserIdClaimType,
            RoleClaimType = JwtTokenService.RoleClaimType
        };

        // Signature and lifetime say the token was ours and has not expired; neither says the
        // account behind it still exists or is still enabled. ActiveAccountEvents asks that on
        // every request, so deactivating or deleting a user takes effect immediately instead of
        // whenever the 24-hour token happens to run out.
        options.EventsType = typeof(ActiveAccountEvents);
    });
builder.Services.AddScoped<ActiveAccountEvents>();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<SysConfigSigningKeys>((options, signingKeys) => options.ConfigurationManager = signingKeys);

builder.Services.AddAuthorization();

const string LocalhostCorsPolicy = "LocalhostCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalhostCorsPolicy, policy => policy
        .SetIsOriginAllowed(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<ISysConfigRepository, SysConfigRepository>();
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IFeaturedPromoItemRepository, FeaturedPromoItemRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors(LocalhostCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// Authentication is applied to the whole controller surface here rather than by decorating each
// class, so a new controller is protected the moment it is added. AuthController.Login opts back
// out with [AllowAnonymous] — on the action, not the class; a class-level opt-out would also
// expose PUT /api/auth/profile, whose whole security is that an authenticated identity exists to
// read the account from. That endpoint is how a caller gets a token in the first place.
//
// This covers "is there a valid token"; it says nothing about what that token may do. The three
// 系統管理 controllers, plus the app-users / app-roles *actions* of LookupsController, add
// [Authorize(Roles = "Admin")] on top — five endpoints in all; see AppUsersController.
app.MapControllers().RequireAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>Exposed so the xUnit test project can host the API with WebApplicationFactory.</summary>
public partial class Program;
