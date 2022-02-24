using System;
using System.IO;
using System.Linq;

namespace build_tasks
{
    static class Utility
    {
        public static Stream GetResourceStream(string name)
        {
            var assembly = typeof(Utility).Assembly;
            var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException("", name);
            return assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException("", name);
        }

        public static string GetResource(string name)
        {
            using var resource = GetResourceStream(name);
            using var streamReader = new System.IO.StreamReader(resource);
            return streamReader.ReadToEnd();
        }
    }
}
