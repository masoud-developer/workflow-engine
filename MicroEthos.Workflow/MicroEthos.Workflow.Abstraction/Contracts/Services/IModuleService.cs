using MicroEthos.Common.Models;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Response;

namespace MicroEthos.Workflow.Abstraction.Contracts.Services;

public interface IModuleService
{
    Task<List<Models.Database.Modules>> LoadAll();
    Task<OperationResult<bool>> Load(string name, string version, byte[] assemblyFile);
    Task<OperationResult<bool>> Unload(string name, string version);
    Task<OperationResult<Guid>> CreateAndLoadDynamically(WorkflowAddModuleModel model);
    Task<OperationResult<List<WorkflowModuleResponse>>> List();
}