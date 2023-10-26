using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Business.Workflow.Providers;

namespace MicroEthos.Workflow.Server.Engine;
public static class Program
{
    public static async Task Main(string[] args)
    {
        var appIp = EnvironmentHelper.Get("APP_IP")!;
        var appPort = EnvironmentHelper.Get("APP_PORT")!;
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddServices(builder);
        var app = builder.Build();
        app.UseServices();
        StaticServiceProvider.ServiceProvider = app.Services;
        if (EnvironmentHelper.IsTest()) await app.RunAsync();
        else await app.RunAsync($"http://{appIp}:{appPort}");
    }
}