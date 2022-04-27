using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TextTemplating;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using OneOf;
using None = OneOf.Types.None;

namespace Neo.ProxyGen;

public record ContractParameter(string Name, ContractType Type);
public record ContractEvent(string Name, IReadOnlyList<ContractParameter> Parameters);
public record ContractMethod(string Name, IReadOnlyList<ContractParameter> Parameters, OneOf<ContractType, None> ReturnType);

public record Contract
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<ContractMethod> Methods { get; init; } = Array.Empty<ContractMethod>();
    public IReadOnlyList<ContractEvent> Events { get; init; } = Array.Empty<ContractEvent>();

    public static Contract FromManifest(ContractManifest manifest, DebugInfo? debugInfo)
    {
        var debugMethods = debugInfo?.Methods ?? Array.Empty<DebugInfo.Method>();
        var debugEvents = debugInfo?.Events ?? Array.Empty<DebugInfo.Event>();

        var methods = new List<ContractMethod>();
        foreach (var method in manifest.Abi.Methods)
        {
            var @params = debugMethods.TryFind(m => m.Name.Equals(method.Name), out var debugMethod)
                ? debugMethod.Parameters.Select(p => new ContractParameter(p.Name, p.Type))
                : method.Parameters.Select(p => new ContractParameter(p.Name, ConvertContractParameterType(p.Type)));
            OneOf<ContractType, None> @return = method.ReturnType == ContractParameterType.Void
                ? default(None)
                : ConvertContractParameterType(method.ReturnType);
            methods.Add(new ContractMethod(method.Name, @params.ToArray(), @return));
        }

        var events = new List<ContractEvent>();
        foreach (var @event in manifest.Abi.Events)
        {
            var @params = debugEvents.TryFind(e => e.Name.Equals(@event.Name), out var debugEvent)
                ? debugEvent.Parameters.Select(p => new ContractParameter(p.Name, p.Type))
                : @event.Parameters.Select(p => new ContractParameter(p.Name, ConvertContractParameterType(p.Type)));
            events.Add(new ContractEvent(@event.Name, @params.ToArray()));
        }

        return new Contract
        {
            Name = manifest.Name,
            Methods = methods,
            Events = events,
        };
    }

    // TODO: use version of ConvertContractParameterType from lib-bctk
    static ContractType ConvertContractParameterType(ContractParameterType type)
        => type switch
        {
            ContractParameterType.Any => ContractType.Unspecified,
            ContractParameterType.Array => new ArrayContractType(ContractType.Unspecified),
            ContractParameterType.Boolean => PrimitiveContractType.Boolean,
            ContractParameterType.ByteArray => PrimitiveContractType.ByteArray,
            ContractParameterType.Hash160 => PrimitiveContractType.Hash160,
            ContractParameterType.Hash256 => PrimitiveContractType.Hash256,
            ContractParameterType.Integer => PrimitiveContractType.Integer,
            ContractParameterType.InteropInterface => InteropContractType.Unknown,
            ContractParameterType.Map => new MapContractType(PrimitiveType.ByteArray, ContractType.Unspecified),
            ContractParameterType.PublicKey => PrimitiveContractType.PublicKey,
            ContractParameterType.Signature => PrimitiveContractType.Signature,
            ContractParameterType.String => PrimitiveContractType.String,
            ContractParameterType.Void => throw new NotSupportedException("Void not supported ContractType"),
            _ => ContractType.Unspecified
        };
}

[Command("neo-proxygen", Description = "Neo N3 smart contract runner for unit testing", UsePagerForHelpText = false)]
[VersionOptionFromMember("--version", MemberName = nameof(GetVersion))]
class Program
{
    static Task<int> Main(string[] args)
    {
        var services = new ServiceCollection()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IConsole>(PhysicalConsole.Singleton)
            .BuildServiceProvider();

        var app = new CommandLineApplication<Program>();
        app.Conventions
            .UseDefaultConventions()
            .UseConstructorInjection(services);

        return app.ExecuteAsync(args);
    }

    internal static string GetVersion() => ThisAssembly.AssemblyInformationalVersion;

