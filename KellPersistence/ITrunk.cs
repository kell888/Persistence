using System;
using System.Collections.Generic;
namespace KellPersistence
{
    public interface ITrunk
    {
        string Name { get; }
        bool DiskOnly { get; }
        bool Bulk(string outDir, bool input = false);
        int DataCount { get; }
        bool Delete(Guid id);
        bool Drop();
        ulong Identity { get; }
        bool IsDrop { get; }
        bool Rename(string newName);
        TrunkStatus Status { get; }
        bool Truncate();
        string TrunkPath { get; }
    }
}
