using MicroEthos.Common.Testing;
using MicroEthos.Workflow.Abstraction.Constants;
using MicroEthos.Workflow.DataAccess.Contexes;
using MicroEthos.Workflow.Server.Engine;

namespace MicroEthos.Workflow.Tests.Integration.Providers;

public class WorkflowProviderIntegrationTests:  IntegrationTestBase<TestStartup, DatabaseSeed, DbContext>
{
    public WorkflowProviderIntegrationTests() : base(ModuleRoutes.WorkflowApiPrefix)
    {
        
    }

    protected override async Task OnSeedingDatabase(DbContext dbContext, DatabaseSeed seedData)
    {
        // TODO: empty tables
        // TODO: re-fill tables
    }
}