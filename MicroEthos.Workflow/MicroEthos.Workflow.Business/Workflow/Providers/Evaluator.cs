using Jint;

namespace MicroEthos.Workflow.Business.Workflow.Providers;

internal static class Evaluator
{
    public static Engine Engine { get; } = new();
}