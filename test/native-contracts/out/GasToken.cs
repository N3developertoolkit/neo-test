//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Neo.Native {
    #if NETSTANDARD || NETFRAMEWORK || NETCOREAPP
    [System.CodeDom.Compiler.GeneratedCode("Neo.BuildTasks","3.2.11.29999")]
    #endif
    [System.ComponentModel.Description("GasToken")]
    interface GasToken {
        System.Numerics.BigInteger balanceOf(Neo.UInt160 account);
        System.Numerics.BigInteger decimals();
        string symbol();
        System.Numerics.BigInteger totalSupply();
        bool transfer(Neo.UInt160 @from, Neo.UInt160 to, System.Numerics.BigInteger amount, object data);
        interface Events {
            void Transfer(Neo.UInt160 @from, Neo.UInt160 to, System.Numerics.BigInteger amount);
        }
    }
}
