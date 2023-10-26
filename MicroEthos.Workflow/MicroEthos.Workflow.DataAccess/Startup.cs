using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("MicroEthos.Workflow.Tests.Integration")]

namespace MicroEthos.Workflow.DataAccess;

public static class Startup
{
    public static void AddDataAccess(this IServiceCollection services)
    {
    }
}