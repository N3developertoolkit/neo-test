using System;
using System.IO;
using Neo;

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class WorkNetConfigAttribute : Attribute
    {
        public string RpcUri { get; init; } = string.Empty;
        public string Height { get; init; } = string.Empty;
        public string DbPath { get; init; } = string.Empty;
        public ProtocolSettings Settings { get; init; }

        public WorkNetConfigAttribute(string uri, string height = "", string dbPath = "", string settingsPath = "")
        {
            RpcUri = uri;
            Height = height;

            if (dbPath == "tmp")
            {
                dbPath = Path.GetTempPath() + "data";
            }

            DbPath = dbPath;

            ProtocolSettings settings = null;
            if (File.Exists(settingsPath))
            {
                settings = ProtocolSettings.Load(settingsPath);
            }

            Settings = settings;
        }
    }
}

