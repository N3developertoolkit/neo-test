using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using OneOf;

namespace NeoTestHarness
{
    public class TestApplicationEngine : ApplicationEngine
    {
        public static Block CreateDummyBlock(DataCache snapshot, ProtocolSettings? settings = null)
        {
            settings ??= ProtocolSettings.Default;
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            var currentBlock = NativeContract.Ledger.GetBlock(snapshot, hash);

            return new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = hash,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = currentBlock.Timestamp + settings.MillisecondsPerBlock,
                    Index = currentBlock.Index + 1,
                    NextConsensus = currentBlock.NextConsensus,
                    Witness = new Witness
                    {
                        InvocationScript = Array.Empty<byte>(),
                        VerificationScript = Array.Empty<byte>()
                    },
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        public static Transaction CreateTestTransaction(UInt160 signerAccount, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            => CreateTestTransaction(new Signer
            {
                Account = signerAccount,
                Scopes = witnessScope,
                AllowedContracts = Array.Empty<UInt160>(),
                AllowedGroups = Array.Empty<Neo.Cryptography.ECC.ECPoint>()
            });

        public static Transaction CreateTestTransaction(Signer? signer = null) => new()
        {
            Nonce = (uint)new Random().Next(),
            Script = Array.Empty<byte>(),
            Signers = signer == null ? Array.Empty<Signer>() : new[] { signer },
            Attributes = Array.Empty<TransactionAttribute>(),
            Witnesses = Array.Empty<Witness>(),
        };

        record BranchInstructionInfo(UInt160 ContractHash, int InstructionPointer, int BranchOffset);

        readonly Dictionary<UInt160, OneOf<ContractState, Script>> executedScripts = new();
        readonly Dictionary<UInt160, Dictionary<int, int>> hitMaps = new();
        readonly Dictionary<UInt160, Dictionary<int, (int branchCount, int continueCount)>> branchMaps = new();
        BranchInstructionInfo? branchInstructionInfo = null;

        public new event EventHandler<LogEventArgs>? Log;
        public new event EventHandler<NotifyEventArgs>? Notify;

        public IReadOnlyDictionary<UInt160, OneOf<ContractState, Script>> ExecutedScripts => executedScripts;

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings? settings = null)
            : this(snapshot, container: null, settings: settings)
        {
        }

        public TestApplicationEngine(DataCache snapshot, Transaction transaction, ProtocolSettings? settings = null)
            : this(snapshot, container: transaction, settings: settings)
        {
        }

        public TestApplicationEngine(DataCache snapshot, Signer signer, ProtocolSettings? settings = null)
            : this(snapshot, container: CreateTestTransaction(signer), settings: settings)
        {
        }

        public TestApplicationEngine(DataCache snapshot, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry, ProtocolSettings? settings = null)
            : this(snapshot, container: CreateTestTransaction(signer, witnessScope), settings: settings)
        {
        }

        // for backwards compat
        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            : this(snapshot, container: CreateTestTransaction(signer, witnessScope), settings: settings)
        {
        }

        // for backwards compat
        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings, Transaction transaction)
            : this(snapshot, container: transaction, settings: settings)
        {
        }

        // for backwards compat
        public TestApplicationEngine(TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas, IDiagnostic? diagnostic = null)
            : this(snapshot, trigger, container, persistingBlock, settings, gas, diagnostic)
        {
        }

        public TestApplicationEngine(DataCache snapshot, TriggerType? trigger = null, IVerifiable? container = null, Block? persistingBlock = null, ProtocolSettings? settings = null, long? gas = null, IDiagnostic? diagnostic = null)
            : base(trigger ?? TriggerType.Application, container ?? CreateTestTransaction(), snapshot, persistingBlock, settings ?? ProtocolSettings.Default, gas ?? TestModeGas,diagnostic)
        {
            ApplicationEngine.Log += OnLog;
            ApplicationEngine.Notify += OnNotify;
        }

