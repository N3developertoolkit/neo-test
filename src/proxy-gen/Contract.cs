using System;
using System.Collections.Generic;
using System.Linq;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using OneOf;
using None = OneOf.Types.None;

namespace Neo.ProxyGen;

public record ContractParameter(string Name, ContractType Type);
public record ContractEvent(string Name, IReadOnlyList<ContractParameter> Parameters);
public record ContractMethod(string Name, bool Safe, IReadOnlyList<ContractParameter> Parameters, OneOf<ContractType, None> ReturnType);

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
            methods.Add(new ContractMethod(method.Name, method.Safe, @params.ToArray(), @return));
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
#nullable restore