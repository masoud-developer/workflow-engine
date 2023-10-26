using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using MicroEthos.Common.Contracts;

namespace MicroEthos.Workflow.Business.Workflow.Modules;

public class WorkflowModuleLoader
{
    private readonly IMicroEthosLogger _logger;

    private readonly ConcurrentDictionary<string, WorkflowModuleAssemblyContext> _modules = new();

    public WorkflowModuleLoader(IMicroEthosLogger logger)
    {
        _logger = logger;
    }

    public Assembly? Load(string moduleName, string version, byte[] moduleAssemblyBuffer)
    {
        var context = new WorkflowModuleAssemblyContext();
        var assembly = context.Load(moduleAssemblyBuffer);
        var res = _modules.TryAdd($"{moduleName}@{version}", context);
        return !res ? null : assembly;
    }

    public bool Unload(string moduleName, string version)
    {
        var canGet = _modules.TryGetValue($"{moduleName}@{version}", out var module);
        if (!canGet)
            return false;

        lock (_modules)
        {
            module?.Unload();
            _modules.TryRemove($"{moduleName}@{version}", out module);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        return true;
    }

    #region Assembly Context

    private class WorkflowModuleAssemblyContext : AssemblyLoadContext
    {
        public WorkflowModuleAssemblyContext() : base(true)
        {
        }

        public Assembly Load(byte[] assemblyBuffer)
        {
            using var ms = new MemoryStream(assemblyBuffer);
            var assembly = LoadFromStream(ms);
            ms.Close();
            return assembly;
        }
    }

    #endregion
}