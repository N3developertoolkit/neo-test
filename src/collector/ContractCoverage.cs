using System;
using System.Collections.Generic;
using System.IO;
using Neo.Collector.Models;

namespace Neo.Collector
{
    class ContractCoverage
    {
        readonly string manifestPath;
        readonly NefFile nefFile;
        readonly NeoDebugInfo debugInfo;
        readonly Hash160 scriptHash;
        readonly IDictionary<uint, uint> hitMap = new Dictionary<uint, uint>();
        readonly IDictionary<uint, (uint branchCount, uint continueCount)> branchMap = new Dictionary<uint, (uint branchCount, uint continueCount)>();

        public Hash160 ScriptHash => scriptHash;
        public IReadOnlyDictionary<uint, uint> HitMap => (IReadOnlyDictionary<uint, uint>)hitMap;
        public IReadOnlyDictionary<uint, (uint branchCount, uint continueCount)> BranchMap => 
            (IReadOnlyDictionary<uint, (uint branchCount, uint continueCount)>)branchMap;

        public ContractCoverage(string manifestPath, NefFile nefFile, NeoDebugInfo debugInfo)
        {
            this.manifestPath = manifestPath;
            this.nefFile = nefFile;
            this.debugInfo = debugInfo;
            this.scriptHash = nefFile.CalculateScriptHash();
        }

        public void RecordHit(uint address)
        {
            var hitCount = hitMap.TryGetValue(address, out var value) ? value : 0;
            hitMap[address] = hitCount + 1;
        }

        public void RecordBranch(uint address, uint offsetAddress, uint branchResult)
        {
            RecordHit(address);
            var (branchCount, continueCount) = branchMap.TryGetValue(address, out var value)
                ? value : (0, 0);
            branchMap[address] = branchResult == address
                ? (branchCount, continueCount + 1)
                : branchResult == offsetAddress
                    ? (branchCount + 1, continueCount)
                    : throw new FormatException($"Branch result {branchResult} did not equal {address} or {offsetAddress}");
        }

        public static bool TryCreate(string manifestPath, out ContractCoverage value)
        {
            var dirname = Path.GetDirectoryName(manifestPath);
            var basename = Path.GetFileNameWithoutExtension(manifestPath); 
            
            if (manifestPath.EndsWith(".manifest.json"))
            {
                basename = Path.GetFileNameWithoutExtension(basename);
            }

            var nefPath = Path.Combine(dirname, $"{basename}.nef");
            if (NefFile.TryLoad(nefPath, out var nefFile)
                && NeoDebugInfo.TryLoadContractDebugInfo(nefPath, out var debugInfo))
            {
                value = new ContractCoverage(manifestPath, nefFile, debugInfo);
                return true;
            }

            value = null;
            return false;
        }
    }
}