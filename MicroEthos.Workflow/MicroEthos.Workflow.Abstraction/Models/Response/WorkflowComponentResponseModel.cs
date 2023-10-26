using System.ComponentModel.DataAnnotations;
using MicroEthos.Workflow.Abstraction.Enums;
using Newtonsoft.Json.Linq;

namespace MicroEthos.Workflow.Abstraction.Models.Response;

public class WorkflowComponentGroupResponseModel
{
    [MaxLength(250)] public string Name { get; set; }

    public List<WorkflowComponentResponseModel> Components { get; set; } = new();
}

public class WorkflowComponentResponseModel
{
    public string Id { get; set; }
    public string DataType { get; set; }
    public StepType Type { get; set; }
    public JObject Inputs { get; set; }
    public JObject Outputs { get; set; }
}