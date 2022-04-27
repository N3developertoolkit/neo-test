using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TextTemplating;
using Mono.TextTemplating;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract.Manifest;

namespace Neo.ProxyGen;

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

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, IFileSystem fileSystem)
    {
        try
        {
            var filename = fileSystem.Path.GetFileName(Manifest);
            var index = filename.IndexOf('.');
            filename = index < 0 ? filename : filename.Substring(0, index);
            var nefPath = fileSystem.Path.Combine(
                fileSystem.Path.GetDirectoryName(Manifest), $"{filename}.nef");

            var mainfestText = await fileSystem.File.ReadAllTextAsync(Manifest).ConfigureAwait(false);
            var manifest = ContractManifest.Parse(mainfestText);
            var _debugInfo = await Neo.BlockchainToolkit.Models.DebugInfo.LoadAsync(nefPath);
            DebugInfo? debugInfo = _debugInfo.Match<DebugInfo?>(di => di, _ => null);

            var contract = Contract.FromManifest(manifest, debugInfo);
            var template = await fileSystem.File.ReadAllTextAsync(Template);

            var generator = new TemplateGenerator();
            generator.Refs.Add(typeof (Contract).Assembly.Location);
            generator.Refs.Add(typeof (ContractType).Assembly.Location);
            generator.Refs.Add(typeof(OneOf.IOneOf).Assembly.Location);
            generator.GetOrCreateSession().Add("Contract", contract);
            string outname = string.Empty;
            var foo = generator.ProcessTemplate(Template, template, ref outname, out var content);
            // var host = new CustomHost(fileSystem);
            // var engine = new Mono.TextTemplating.TemplatingEngine();
            // var result = engine.ProcessTemplate(template, host);

            // foreach (CompilerError error in host.Errors)
            // {
            //     Console.WriteLine(error.ToString());
            // }

            if (foo)
            {
                Console.WriteLine(content);
            }
            else
            {
                foreach (var error in generator.Errors)
                {
                    Console.WriteLine(error);
                }
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