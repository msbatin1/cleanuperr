using Common.Configuration;
using Executable.Jobs;
using Infrastructure.Verticals.FrozenTorrent;

namespace Executable;
using Quartz;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddLogging(builder => builder.AddConsole())
            .AddHttpClient()
            .AddConfiguration(configuration)
            .AddServices()
            .AddQuartzServices(configuration);

    private static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration) =>
        services
            .Configure<QuartzConfig>(configuration.GetSection(nameof(QuartzConfig)))
            .Configure<SonarrConfig>(configuration.GetSection(nameof(SonarrConfig)));

    private static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddTransient<FrozenTorrentHandler>();

    private static IServiceCollection AddQuartzServices(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddQuartz(q =>
            {
                QuartzConfig? config = configuration.GetRequiredSection(nameof(QuartzConfig)).Get<QuartzConfig>();

                if (config is null)
                {
                    throw new NullReferenceException("Quartz configuration is null");
                }

                q.AddFrozenTorrentJob(config.FrozenTorrentTrigger);
            })
            .AddQuartzHostedService(opt =>
            {
                opt.WaitForJobsToComplete = true;
            });

    private static void AddFrozenTorrentJob(this IServiceCollectionQuartzConfigurator q, string trigger)
    {
        q.AddJob<FrozenTorrentJob>(opts =>
        {
            opts.WithIdentity(nameof(FrozenTorrentJob));
        });

        q.AddTrigger(opts =>
        {
            opts.ForJob(nameof(FrozenTorrentJob))
                .WithIdentity($"{nameof(FrozenTorrentJob)}-trigger")
                .WithCronSchedule(trigger);
        });
    }
}