    [Argument(0)]
    [Required]
    internal string Manifest { get; set; } = string.Empty;

    [Argument(1)]
    [Required]
    internal string Template { get; set; } = string.Empty;

    [Option]
    internal string DebugInfo { get; set; } = string.Empty;

    public int OnExecute(CommandLineApplication app, IConsole console)
    {
        return 0;
    }
    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
    {
        var fileSystem = new FileSystem();
        try
        {
            var mainfestText = await fileSystem.File.ReadAllTextAsync(DebugInfo).ConfigureAwait(false);
            var manifest = ContractManifest.Parse(mainfestText);
            DebugInfo? debugInfo = null;

            if (!string.IsNullOrEmpty(DebugInfo))
            {
                debugInfo = (await Neo.BlockchainToolkit.Models.DebugInfo.LoadAsync(DebugInfo))
                    .Match<DebugInfo?>(di => di, _ => null);
            }

            var contract = Contract.FromManifest(manifest, debugInfo);
            var template = await fileSystem.File.ReadAllTextAsync(Template);

            var host = new CustomHost(fileSystem);
            var engine = new Mono.TextTemplating.TemplatingEngine();
            var result = engine.ProcessTemplate(template, host);

            foreach (CompilerError error in host.Errors)
            {
                Console.WriteLine(error.ToString());
            }

            return 0;
        }
        catch (Exception ex)
        {
            await app.Error.WriteLineAsync(ex.Message);
            return 1;
        }
    }
}

#nullable disable
class CustomHost : ITextTemplatingEngineHost
{
    readonly IFileSystem fileSystem;

    public CustomHost(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
        this.StandardAssemblyReferences = new string[] { typeof(System.Uri).Assembly.Location };
        this.StandardImports = new string[] { "System " };
    }

    public string FileExtension { get; private set; } = ".txt";
    public Encoding FileEncoding { get; private set; } = Encoding.UTF8;
    public CompilerErrorCollection Errors { get; private set; } = new();
    public string TemplateFile { get; } = string.Empty;
    public IList<string> StandardAssemblyReferences { get; }
    public IList<string> StandardImports { get; }

    public bool LoadIncludeText(string requestFileName, out string content, out string location)
    {
        location = string.Empty;

        if (fileSystem.File.Exists(requestFileName))
        {
            content = fileSystem.File.ReadAllText(requestFileName);
            return true;
        }

        content = string.Empty;
        return false;
    }

    public object GetHostOption(string optionName)
    {
        if (optionName == "CacheAssemblies") return true;
        return null;
    }

    public string ResolveAssemblyReference(string assemblyReference)
    {
        if (fileSystem.File.Exists(assemblyReference))
        {
            return assemblyReference;
        }

        var templateDir = fileSystem.Path.GetDirectoryName(TemplateFile);
        var candidate = fileSystem.Path.Combine(templateDir, assemblyReference);
        if (fileSystem.File.Exists(candidate))
        {
            return candidate;
        }

        return string.Empty;
    }

    public Type ResolveDirectiveProcessor(string processorName)
    {
        throw new Exception("Directive Processor not found");
    }
    
    public string ResolvePath(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        if (fileSystem.File.Exists(path))
        {
            return path;
        }

        var templateDir = fileSystem.Path.GetDirectoryName(TemplateFile);
        var candidate = fileSystem.Path.Combine(templateDir, path);
        if (fileSystem.File.Exists(candidate))
        {
            return candidate;
        }

        return path;
    }

    public string ResolveParameterValue(string directiveId, string processorName, string parameterName)
    {
        if (directiveId is null) throw new ArgumentNullException(nameof(directiveId));
        if (processorName is null) throw new ArgumentNullException(nameof(processorName));
        if (parameterName is null) throw new ArgumentNullException(nameof(parameterName));

        return string.Empty;
    }

    public void SetFileExtension(string extension)
    {
        FileExtension = extension;
    }

    public void SetOutputEncoding(Encoding encoding, bool fromOutputDirective)
    {
        FileEncoding = encoding;
    }

    public void LogErrors(CompilerErrorCollection errors)
    {
        Errors = errors;
    }
}
#nullable restore