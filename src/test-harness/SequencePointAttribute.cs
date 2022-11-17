using System;

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
    public class SequencePointAttribute : Attribute
    {
        public string FileName { get; } = string.Empty;
        public uint Address { get; }
        public uint StartLine { get; }
        public uint StartColumn { get; }
        public uint EndLine { get; }
        public uint EndColumn { get; }

        public SequencePointAttribute(string fileName, uint address, uint startLine, uint startColumn, uint endLine, uint endColumn)
        {
            FileName = fileName;
            Address = address;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }
    }
}