        public override void Dispose()
        {
            ApplicationEngine.Log -= OnLog;
            ApplicationEngine.Notify -= OnNotify;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public IReadOnlyDictionary<int, int> GetHitMap(UInt160 contractHash)
        {
            if (hitMaps.TryGetValue(contractHash, out var hitMap))
            {
                return hitMap;
            }

            return ImmutableDictionary<int, int>.Empty;
        }

        public IReadOnlyDictionary<int, (int branchCount, int continueCount)> GetBranchMap(UInt160 contractHash)
        {
            if (branchMaps.TryGetValue(contractHash, out var branchMap))
            {
                return branchMap;
            }

            return ImmutableDictionary<int, (int, int)>.Empty;
        }

        private void OnLog(object? sender, LogEventArgs e)
        {
            if (ReferenceEquals(this, sender))
            {
                Log?.Invoke(sender, e);
            }
        }

        private void OnNotify(object? sender, NotifyEventArgs e)
        {
            if (ReferenceEquals(this, sender))
            {
                Notify?.Invoke(sender, e);
            }
        }

        public override VMState Execute()
        {
            return base.Execute();
        }

        protected override void LoadContext(ExecutionContext context)
        {
            base.LoadContext(context);

            var ecs = context.GetState<ExecutionContextState>();
            if (ecs.ScriptHash is null) return;
            if (!executedScripts.ContainsKey(ecs.ScriptHash))
            {
                OneOf<ContractState, Script> value = ecs.Contract is null ? context.Script : ecs.Contract;
                executedScripts.Add(ecs.ScriptHash, value);
            }
        }

        protected override void PreExecuteInstruction(Instruction instruction)
        {
            base.PreExecuteInstruction(instruction);

            if (CurrentContext is null) return;
            var hash = CurrentContext.GetScriptHash();
            if (hash is null) return;

            var ip = CurrentContext.InstructionPointer;
            var offset = ip + GetBranchOffset(instruction);
            branchInstructionInfo = offset > 0
                ? new BranchInstructionInfo(hash, ip, offset)
                : null;

            if (!hitMaps.TryGetValue(hash, out var hitMap))
            {
                hitMap = new();
                hitMaps.Add(hash, hitMap);
            }

            hitMap[ip] = hitMap.TryGetValue(ip, out var value) 
                ? value + 1 
                : 1;

            static int GetBranchOffset(Instruction instruction)
                => instruction.OpCode switch
                {
                    OpCode.JMPIF_L or OpCode.JMPIFNOT_L or
                    OpCode.JMPEQ_L or OpCode.JMPNE_L or
                    OpCode.JMPGT_L or OpCode.JMPGE_L or
                    OpCode.JMPLT_L or OpCode.JMPLE_L => instruction.TokenI32,
                    OpCode.JMPIF or OpCode.JMPIFNOT or
                    OpCode.JMPEQ or OpCode.JMPNE or
                    OpCode.JMPGT or OpCode.JMPGE or
                    OpCode.JMPLT or OpCode.JMPLE => instruction.TokenI8,
                    _ => 0
                };
        }

        protected override void PostExecuteInstruction(Instruction instruction)
        {
            base.PostExecuteInstruction(instruction);

            if (CurrentContext is null) return;

            if (branchInstructionInfo is not null)
            {
                var (hash, ip, offset) = branchInstructionInfo;
                if (!branchMaps.TryGetValue(hash, out var branchMap))
                {
                    branchMap = new();
                    branchMaps.Add(hash, branchMap);
                }

                (int branchCount, int continueCount) hitCount = branchMap.TryGetValue(ip, out var value) ? value : (0,0);
                hitCount = CurrentContext.InstructionPointer == offset
                    ? (hitCount.branchCount + 1, hitCount.continueCount)
                    : CurrentContext.InstructionPointer == ip
                        ? (hitCount.branchCount, hitCount.continueCount + 1)
                        : throw new InvalidOperationException($"Unexpected InstructionPointer {CurrentContext.InstructionPointer}");
                branchMap[ip] = hitCount;
            }
        }
    }
}

