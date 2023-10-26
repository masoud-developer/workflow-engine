using MicroEthos.Workflow.Abstraction.Enums;

namespace MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;

[AttributeUsage(
    AttributeTargets.Class)]
public class StepModuleAttribute : Attribute
{
    public StepModuleAttribute(string module, StepType type = StepType.Function)
    {
        Module = module;
        Type = type;
    }

    public string Module { get; set; }
    public StepType Type { get; set; }
}