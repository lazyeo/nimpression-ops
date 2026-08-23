using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Features.Drivers.Storage;

namespace Nimpression.Infrastructure.Storage;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<StorageOptions>(options =>
        {
            var section = configuration.GetSection(StorageOptions.SectionName);
            if (section.Exists())
            {
                section.Bind(options);
            }

            var minioPort = Environment.GetEnvironmentVariable("MINIO_PORT") ?? "9000";
            var minioHost = Environment.GetEnvironmentVariable("MINIO_HOST") ?? "localhost";
            options.Endpoint = configuration["Storage:Endpoint"]
                ?? Environment.GetEnvironmentVariable("MINIO_ENDPOINT")
                ?? $"http://{minioHost}:{minioPort}";

            options.AccessKey = configuration["Storage:AccessKey"]
                ?? Environment.GetEnvironmentVariable("MINIO_ROOT_USER")
                ?? options.AccessKey;

            options.SecretKey = configuration["Storage:SecretKey"]
                ?? Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD")
                ?? options.SecretKey;
        });

        services.AddSingleton<IObjectStorageService, MinioObjectStorageService>();
        services.AddScoped<Nimpression.Application.Features.Drivers.Abstractions.IDriverRepository, DriverRepository>();
        services.AddSingleton<Nimpression.Application.Common.Abstractions.IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<Nimpression.Application.Common.Security.IPasswordHasher, PasswordHasher>();

        return services;
    }
}
