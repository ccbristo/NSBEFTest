using System.Reflection;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NServiceBus.Json;
using NServiceBus.Persistence;
using NServiceBus.Persistence.Sql;
using NServiceBus.TransactionalSession;

namespace NSBEFTest;

static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (IsRunningDotnetEF)
        {
            ConfigureDbContextForDotnetEF(builder);
            return;
        }

        await InitializeDatabase(builder);

        builder.Host.UseNServiceBus(context =>
        {
            var endpointName = "NSB-EF-Test";
            var endpointConfiguration = new EndpointConfiguration(endpointName);
            endpointConfiguration.SendFailedMessagesTo($"{endpointName}-error");
            endpointConfiguration.UseSerialization<SystemJsonSerializer>();
            endpointConfiguration.EnableInstallers();
            endpointConfiguration.EnableOutbox();

            var awsServiceUrlOverride = context.Configuration.GetValue<string>("AWSServiceUrlOverride")!;
            var routing = ConfigureLocalStack(endpointConfiguration, awsServiceUrlOverride);
            routing.RouteToEndpoint(Assembly.GetExecutingAssembly(), endpointName);

            string connectionString = builder.Configuration.GetConnectionString("NSB_EF_Test")!;
            ConfigureEntityFrameworkPersistence(connectionString, endpointConfiguration);

            return endpointConfiguration;
        });

        // Add services to the container.
        builder.Services.AddControllersWithViews();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        await app.RunAsync();
    }

    private static void ConfigureDbContextForDotnetEF(WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AppDbContext>(config =>
        {
            config.UseSqlServer(builder.Configuration.GetConnectionString("NSB_EF_Test"));
        });
        builder.Build();
    }

    private static bool IsRunningDotnetEF
    {
        get
        {
            // Windows guarantees we will at least get the name of the executing program, so we can ASSUME
            // we will get at least one command line argument. 95% sure this holds for *nix OSes too.
            var executingFilePath = Environment.GetCommandLineArgs()[0];
            var executingFile = Path.GetFileName(executingFilePath);
            return StringComparer.OrdinalIgnoreCase.Equals(executingFile, "ef.dll");
        }
    }

    private static async Task InitializeDatabase(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("NSB_EF_Test")!;

        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString);
        await using var dbContext = new AppDbContext(dbContextOptions.Options);
        await dbContext.Database.MigrateAsync();
    }

    private static void ConfigureEntityFrameworkPersistence(string connectionString, EndpointConfiguration endpoint)
    {
        var persistence = endpoint.UsePersistence<SqlPersistence>();
        persistence.ConnectionBuilder(() => new SqlConnection(connectionString));
        persistence.SqlDialect<SqlDialect.MsSqlServer>();
        persistence.EnableTransactionalSession();

        endpoint.RegisterComponents(services =>
        {
            services.AddScoped(serviceProvider =>
            {
                if (serviceProvider.GetRequiredService<ISynchronizedStorageSession>() is ISqlStorageSession { Connection: not null } session)
                {
                    var dbContextOptionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlServer(session.Connection);
                    
                    var context = new AppDbContext(dbContextOptionsBuilder.Options);

                    //Use the same underlying ADO.NET transaction
                    context.Database.UseTransaction(session.Transaction);

                    //Ensure context is flushed before the transaction is committed
                    session.OnSaveChanges((s, cancellationToken) => context.SaveChangesAsync(cancellationToken));

                    return context;
                }
                else
                {
                    var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlServer(connectionString)
                        .Options);
                    return context;
                }
            });
        });
    }

    private static RoutingSettings<SqsTransport> ConfigureLocalStack(EndpointConfiguration endpoint,
        string awsServiceUrlOverride)
    {
        var credentials = new BasicAWSCredentials("ear", "elephant");

        var sqsClient = new AmazonSQSClient(credentials, new AmazonSQSConfig
        {
            ServiceURL = awsServiceUrlOverride
        });

        var snsClient = new AmazonSimpleNotificationServiceClient(credentials,
            new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = awsServiceUrlOverride
            });

        var sqsTransport = new SqsTransport(sqsClient, snsClient);
        return endpoint.UseTransport(sqsTransport);
    }
}