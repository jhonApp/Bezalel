using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bezalel.Aplication.Interface;
using Bezalel.Aplication.Service;
using Bezalel.Core.Interfaces;
using Bezalel.Infrastructure.Repositories;
using Bezalel.Infrastructure.Services;

namespace Bezalel.Ioc
{
    public static class DependencyContainer
    {
        public static IServiceCollection AddInfrastructureDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddHttpClient();

            // --- 1. AWS Clients ---
            services.AddSingleton<IAmazonDynamoDB>(sp => CreateDynamoDbClient(configuration));
            services.AddSingleton<IAmazonSQS>(sp => CreateSqsClient(configuration));
            services.AddSingleton<IAmazonS3>(sp => CreateS3Client(configuration));

            // --- 2. Application Services ---
            services.AddScoped<ICarouselService, CarouselService>();
            services.AddScoped<IProjectService, ProjectService>();

            // --- 3. Repositories ---
            services.AddScoped<ICarouselJobRepository>(sp =>
            {
                var client = sp.GetRequiredService<IAmazonDynamoDB>();
                var tableName = configuration["DynamoDb:CarouselJobTableName"] ?? "Bezalel_Dev_Job";
                return new DynamoDbCarouselJobRepository(client, tableName);
            });

            services.AddScoped<IProjectRepository>(sp =>
            {
                var client = sp.GetRequiredService<IAmazonDynamoDB>();
                var tableName = configuration["DynamoDb:ProjectTableName"] ?? "Bezalel_Dev_Projects";
                return new DynamoDbProjectRepository(client, tableName);
            });

            // --- 4. Infrastructure Services (Generic) ---
            services.AddScoped<IQueuePublisher, SqsPublisher>();
            services.AddScoped<ISafetyService, SafetyService>();
            services.AddScoped<IStorageService, S3StorageService>();

            services.AddScoped<IAuditPublisher>(sp =>
            {
                var sqsClient = sp.GetRequiredService<IAmazonSQS>();
                var auditUrl = configuration["AWS:AuditQueueUrl"] ?? string.Empty;
                return new SqsAuditPublisher(sqsClient, auditUrl);
            });

            // --- 5. Auth & Identity ---
            services.AddScoped<IDynamoDBContext>(sp => {
                var client = sp.GetRequiredService<IAmazonDynamoDB>();
                return new DynamoDBContext(client);
            });

            services.AddScoped<IUserRepository, DynamoDbUserRepository>();
            services.AddScoped<IRefreshTokenRepository, DynamoDbRefreshTokenRepository>();
            services.AddScoped<ICryptographyService, Argon2PasswordHasher>();
            services.AddScoped<ITokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }

        private static IAmazonDynamoDB CreateDynamoDbClient(IConfiguration configuration)
        {
            var serviceUrl = configuration["AWS:ServiceUrl"];
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                return new AmazonDynamoDBClient(new Amazon.Runtime.BasicAWSCredentials("test", "test"),
                    new AmazonDynamoDBConfig { ServiceURL = serviceUrl });
            }
            return new AmazonDynamoDBClient(Amazon.RegionEndpoint.USEast1);
        }

        private static IAmazonSQS CreateSqsClient(IConfiguration configuration)
        {
            var serviceUrl = configuration["AWS:ServiceUrl"];
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                return new AmazonSQSClient(new Amazon.Runtime.BasicAWSCredentials("test", "test"),
                    new AmazonSQSConfig { ServiceURL = serviceUrl });
            }
            return new AmazonSQSClient(Amazon.RegionEndpoint.USEast1);
        }

        private static IAmazonS3 CreateS3Client(IConfiguration configuration)
        {
            var serviceUrl = configuration["AWS:ServiceUrl"];
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                return new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials("test", "test"),
                    new AmazonS3Config { ServiceURL = serviceUrl, ForcePathStyle = true });
            }
            return new AmazonS3Client(Amazon.RegionEndpoint.USEast1);
        }
    }
}