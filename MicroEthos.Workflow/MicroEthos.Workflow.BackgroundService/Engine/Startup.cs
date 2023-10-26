using MicroEthos.Common.BackgroundServices.Extensions;
using MicroEthos.Workflow.BackgroundService.ServiceWorkers;
using Microsoft.Extensions.DependencyInjection;

namespace MicroEthos.Workflow.BackgroundService.Engine;

public static class Startup
{
    public static void AddWorkers(this IServiceCollection services)
    {
        services.AddHostedService<WorkflowRunnerServiceWorker>();
        services.AddHostedService<WorkflowModuleServiceWorker>();
        services.AddHostedService<TerminateRunningWorkflowsServiceWorker>();
    }
}