namespace MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;

[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field)]
public class StepOutputAttribute : Attribute
{
}