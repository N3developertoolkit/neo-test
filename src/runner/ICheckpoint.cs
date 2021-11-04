using System;
using Neo.Persistence;

namespace Neo.Test.Runner
{
    interface ICheckpoint : IDisposable
    {
        IReadOnlyStore Store { get; }
        ProtocolSettings Settings { get; }
    }
}
