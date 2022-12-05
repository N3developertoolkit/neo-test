using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neo.Collector.Models
{
    public partial struct NeoDebugInfo
    {
        public const string NEF_DBG_NFO_EXTENSION = ".nefdbgnfo";
        public const string DEBUG_JSON_EXTENSION = ".debug.json";

        public readonly Hash160 Hash;
        public readonly IReadOnlyList<string> Documents;
        public readonly IReadOnlyList<Method> Methods;

        public NeoDebugInfo(Hash160 hash, IReadOnlyList<string> documents, IReadOnlyList<Method> methods)
        {
            Hash = hash;
            Documents = documents;
            Methods = methods;
        }

        public static bool TryLoadContractDebugInfo(string nefPath, out NeoDebugInfo debugInfo)
        {
            if (string.IsNullOrEmpty(nefPath))
            {
                debugInfo = default;
                return false;
            }

            var nefdbgnfoPath = Path.ChangeExtension(nefPath, NEF_DBG_NFO_EXTENSION);
            if (TryLoadCompressed(nefdbgnfoPath, out debugInfo)) return true;

            var debugJsonPath = Path.ChangeExtension(nefPath, DEBUG_JSON_EXTENSION);
            if (TryLoadUncompressed(debugJsonPath, out debugInfo)) return true;

            return false;
        }

        static bool TryLoadCompressed(string debugInfoPath, out NeoDebugInfo debugInfo)
        {
            try
            {
                if (File.Exists(debugInfoPath))
                {
                    using (var zip = ZipStorer.Open(debugInfoPath, FileAccess.Read))
                    {
                        var dir = zip.ReadCentralDir();
                        zip.ExtractFile(dir[0], out byte[] buffer);
                        using (var stream = new MemoryStream(buffer))
                        {
                            debugInfo = Load(stream);
                            return true;
                        }
                    }
                }
            }
            catch { }

            debugInfo = default;
            return false;
        }

        static bool TryLoadUncompressed(string debugInfoPath, out NeoDebugInfo debugInfo)
        {
            try
            {
                if (File.Exists(debugInfoPath))
                {
                    using (var fileStream = File.OpenRead(debugInfoPath))
                    {
                        debugInfo = Load(fileStream);
                        return true;
                    }
                }
            }
            catch { }

            debugInfo = default;
            return false;
        }

        static NeoDebugInfo Load(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var text = reader.ReadToEnd();
                var json = SimpleJSON.JSON.Parse(text) ?? throw new InvalidOperationException();
                return FromDebugInfoJson(json);
            }
        }

        public static NeoDebugInfo FromDebugInfoJson(SimpleJSON.JSONNode json)
        {
            var hash = Hash160.TryParse(json["hash"].Value, out var _hash)
                ? _hash
                : throw new FormatException($"Invalid hash {json["hash"].Value}");
            var documents = json["documents"].Linq.Select(kvp => kvp.Value.Value);
            var methods = json["methods"].Linq.Select(kvp => MethodFromJson(kvp.Value));
            // TODO: parse events and static variables

            return new NeoDebugInfo(hash, documents.ToArray(), methods.ToArray());
        }

        static Method MethodFromJson(SimpleJSON.JSONNode json)
        {
            // TODO: parse return, params and variables
            var id = json["id"].Value;
            var (@namespace, name) = NameFromJson(json["name"]);
            var range = RangeFromJson(json["range"]);
            var @params = json["params"].Linq.Select(kvp => ParamFromJson(kvp.Value));
            var sequencePoints = json["sequence-points"].Linq.Select(kvp => SequencePointFromJson(kvp.Value));

            return new Method(id, @namespace, name, range, @params.ToArray(), sequencePoints.ToArray());
        }

        static Parameter ParamFromJson(SimpleJSON.JSONNode json)
        {
            var values = json.Value.Split(',');
            if (values.Length == 2 || values.Length == 3)
            {
                var index = values.Length == 3 
                    && int.TryParse(values[2], out var _index)
                    && _index >= 0 ? _index : -1;

                return new Parameter(values[0], values[1], index);
            }
            throw new FormatException($"invalid parameter \"{json.Value}\"");
        }

        static (string, string) NameFromJson(SimpleJSON.JSONNode json)
        {
            var values = json.Value.Split(',');
            return values.Length == 2
                ? (values[0], values[1])
                : throw new FormatException($"Invalid name '{json.Value}'");
        }

        static (int, int) RangeFromJson(SimpleJSON.JSONNode json)
        {
            var values = json.Value.Split('-');
            return values.Length == 2
                ? (int.Parse(values[0]), int.Parse(values[1]))
                : throw new FormatException($"Invalid range '{json.Value}'");
        }

        static readonly Regex spRegex = new Regex(@"^(\d+)\[(-?\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$");

        static SequencePoint SequencePointFromJson(SimpleJSON.JSONNode json)
        {
            var match = spRegex.Match(json.Value);
            if (match.Groups.Count != 7) throw new FormatException($"Invalid Sequence Point \"{json.Value}\"");

            var address = int.Parse(match.Groups[1].Value);
            var document = int.Parse(match.Groups[2].Value);
            var start = (int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));
            var end = (int.Parse(match.Groups[5].Value), int.Parse(match.Groups[6].Value));

            return new SequencePoint(address, document, start, end);
        }
    }
}
