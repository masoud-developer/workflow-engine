namespace MicroEthos.Workflow.Business.Workflow.Steps.Utils;

public static class Utils
{
    public static Type GetStepType(string stepType)
    {
        var segments = stepType.Split(",");
        var typeName = segments.First().Trim();
        var assemblyName = segments.Last().Trim();
        var assembly = AppDomain.CurrentDomain.GetAssemblies().First(ass => ass.GetName().Name == assemblyName);
        return assembly.GetTypes().First(t => t.FullName == typeName);
    }
}