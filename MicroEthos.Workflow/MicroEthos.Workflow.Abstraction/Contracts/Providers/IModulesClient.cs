using MicroEthos.Workflow.Abstraction.Models.Workflow;

namespace MicroEthos.Workflow.Abstraction.Contracts.Providers;

public interface IModulesClient
{
    Task<string> CallModule(string queueName, string moduleName, string stepName,
        object inputPayload, WorkflowStateModel contextData);

    T PrepareOutput<T>(object output);
}