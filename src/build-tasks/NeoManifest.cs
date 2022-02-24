using System;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;

namespace Neo.BuildTasks
{
    // Parse Manifest ABI JSON manually using SimpleJSON to avoid taking dependency on neo.dll or a JSON parsing package
    class NeoManifest
    {
        public class Method
        {
            public string Name { get; set; } = "";
            public string ReturnType { get; set; } = "";
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; } = Array.Empty<(string Name, string Type)>();
        }

        public class Event
        {
            public string Name { get; set; } = "";
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; } = Array.Empty<(string Name, string Type)>();
        }

        public string Name { get; set; } = "";
        public IReadOnlyList<Method> Methods { get; set; } = Array.Empty<Method>();
        public IReadOnlyList<Event> Events { get; set; } = Array.Empty<Event>();

        public static void UpdateExtra(JSONNode manifest, JSONNode extra, string name)
        {
            if (manifest is null) throw new ArgumentNullException(nameof(manifest));
            if (extra is null) throw new ArgumentNullException(nameof(extra));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            manifest["extra"][name] = extra;
        }

        public static NeoManifest FromJson(JSONNode manifest)
        {
            if (manifest is null) throw new ArgumentNullException(nameof(manifest));

            var name = manifest["name"].Value;
            var methods = manifest["abi"]["methods"].Linq.Select(kvp => MethodFromJson(kvp.Value));
            var events = manifest["abi"]["events"].Linq.Select(kvp => EventFromJson(kvp.Value));

            if (string.IsNullOrEmpty(name)) throw new Exception("missing manifest name");

            return new NeoManifest
            {
                Name = name,
                Methods = methods.ToArray(),
                Events = events.ToArray()
            };

            static (string Name, string Type) ParamFromJson(JSONNode json)
            {
                if (json is null) throw new ArgumentNullException(nameof(json));

                var name = json["name"].Value;
                var type = json["type"].Value;

                if (string.IsNullOrEmpty(name)) throw new Exception("missing parameter name");
                if (string.IsNullOrEmpty(type)) throw new Exception("missing parameter type");

                return (name, type);
            }

            static Method MethodFromJson(JSONNode json)
            {
                if (json is null) throw new ArgumentNullException(nameof(json));

                var name = json["name"].Value;
                var returnType = json["returntype"].Value;
                var @params = json["parameters"].Linq.Select(kvp => ParamFromJson(kvp.Value));

                if (string.IsNullOrEmpty(name)) throw new Exception("missing method name");
                if (string.IsNullOrEmpty(returnType)) throw new Exception("missing method returnType");

                return new Method
                {
                    Name = name,
                    ReturnType = returnType,
                    Parameters = @params.ToArray()
                };
            }

            static Event EventFromJson(JSONNode json)
            {
                if (json is null) throw new ArgumentNullException(nameof(json));

                var name = json["name"].Value;
                var @params = json["parameters"].Linq.Select(kvp => ParamFromJson(kvp.Value));

                if (string.IsNullOrEmpty(name)) throw new Exception("missing event name");

                return new Event
                {
                    Name = name,
                    Parameters = @params.ToArray()
                };
            }
        }
    }
}
