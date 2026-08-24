using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Storage;

namespace Nimpression.Infrastructure;

/// <summary>
/// 基础设施层的组合根。与应用层同理：新增一个适配器实现只需新增文件，
/// 具体注册尽量收敛到本文件的分组方法内，避免 Program.cs 变成人人必改的热点。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = ResolveConnectionString(configuration);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // 多个 Include 时拆成多条查询，避免笛卡尔积把列表接口的
                // 返回行数放大若干倍（N3.6 无 N+1 的另一面：也别搞成一次巨查询）
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
        });

        services.AddHttpContextAccessor();
        services.AddScoped<Nimpression.Application.Common.Abstractions.ICurrentUser, Nimpression.Infrastructure.Security.CurrentUser>();
        services.AddScoped<Nimpression.Application.Common.Abstractions.IUnitOfWork, Nimpression.Infrastructure.Persistence.UnitOfWork>();
        services.AddScoped<Nimpression.Application.Common.Abstractions.IAuditSink, Nimpression.Infrastructure.Persistence.Auditing.AuditSink>();
        services.AddScoped<Nimpression.Application.Features.Areas.Abstractions.IAreaRepository, Nimpression.Infrastructure.Persistence.Repositories.AreaRepository>();
        services.AddScoped<Nimpression.Application.Features.Dispatch.Abstractions.IJobTaskRepository, Nimpression.Infrastructure.Persistence.Repositories.JobTaskRepository>();
        services.AddScoped<Nimpression.Application.Features.Dispatch.Abstractions.IIdempotencyService, Nimpression.Infrastructure.Idempotency.IdempotencyService>();

        // 认证与授权基础设施（F1 认证授权）
        services.Configure<Nimpression.Infrastructure.Security.JwtSettings>(configuration.GetSection(Nimpression.Infrastructure.Security.JwtSettings.SectionName));
        services.AddSingleton<Nimpression.Application.Common.Security.IPasswordHasher, Nimpression.Infrastructure.Security.PasswordHasher>();
        services.AddSingleton<Nimpression.Application.Common.Security.IJwtTokenGenerator, Nimpression.Infrastructure.Security.JwtTokenGenerator>();
        services.AddScoped<Nimpression.Application.Features.Identity.Abstractions.IIdentityRepository, Nimpression.Infrastructure.Security.IdentityRepository>();

        var jwtSettings = configuration.GetSection(Nimpression.Infrastructure.Security.JwtSettings.SectionName).Get<Nimpression.Infrastructure.Security.JwtSettings>()
            ?? new Nimpression.Infrastructure.Security.JwtSettings();
        var key = System.Text.Encoding.UTF8.GetBytes(jwtSettings.Secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
            };

            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    // F1.5: 账号停用后 access token 在 <=60s 内失效
                    var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

                    if (Guid.TryParse(userIdClaim, out var userId))
                    {
                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                        if (user is null || user.Status != Nimpression.Domain.Enums.UserStatus.Active)
                        {
                            context.Fail("User is inactive or no longer exists.");
                        }
                    }
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Nimpression.Application.Common.Security.AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireRole(Nimpression.Domain.Enums.UserRole.Admin.ToString()));

            options.AddPolicy(Nimpression.Application.Common.Security.AuthorizationPolicies.Dispatcher, policy =>
                policy.RequireRole(Nimpression.Domain.Enums.UserRole.Admin.ToString(), Nimpression.Domain.Enums.UserRole.Dispatcher.ToString()));

            options.AddPolicy(Nimpression.Application.Common.Security.AuthorizationPolicies.AuthenticatedUser, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(Nimpression.Application.Common.Security.AuthorizationPolicies.DriverOnly, policy =>
                policy.RequireRole(Nimpression.Domain.Enums.UserRole.Driver.ToString()));
        });

        // 对象存储（F2.2 头像 / F8.4 罚单照片）
        services.AddStorage(configuration);

        return services;
    }

    /// <summary>
    /// 连接串解析顺序：配置 → 标准环境变量 → 容器编排常见的 DATABASE_URL → 本地默认值。
    /// 本地默认值只含开发口令，生产靠环境变量覆盖（N1.7：无密钥入库）。
    /// </summary>
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=nimpression;Username=nimpression;Password=devonly_change_me";
    }
}
