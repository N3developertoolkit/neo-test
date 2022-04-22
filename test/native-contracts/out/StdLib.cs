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
    [System.ComponentModel.Description("StdLib")]
    interface StdLib {
        System.Numerics.BigInteger atoi(string value);
        System.Numerics.BigInteger atoi(string value, System.Numerics.BigInteger @base);
        byte[] base58CheckDecode(string s);
        string base58CheckEncode(byte[] data);
        byte[] base58Decode(string s);
        string base58Encode(byte[] data);
        byte[] base64Decode(string s);
        string base64Encode(byte[] data);
        object deserialize(byte[] data);
        string itoa(System.Numerics.BigInteger value);
        string itoa(System.Numerics.BigInteger value, System.Numerics.BigInteger @base);
        object jsonDeserialize(byte[] json);
        byte[] jsonSerialize(object item);
        System.Numerics.BigInteger memoryCompare(byte[] str1, byte[] str2);
        System.Numerics.BigInteger memorySearch(byte[] mem, byte[] value);
        System.Numerics.BigInteger memorySearch(byte[] mem, byte[] value, System.Numerics.BigInteger start);
        System.Numerics.BigInteger memorySearch(byte[] mem, byte[] value, System.Numerics.BigInteger start, bool backward);
        byte[] serialize(object item);
        Neo.VM.Types.Array stringSplit(string str, string separator);
        Neo.VM.Types.Array stringSplit(string str, string separator, bool removeEmptyEntries);
    }
}
