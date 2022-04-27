using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Neo.ProxyGen;

[Command("neo-proxygen", Description = "Neo N3 smart contract runner for unit testing", UsePagerForHelpText = false)]
[VersionOptionFromMember("--version", MemberName = nameof(GetVersion))]
class Program
{
    [Argument(0)]
    [Required]
    internal string ManifestFile { get; set; } = string.Empty;

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

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, IFileSystem fileSystem)
    {
        try
        {
            return 0;
        }
        catch (Exception ex)
        {
            await app.Error.WriteLineAsync(ex.Message);
            return 1;
        }
    }

}