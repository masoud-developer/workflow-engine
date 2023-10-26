using MicroEthos.Common.Extensions;
using MicroEthos.Common.Logging.Extensions;
using MicroEthos.Workflow.BackgroundService.Engine;
using MicroEthos.Workflow.Business;
using MicroEthos.Workflow.Business.Workflow.Providers;
using Microsoft.Extensions.Hosting;
public class Program
{
    public static async Task Main(params string[] args)
    {
        ThreadPool.SetMinThreads(100, 100);
        var app = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
        {
            services.AddMicroEthosCommon();
            services.AddMicroEthosLogging();
            services.AddBusiness();
            services.AddWorkers();
        }).UseMicroEthosLogging().Build();
        StaticServiceProvider.ServiceProvider = app.Services;
        await app.RunAsync();
    }
}