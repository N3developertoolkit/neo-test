using System;
using System.Linq;
using System.Text.RegularExpressions;
using static Neo.BuildTasks.CSharpHelpers;

namespace Neo.BuildTasks
{
    public static class ContractGenerator
    {
        public static string GenerateContractInterface(NeoManifest manifest, string manifestFile, string contractNameOverride, string @namespace)
        {
            var manifestName = manifest.Name.Replace("\"", "\"\"");
            var contractName = string.IsNullOrEmpty(contractNameOverride)
                ? Regex.Replace(manifest.Name, "^.*\\.", string.Empty)
                : contractNameOverride;

            if (!IsValidTypeName(contractName) || contractName.Contains('.'))
            {
                throw new Exception($"\"{contractName}\" is not a valid C# type name");
            }

            var builder = new IndentedStringBuilder();

            builder.AppendLines($@"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

");

            if (@namespace.Length > 0)
            {
                builder.AppendLine($"namespace {@namespace} {{");
                builder.IncrementIndent();
            }
            builder.AppendLines($@"#if NETSTANDARD || NETFRAMEWORK || NETCOREAPP
[System.CodeDom.Compiler.GeneratedCode(""Neo.BuildTasks"",""{ThisAssembly.AssemblyFileVersion}"")]
#endif
");
            builder.AppendLine($"[System.ComponentModel.Description(@\"{manifestName}\")]");
            builder.AppendLine("#if TEST_HARNESS_ATTRIBUTES");
            builder.AppendLine($"[NeoTestHarness.Contract(@\"{manifestName}\", @\"{manifestFile}\")]");
            builder.AppendLine("#endif");
            builder.AppendLine($"interface {contractName} {{");
            builder.IncrementIndent();
            for (int i = 0; i < manifest.Methods.Count; i++)
            {
                var method = manifest.Methods[i];
                if (method.Name.StartsWith("_")) continue;

                builder.Append($"{ConvertParameterType(method.ReturnType)} {method.Name}(");
                builder.Append(string.Join(", ", method.Parameters.Select(p => $"{ConvertParameterType(p.Type)} {CreateEscapedIdentifier(p.Name)}")));
                builder.AppendLine(");");
            }

            if (manifest.Events.Count > 0)
            {
                builder.AppendLine("interface Events {");
                builder.IncrementIndent();
                for (int i = 0; i < manifest.Events.Count; i++)
                {
                    var @event = manifest.Events[i];
                    builder.Append($"void {@event.Name}(");
                    builder.Append(string.Join(", ", @event.Parameters.Select(p => $"{ConvertParameterType(p.Type)} {CreateEscapedIdentifier(p.Name)}")));
                    builder.AppendLine($");");
                }
                builder.DecrementIndent();
                builder.AppendLine("}");
            }

            builder.DecrementIndent();
            builder.AppendLine("}");

            if (@namespace.Length > 0)
            {
                builder.DecrementIndent();
                builder.AppendLine("}");
            }

            return builder.ToString();
        }

        static string ConvertParameterType(string parameterType)
        {
            switch (parameterType)
            {
                case "Any": return "object";
                case "Array": return "Neo.VM.Types.Array";
                case "Boolean": return "bool";
                case "ByteArray": return "byte[]";
                case "Hash160": return "Neo.UInt160";
                case "Hash256": return "Neo.UInt256";
                case "Integer": return "System.Numerics.BigInteger";
                case "InteropInterface": return "Neo.VM.Types.InteropInterface";
                case "PublicKey": return "Neo.Cryptography.ECC.ECPoint";
                case "Map": return "Neo.VM.Types.Map";
                case "Signature": return "Neo.VM.Types.ByteString";
                case "String": return "string";
                case "Void": return "void";
                default: throw new FormatException($"Invalid parameter type {parameterType}");
            };
        }
    }
}

