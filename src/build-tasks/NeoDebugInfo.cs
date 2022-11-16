using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neo.BuildTasks
{
    public class NeoDebugInfo
    {
        public class Method
        {
            public string Id { get; set; } = "";
            public string Namespace { get; set; } = "";
            public string Name { get; set; } = "";
            public (int Start, int End) Range { get; set; }
            public IReadOnlyList<SequencePoint> SequencePoints { get; set; } = Array.Empty<SequencePoint>();
        }

        public class SequencePoint
        {
            public int Address { get; set; }
            public int Document { get; set; }
            public (int Line, int Column) Start { get; set; }
            public (int Line, int Column) End { get; set; }
        }

        public string Hash { get; set; } = "";
        public IReadOnlyList<string> Documents { get; set; } = Array.Empty<string>();
        public IReadOnlyList<Method> Methods { get; set; } = Array.Empty<Method>();


        // public IEnumerable<()

        public static NeoDebugInfo? TryLoad(string? debugInfoPath)
        {
            if (string.IsNullOrEmpty(debugInfoPath)) return null;

            try
            {
                using var fileStream = File.OpenRead(debugInfoPath);
                using var archive = new ZipArchive(fileStream);
                using var stream = archive.Entries[0].Open();
                return Load(stream);
            }
            catch {}

            try
            {
                using var fileStream = File.OpenRead(debugInfoPath);
                return Load(fileStream);
            }
            catch {}

            return null;
        }

        static NeoDebugInfo Load(Stream stream)
        {
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            var json = SimpleJSON.JSON.Parse(text) ?? throw new InvalidOperationException();
            return FromDebugInfoJson(json);
        }

        public static NeoDebugInfo FromDebugInfoJson(SimpleJSON.JSONNode json)
        {
            // TODO: parse events and static variables
            var hash = json["hash"].Value;
            var documents = json["documents"].Linq.Select(kvp => kvp.Value.Value);
            var methods = json["methods"].Linq.Select(kvp => MethodFromJson(kvp.Value));

            return new NeoDebugInfo
            {
                Hash = hash,
                Documents = documents.ToArray(),
                Methods = methods.ToArray()
            };
        }

        static Method MethodFromJson(SimpleJSON.JSONNode json)
        {
            // TODO: parse return, params and variables
            var id = json["id"].Value;
            var (@namespace, name) = NameFromJson(json["name"]);
            var range = RangeFromJson(json["range"]);
            var sequencePoints = json["sequence-points"].Linq.Select(kvp => SequencePointFromJson(kvp.Value));

            return new Method
            {
                Id = id,
                Namespace = @namespace,
                Name = name,
                Range = range,
                SequencePoints = sequencePoints.ToArray(),
            };
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

        static readonly Regex spRegex = new(@"^(\d+)\[(-?\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$");

        static SequencePoint SequencePointFromJson(SimpleJSON.JSONNode json)
        {
            var match = spRegex.Match(json.Value);
            if (match.Groups.Count != 7) throw new FormatException($"Invalid Sequence Point \"{json.Value}\"");

            var address = int.Parse(match.Groups[1].Value);
            var document = int.Parse(match.Groups[2].Value);
            var start = (int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));
            var end = (int.Parse(match.Groups[5].Value), int.Parse(match.Groups[6].Value));

            return new SequencePoint
            {
                Address = address,
                Document = document,
                Start = start,
                End = end
            };
        }
    }
}
