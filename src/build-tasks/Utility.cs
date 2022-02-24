using System;
using System.IO;
using Microsoft.Build.Framework;
using SimpleJSON;

namespace Neo.BuildTasks
{
    static class Utility
    {
        public static JSONNode ReadJson(ITaskItem item) => ReadJson(item?.ItemSpec);

        public static JSONNode ReadJson(string? path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("", path);
            var text = File.ReadAllText(path) ?? throw new FileLoadException("failed to read text", path);
            return JSON.Parse(text) ?? throw new FileLoadException("failed to parse json", path);
        }

        public static void FileOperationWithRetry(Action operation)
        {
            const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);

            for (int retriesLeft = 6; retriesLeft > 0; retriesLeft--)
            {
                try
                {
                    operation();
                }
                catch (IOException ex) when (ex.HResult == ProcessCannotAccessFileHR && retriesLeft > 0)
                {
                    System.Threading.Tasks.Task.Delay(100).Wait();
                    continue;
                }
            }
        }
    }
}
