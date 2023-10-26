using System.Text;
using MicroEthos.Common.Contracts;
using MicroEthos.Workflow.Abstraction.Constants;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MicroEthos.Workflow.Business.Providers;

public class ModulesClient : IModulesClient
{
    private readonly IJsonService _jsonService;
    private readonly IMicroEthosLogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public ModulesClient(
        IServiceProvider serviceProvider,
        IJsonService jsonService,
        IMicroEthosLogger logger)
    {
        _serviceProvider = serviceProvider;
        _jsonService = jsonService;
        _logger = logger;
    }

    public async Task<string> CallModule(string queueName, string moduleName,
        string stepName, object? inputPayload, WorkflowStateModel contextData)
    {
        var queueConnectionPool = (IPool<IQueueClient>) _serviceProvider.GetService(typeof(IPool<IQueueClient>))!;
        var payload = inputPayload == null
            ? new JObject().ToString()
            : JObject.FromObject(inputPayload).ToString();

        payload = BindServiceIdAndInstitutionIdWithReplacePayload(payload, contextData);
        inputPayload = JsonConvert.DeserializeObject(payload);
        
        var item = new
        {
            Command = stepName,
            ServiceId = contextData.ServiceId.ToString(),
            UserId = contextData.UserId.ToString(),
            InstitutionId = contextData.InstitutionId.ToString(),
            JobId = Guid.NewGuid().ToString(),
            TraceId = contextData.TraceId.ToString(),
            Payload = inputPayload,
            When = DateTime.UtcNow,
            ServiceName = ModuleConstants.ModuleName,
            RequireResponse = true
        };
        using (var connection = queueConnectionPool.Acquire())
        {
            await connection.Enqueue(queueName, _jsonService.Serialize(item));
        }

        await _logger.Info($"Called module ({moduleName}) action ({stepName}) with payload {payload} \n" +
                           $"TraceId is {item.TraceId} \n" +
                           $"JobId is {item.JobId}");

        return item.JobId;
    }

    public T PrepareOutput<T>(object output)
    {
        var res = _jsonService.Deserialize<T>(output.ToString().Trim('\"'));
        return res;
    }
    
    private string BindServiceIdAndInstitutionIdWithReplacePayload(string serializedInput, WorkflowStateModel context)
    {
        serializedInput = serializedInput.Replace("{}", "null");
        serializedInput = serializedInput.Replace("{", $"{{" +
                                                       $"\"ServiceId\": \"{context.ServiceId}\"," +
                                                       $"\"InstitutionId\": \"{context.InstitutionId}\"," +
                                                       $"\"UserId\": \"{context.UserId}\",");
        // serializedInput = serializedInput.Replace("\"institutionId\": \"00000000-0000-0000-0000-000000000000\"", $"\"institutionId\": \"{context.InstitutionId}\"");
        return serializedInput;
    }
}