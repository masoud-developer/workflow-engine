using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MicroEthos.Common.Contracts;
using MicroEthos.Common.Models;
using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Response;
using MicroEthos.Workflow.Business.Workflow.Modules;
using MicroEthos.Workflow.DataAccess.Repository;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using WorkflowCore.Interface;

namespace MicroEthos.Workflow.Business.Services;

public class ModuleService : IModuleService
{
    private readonly IMicroEthosLogger _logger;
    private readonly IWorkflowHost _workflowHost;
    private readonly IRepository<Modules> _moduleRepo;
    private readonly IRepository<WorkflowDefinitions> _workflowRepo;
    private readonly GeneralRepository<WorkflowInstance> _workflowInstanceRepo;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkflowModuleLoader _workflowModuleLoader;
    private static Semaphore _semaphore = new(1, 1);

    public ModuleService(IMicroEthosLogger logger,
        IRepository<Modules> moduleRepo,
        IRepository<WorkflowDefinitions> workflowRepo,
        IServiceProvider serviceProvider,
        IWorkflowHost workflowHost,
        WorkflowModuleLoader workflowModuleLoader, GeneralRepository<WorkflowInstance> workflowInstanceRepo)
    {
        _logger = logger;
        _moduleRepo = moduleRepo;
        _workflowRepo = workflowRepo;
        _serviceProvider = serviceProvider;
        _workflowModuleLoader = workflowModuleLoader;
        _workflowInstanceRepo = workflowInstanceRepo;
        _workflowHost = workflowHost;
    }

    public async Task<List<Modules>> LoadAll()
    {
        try
        {
            await _logger.Info("Loading dynamic modules ...");
            var modules = await _moduleRepo.ListAsync(m => !m.Deprecated);
            foreach (var module in modules)
                try
                {
                    // Assembly.Load(module.AssemblyFile);
                    _workflowModuleLoader.Load(module.Name, module.Version, module.AssemblyFile);
                    await _logger.Info($"{module.Name}({module.Version}) module loaded completely.");
                }
                catch (Exception e)
                {
                    await _logger.Error($"Can not load module {module.Name}({module.Version}).", e);
                }

            return modules;
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in load modules", e);
            return new List<Modules>();
        }
    }

    public async Task<OperationResult<bool>> Load(string name, string version, byte[] assemblyFile)
    {
        try
        {
            _workflowModuleLoader.Load(name, version, assemblyFile);
            return OperationResult<bool>.Success();
        }
        catch (Exception e)
        {
            await _logger.Error($"Error occured in loading module {name}({version})", e);
            return OperationResult<bool>.Fail();
        }
    }

    public async Task<OperationResult<bool>> Unload(string name, string version)
    {
        try
        {
            _workflowModuleLoader.Unload(name, version);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return OperationResult<bool>.Success();
        }
        catch (Exception e)
        {
            await _logger.Error($"Error occured in unloading module {name}({version})", e);
            return OperationResult<bool>.Fail();
        }
    }

