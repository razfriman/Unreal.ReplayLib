using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models;

public struct FQuat : IProperty
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    //bool FQuat::NetSerialize(FArchive& Ar, class UPackageMap*, bool& bOutSuccess)
    public void Serialize(UnrealBinaryReader reader)
    {
        X = reader.ReadSingle();
        Y = reader.ReadSingle();
        Z = reader.ReadSingle();
        W = reader.ReadSingle();
    }
    
    public void SerializeNet(UnrealBinaryReader reader)
    {
        X = reader.ReadSingle();
        Y = reader.ReadSingle();
        Z = reader.ReadSingle();

        var xyzMagSquared = X * X + Y * Y + Z * Z;
        var wSquared = 1.0f - xyzMagSquared;
    }
}