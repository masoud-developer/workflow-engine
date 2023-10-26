using MicroEthos.Common.Contracts;
using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;
using MicroEthos.Workflow.Business.Workflow.Providers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace MicroEthos.Workflow.Business.Workflow.Steps;

[StepModule("Primitives")]
public class ArrayMapper : StepBodyAsync
{
    private readonly IMicroEthosLogger _logger;

    public ArrayMapper()
    {
        _logger = StaticServiceProvider.ServiceProvider.GetRequiredService<IMicroEthosLogger>();
    }

    [StepInput] public object[] Collection { get; set; }
    [StepInput] public Dictionary<string, string> Map { get; set; } = new();
    [StepInput] public ArrayMapperType Type { get; set; } = ArrayMapperType.Object;

    [StepOutput] public JArray Result { get; set; } = new();

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        if (Map.Count == 0)
            return ExecutionResult.Next();

        var serializedInputArray = JArray.FromObject(Collection);

        if (Type is ArrayMapperType.Object)
        {
            var result = serializedInputArray.Where(s => s is JObject).Select(s =>
            {
                var mapped = new JObject();
                Map.ToList().ForEach(m =>
                {
                    var item = (JObject) s;
                    mapped.Add(m.Key, GetPathValue(m.Value, item));
                });
                return mapped;
            });
            Result = new JArray(result);
        }
        else if (Type is ArrayMapperType.String)
        {
            var result = serializedInputArray.Where(s => s is JObject)
                .Select(s => GetPathValue(Map.FirstOrDefault().Value, s)?.ToString());
            Result = new JArray(result);
        }
        else if (Type is ArrayMapperType.Integer)
        {
            var result = serializedInputArray.Where(s => s is JObject)
                .Select(s => Convert.ToInt32(GetPathValue(Map.FirstOrDefault().Value, s)?.ToString()));
            Result = new JArray(result);
        }

        return ExecutionResult.Next();
    }

    private JToken? GetPathValue(string path, JToken value)
    {
        try
        {
            if (path.Contains("$self.") || path.Contains("$this."))
            {
                path = path.Replace("$self.", string.Empty).Replace("$this.", string.Empty);
                var splitted = path.Split('.');
                var current = value[splitted[0]];
                for (var i = 1; i < splitted.Length; i++) current = current[splitted[i]];

                return current;
            }

            return path;
        }
        catch (Exception ex)
        {
            _logger.Error($"can not map path {path} from object {value} then set null.", ex);
            return null;
        }
    }
}