    public async Task<OperationResult<List<WorkflowModuleResponse>>> List()
    {
        try
        {
            var modules = await _moduleRepo.ListAsync(null, m => m.AssemblyFile);
            var items = modules.OrderBy(s => s.Name)
                .ThenByDescending(s => s.Version).Select(s => new WorkflowModuleResponse
                {
                    Id = s.Id,
                    Name = s.Name,
                    Version = s.Version,
                    CreatedAt = s.CreatedAt,
                    RequestQueueName = s.Queues.RequestQueueName,
                    ResponseQueueName = s.Queues.ResponseQueueName,
                    EventQueueName = s.Queues.EventQueueName,
                    Loaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == s.AssemblyName)
                }).ToList();
            return OperationResult<List<WorkflowModuleResponse>>.Success(items);
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in listing module", e);
            return OperationResult<List<WorkflowModuleResponse>>.Fail();
        }
    }

    public async Task<OperationResult<Guid>> CreateAndLoadDynamically(WorkflowAddModuleModel model)
    {
        _semaphore.WaitOne();
        try
        {
            var allCode = string.Empty;
            model.Name = model.Name.Replace("-", "").Replace(".", "");
            var moduleHash = GenerateModuleHash(model);

            var existedModule =
                await _moduleRepo.GetAsync(m =>
                    m.Name == model.Name && m.Version == model.Version && !m.Deprecated);
            if (existedModule != null)
            {
                //TODO: this section must be remove in production time
                ///////////////////////////////////////////////////////
                // if (moduleHash == existedModule.Hash)
                // {
                //     await _logger.Info(
                //         $"not detected any schema change in module {model.Name} with version ({model.Version}).");
                //     _semaphore.Release();
                //     return OperationResult<Guid>.Success(existedModule.Id);
                // }
                // var workflows = await _workflowRepo.ListAsync(w => w.Raw.Contains(existedModule.AssemblyName), w => w.MetaData);
                // foreach (var workflow in workflows)
                // {
                //     try
                //     {
                //         _workflowHost.Registry.DeregisterWorkflow(workflow.Model.Id.ToString(), workflow.Model.Version);
                //         await _workflowInstanceRepo.DeleteAsync(i => i.WorkflowDefinitionId == workflow.Model!.Id.ToString());
                //         await _workflowRepo.DeleteAsync(workflow);
                //     }
                //     catch (Exception e)
                //     {
                //         await _logger.Error("Exception occured ...", e);
                //     }
                // }
                // await Unload(existedModule.Name, existedModule.Version);
                // await _moduleRepo.DeleteAsync(existedModule.Id);
                ///////////////////////////////////////////////////////
                
                //TODO: this section must be uncomment in production time
                await _logger.Error(
                    $"Can not create this module because module {model.Name} with version ({model.Version}) is exists.");
                _semaphore.Release();
                return OperationResult<Guid>.Fail();
            }

            //generate input and output models class from json schema
            var namespaces =
                new[]
                {
                    "System",
                    "System.IO",
                    "System.Net",
                    "System.Linq",
                    "System.Text",
                    "System.Text.RegularExpressions",
                    "System.Collections.Generic",
                    "System.Collections",
                    "MicroEthos.Workflow.Abstraction.Models.Workflow",
                    "MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes",
                    "WorkflowCore.Interface",
                    "WorkflowCore.Models",
                    "System.Threading.Tasks",
                    "Newtonsoft.Json",
                    "MicroEthos.Workflow.Business.Providers",
                    "MicroEthos.Workflow.Business.Workflow.Providers"
                };
            foreach (var component in model.Components)
            {
                component.Name = component.Name.Replace("-", "").Replace(".", "");
                var inputSchemaObject = JObject.Parse(component.InputSchema);
                var outputSchemaObject = JObject.Parse(component.OutputSchema);
                //generate input model class
                var stepRootNamespace =
                    $"MicroEthos.Workflow.Business.Workflow.Steps.Dynamic.{model.Name}_{model.Version.Replace(".", "")}.{component.Name}_Context";
                var inputSchema = await JsonSchema.FromJsonAsync(component.InputSchema);
                if (inputSchema.AllOf.Count == 0 && inputSchema.ActualProperties.Count == 0
                                                 && inputSchema.Properties.Count == 0)
                    continue;

                var inputGenerator = new CSharpGenerator(inputSchema, new CSharpGeneratorSettings
                {
                    Namespace = $"{stepRootNamespace}.Inputs"
                });
                var inputCsharpClass = inputGenerator.GenerateFile();

                //generate output model class
                var outputSchema = await JsonSchema.FromJsonAsync(component.OutputSchema);
                if (outputSchema.AllOf.Count == 0 && outputSchema.ActualProperties.Count == 0
                                                  && outputSchema.Properties.Count == 0)
                    continue;
                var outputGenerator = new CSharpGenerator(outputSchema, new CSharpGeneratorSettings
                {
                    Namespace = $"{stepRootNamespace}.Outputs"
                });
                var outputCsharpClass = outputGenerator.GenerateFile();

                allCode = string.Join(Environment.NewLine, allCode, inputCsharpClass, outputCsharpClass);

                //generate step code
                var stepCode = GenerateStepCode(stepRootNamespace, model.Name, component.Name,
                    inputSchemaObject["title"].ToString(), outputSchemaObject["title"].ToString(),
                    model.Queues.RequestQueueName, namespaces);
                allCode = string.Join(Environment.NewLine, allCode, stepCode);
            }

            //generate events steps codes
            foreach (var e in model.Events)
            {
                e.Name = e.Name.Replace("-", "").Replace(".", "");
                var outputSchemaObject = JObject.Parse(e.OutputSchema);

                //generate output model class
                var stepRootNamespace =
                    $"MicroEthos.Workflow.Business.Workflow.Steps.Dynamic.{model.Name}_{model.Version.Replace(".", "")}.{e.Name}_Context";
                var outputSchema = await JsonSchema.FromJsonAsync(e.OutputSchema);
                if (outputSchema.AllOf.Count == 0 && outputSchema.ActualProperties.Count == 0
                                                  && outputSchema.Properties.Count == 0)
                    continue;

                var outputGenerator = new CSharpGenerator(outputSchema, new CSharpGeneratorSettings
                {
                    Namespace = $"{stepRootNamespace}.Outputs"
                });
                var outputCsharpClass = outputGenerator.GenerateFile();

                allCode = string.Join(Environment.NewLine, allCode, outputCsharpClass);

                //generate event step code
                var eventStepCode = GenerateEventCode(stepRootNamespace, model.Name, e.Name,
                    outputSchemaObject["title"].ToString(), namespaces);
                allCode = string.Join(Environment.NewLine, allCode, eventStepCode);
            }
            //replace all string to string?
            allCode = allCode.Replace(" string ", " string? ");

            //compile all module code
            var stringText = SourceText.From(allCode, Encoding.UTF8);
            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(stringText,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10), string.Empty);

            var assemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                .ToList();
            assemblies.Add(Assembly.GetExecutingAssembly().Location);
            var referencedAssemblies =
                Assembly.GetExecutingAssembly().GetReferencedAssemblies().Select(s => s.Name);
            assemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies()
                .Where(p => !p.IsDynamic).Select(a => a?.Location)
                .Where(a => !string.IsNullOrEmpty(a) && !referencedAssemblies.Contains(a))!);
            var trustedAssembliesPaths = assemblies.Select(s => MetadataReference.CreateFromFile(s));

            //
            // var asmInfo = new StringBuilder();
            // asmInfo.AppendLine("using System.Reflection;");
            // asmInfo.AppendLine($"[assembly: AssemblyTitle(\"{dllName}\")]");
            // asmInfo.AppendLine("[assembly: AssemblyVersion(\"1.0.0\")]");
            // asmInfo.AppendLine("[assembly: AssemblyFileVersion(\"1.0.0\")]");
            //
            // var syntaxTree = CSharpSyntaxTree.ParseText(asmInfo.ToString(), encoding: Encoding.Default);
            //

            var dllName = $"{model.Name}_Module_{model.Version}";
            var compilation
                = CSharpCompilation.Create(dllName,
                    new[] { parsedSyntaxTree }, trustedAssembliesPaths,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                        .WithOverflowChecks(true).WithOptimizationLevel(OptimizationLevel.Release)
                        .WithUsings(namespaces));

            try
            {
                using (var ms = new MemoryStream())
                {
                    var result = compilation.Emit(ms);
                    if (!result.Success)
                    {
                        _semaphore.Release();
                        return OperationResult<Guid>.Fail();
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    var byteAssembly = ms.ToArray();

                    //Save Assembly
                    // ms.Seek(0, SeekOrigin.Begin);
                    // var savePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase).Substring(5)}/{dllName}.dll";
                    // var file = File.Open(savePath, FileMode.Create);
                    // ms.WriteTo(file);
                    // file.Close();
                    // ms.Close();

                    var module = new Modules
                    {
                        CreatedAt = DateTime.Now,
                        Name = model.Name,
                        AssemblyName = dllName,
                        AssemblyFile = byteAssembly,
                        Version = model.Version,
                        Deprecated = false,
                        Queues = model.Queues,
                        Hash = moduleHash
                    };
                    await _moduleRepo.AddAsync(module);

                    // Assembly.Load(byteAssembly);
                    _workflowModuleLoader.Load(module.Name, model.Version, byteAssembly);

                    //listen to response queue
                    await SendNotificationToNodes(module);

                    _semaphore.Release();
                    return OperationResult<Guid>.Success(module.Id);
                }
            }
            catch (Exception ex)
            {
                _semaphore.Release();
                await _logger.Error($"Error Occured in compiling module {model.Name}.", ex);
                return OperationResult<Guid>.Fail("Error occured when creating module.");
            }
        }
        catch (Exception e)
        {
            _semaphore.Release();
            await _logger.Error("Error Occured ...", e);
            return OperationResult<Guid>.Fail("Error occured when creating module.");
        }
    }
    
    private async Task SendNotificationToNodes(Modules module)
    {
        var queueConnectionPool = _serviceProvider.GetRequiredService<IPool<IQueueClient>>();
        using var connection = queueConnectionPool.Acquire();
        await connection.Enqueue(TopicNames.WorkflowModuleCreated, module);
    }

    private string GenerateModuleHash(WorkflowAddModuleModel module)
    {
        var componentsText = module.Components.Count == 0 ? string.Empty : module.Components.Select(a => $"{a.Name}{a.InputSchema}{a.OutputSchema}")
            .Aggregate((a, b) => $"{a}@@@{b}");
        var eventsText = module.Events.Count == 0 ? string.Empty : module.Events.Select(a => $"{a.Name}{a.OutputSchema}").Aggregate((a, b) => $"{a}@@@{b}");
        var text = $"{module.Name}@@{module.Version}@@{module.Queues.RequestQueueName}" +
                   $"@@{module.Queues.ResponseQueueName}@@{module.Queues.EventQueueName}" +
                   $"@@{componentsText}" +
                   $"@@{eventsText}";
        using var sha256Hash = SHA256.Create();
        var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(text));
        var builder = new StringBuilder();
        foreach (var t in bytes)
            builder.Append(t.ToString("x2"));
        return builder.ToString();
    }

    #region Roslyn Steps Code Generate

    /// <summary>
    ///     Generate function step code from coming inputs and outputs schema from microservice
    /// </summary>
    /// <param name="rootNameSpace"></param>
    /// <param name="moduleName"></param>
    /// <param name="stepName"></param>
    /// <param name="inputName"></param>
    /// <param name="outputName"></param>
    /// <param name="queueName"></param>
    /// <param name="namespaces"></param>
    /// <returns></returns>
    private string GenerateStepCode(string rootNameSpace, string moduleName, string stepName, string inputName,
        string outputName,
        string queueName, string[] namespaces)
    {
        // Create a namespace: (namespace MicroEthos.Workflow.Business.Workflow.Steps.Dynamic)
        var @namespace = SyntaxFactory
            .NamespaceDeclaration(
                SyntaxFactory.ParseName(rootNameSpace))
            .NormalizeWhitespace();

        // Add System using statement: (using System, etc ...)
        @namespace = @namespace.AddUsings(namespaces
            .Select(n => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(n))).ToArray());

        //  Create a class: (class {0}Step)
        var classDeclaration = SyntaxFactory.ClassDeclaration(stepName);

        // Add the public modifier: (public class {0}Step)
        classDeclaration = classDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

        // Inherit StepBodyAsync: ([StepModule(module:"${moduleName}") ]public class {0}Step : StepBodyAsync)
        classDeclaration = classDeclaration.AddBaseListTypes(
            SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("StepBodyAsync")));

        var attributeArgument = SyntaxFactory.AttributeArgument(
            null, SyntaxFactory.NameColon("module"),
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(moduleName)));

        classDeclaration = classDeclaration.AddAttributeLists(
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("StepModule"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(attributeArgument)))
            )).NormalizeWhitespace());

        // Create a string variable: (Abstraction.Contracts.Providers.IModulesClient _moduleClient;)
        var variableDeclaration = SyntaxFactory
            .VariableDeclaration(SyntaxFactory.ParseTypeName("Abstraction.Contracts.Providers.IModulesClient"))
            .AddVariables(SyntaxFactory.VariableDeclarator("_moduleClient"));

        // Create a field declaration: (private Abstraction.Contracts.Providers.IModulesClient _moduleClient;)
        var fieldDeclaration = SyntaxFactory.FieldDeclaration(variableDeclaration)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

        // Create Step Constructor
        var constructorBody = new StringBuilder();
        // constructorBody.AppendLine("_moduleClient = moduleClient;");
        constructorBody.AppendLine(
            "_moduleClient = (Abstraction.Contracts.Providers.IModulesClient)StaticServiceProvider.ServiceProvider.GetService(typeof(Abstraction.Contracts.Providers.IModulesClient));");
        var constructorDeclaration = SyntaxFactory.ConstructorDeclaration(stepName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            // .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("moduleClient"))
            //     .WithType(SyntaxFactory.ParseTypeName("Abstraction.Contracts.Providers.IModulesClient")))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(constructorBody.ToString())));

        // Create a Property for input: ([StepInput] public InputType Input { get; set; })
        var inputPropertyDeclaration = SyntaxFactory
            .PropertyDeclaration(SyntaxFactory.ParseTypeName($"Inputs.{inputName}"), inputName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

        inputPropertyDeclaration = inputPropertyDeclaration.AddAttributeLists(
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("StepInput")))).NormalizeWhitespace());

        // Create a Property for output: (public OutputType Output { get; set; })
        var outputPropertyDeclaration = SyntaxFactory
            .PropertyDeclaration(SyntaxFactory.ParseTypeName($"Outputs.{outputName}"), outputName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

        outputPropertyDeclaration = outputPropertyDeclaration.AddAttributeLists(
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("StepOutput")))).NormalizeWhitespace());

        // Create RunAsync method
        var methodDeclaration = SyntaxFactory
            .MethodDeclaration(SyntaxFactory.ParseTypeName("Task<ExecutionResult>"), "RunAsync")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.OverrideKeyword),
                SyntaxFactory.Token(SyntaxKind.AsyncKeyword))
            .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("context"))
                .WithType(SyntaxFactory.ParseTypeName("IStepExecutionContext")))
            .WithBody(SyntaxFactory.Block(
                // SyntaxFactory.ParseStatement(
                //     "((WorkflowStateModel)context.Workflow.Data).State.Items[$\"{{context.Step.ExternalId}}_JobId\"] = jobId;"),
                SyntaxFactory.ParseStatement(
                    "if (!context.ExecutionPointer.EventPublished) {"),
                SyntaxFactory.ParseStatement(
                    $"var jobId = await _moduleClient.CallModule(\"{queueName}\", \"{moduleName}\", \"{stepName}\", this.{inputName}, (WorkflowStateModel)context.Workflow.Data);"),
                SyntaxFactory.ParseStatement(
                    "return ExecutionResult.WaitForEvent(jobId, jobId, DateTime.Now); }"),
                SyntaxFactory.ParseStatement(
                    $"{outputName} = _moduleClient.PrepareOutput<Outputs.{outputName}>(context.ExecutionPointer.EventData);"),
                SyntaxFactory.ParseStatement("return ExecutionResult.Next();")
            ));

        // Add the field, the property and method to the class.
        classDeclaration =
            classDeclaration.AddMembers(fieldDeclaration, constructorDeclaration, inputPropertyDeclaration,
                outputPropertyDeclaration, methodDeclaration);

        // Add the class to the namespace.
        @namespace = @namespace.AddMembers(classDeclaration);

        // Normalize and get code as string.
        var code = @namespace
            .NormalizeWhitespace()
            .ToFullString();

        return code;
    }

    /// <summary>
    ///     Generate Event Step Code with coming step output schema from microservice
    /// </summary>
    /// <param name="rootNameSpace"></param>
    /// <param name="moduleName"></param>
    /// <param name="eventName"></param>
    /// <param name="outputName"></param>
    /// <param name="namespaces"></param>
    /// <returns></returns>
    private string GenerateEventCode(string rootNameSpace, string moduleName, string eventName, string outputName,
        string[] namespaces)
    {
        // Create a namespace: (namespace MicroEthos.Workflow.Business.Workflow.Events.Dynamic)
        var @namespace = SyntaxFactory
            .NamespaceDeclaration(
                SyntaxFactory.ParseName(rootNameSpace))
            .NormalizeWhitespace();

        // Add System using statement: (using System, etc ...)
        @namespace = @namespace.AddUsings(namespaces
            .Select(n => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(n))).ToArray());

        //  Create a class: (class {0}Event)
        var classDeclaration = SyntaxFactory.ClassDeclaration(eventName);

        // Add the public modifier: (public class {0}Event)
        classDeclaration = classDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

        // Inherit StepBodyAsync: ([StepModule(module:"${moduleName}") ]public class {0}Step : StepBodyAsync)
        classDeclaration = classDeclaration.AddBaseListTypes(
            SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("StepBodyAsync")));

        var attributeArgument = SyntaxFactory.AttributeArgument(
            null, SyntaxFactory.NameColon("module"),
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(moduleName)));

        var attributeTypeArgument = SyntaxFactory.AttributeArgument(
            null, SyntaxFactory.NameColon("type"),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("MicroEthos.Workflow.Abstraction.Enums.StepType"),
                SyntaxFactory.Token(SyntaxKind.DotToken),
                SyntaxFactory.IdentifierName("Event")
            ));

        classDeclaration = classDeclaration.AddAttributeLists(
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("StepModule"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(new[]
                            { attributeArgument, attributeTypeArgument })))
            )).NormalizeWhitespace());

        // Create a string variable: (Abstraction.Contracts.Providers.IModulesClient _moduleClient;)
        var variableDeclaration = SyntaxFactory
            .VariableDeclaration(SyntaxFactory.ParseTypeName("Abstraction.Contracts.Providers.IModulesClient"))
            .AddVariables(SyntaxFactory.VariableDeclarator("_moduleClient"));

        // Create a field declaration: (private Abstraction.Contracts.Providers.IModulesClient _moduleClient;)
        var fieldDeclaration = SyntaxFactory.FieldDeclaration(variableDeclaration)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

        // Create Event Step Constructor
        var constructorBody = new StringBuilder();
        // constructorBody.AppendLine("_moduleClient = moduleClient;");
        constructorBody.AppendLine(
            "_moduleClient = (Abstraction.Contracts.Providers.IModulesClient)StaticServiceProvider.ServiceProvider.GetService(typeof(Abstraction.Contracts.Providers.IModulesClient));");
        var constructorDeclaration = SyntaxFactory.ConstructorDeclaration(eventName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            // .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("moduleClient"))
            //     .WithType(SyntaxFactory.ParseTypeName("Abstraction.Contracts.Providers.IModulesClient")))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(constructorBody.ToString())));

        // Create a Property for output: (public OutputType Output { get; set; })
        var outputPropertyDeclaration = SyntaxFactory
            .PropertyDeclaration(SyntaxFactory.ParseTypeName($"Outputs.{outputName}"), outputName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

        outputPropertyDeclaration = outputPropertyDeclaration.AddAttributeLists(
            SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("StepOutput")))).NormalizeWhitespace());

        // Create RunAsync method
        var methodDeclaration = SyntaxFactory
            .MethodDeclaration(SyntaxFactory.ParseTypeName("Task<ExecutionResult>"), "RunAsync")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.OverrideKeyword),
                SyntaxFactory.Token(SyntaxKind.AsyncKeyword))
            .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("context"))
                .WithType(SyntaxFactory.ParseTypeName("IStepExecutionContext")))
            .WithBody(SyntaxFactory.Block(
                // SyntaxFactory.ParseStatement(
                //     "((WorkflowStateModel)context.Workflow.Data).State.Items[$\"{{context.Step.ExternalId}}_JobId\"] = jobId;"),
                SyntaxFactory.ParseStatement(
                    "if (!context.ExecutionPointer.EventPublished) {"),
                SyntaxFactory.ParseStatement(
                    $"return ExecutionResult.WaitForEvent(\"{eventName}\", \"{moduleName}\", DateTime.Now); }}"),
                SyntaxFactory.ParseStatement(
                    $"{outputName} = _moduleClient.PrepareOutput<Outputs.{outputName}>(context.ExecutionPointer.EventData);"),
                SyntaxFactory.ParseStatement("return ExecutionResult.Next();")
            ));

        // Add the field, the property and method to the class.
        classDeclaration =
            classDeclaration.AddMembers(fieldDeclaration, constructorDeclaration,
                outputPropertyDeclaration, methodDeclaration);

        // Add the class to the namespace.
        @namespace = @namespace.AddMembers(classDeclaration);

        // Normalize and get code as string.
        var code = @namespace
            .NormalizeWhitespace()
            .ToFullString();

        return code;
    }

    #endregion
}