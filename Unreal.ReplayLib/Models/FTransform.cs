using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models;

public class FTransform : IProperty
{
    public FQuat Rotation { get; set; }
    public FVector Scale3D { get; set; }
    public FVector Translation { get; set; }

    public void Serialize(UnrealBinaryReader reader)
    {
        Rotation = new FQuat();
        Rotation.Serialize(reader);

        Scale3D = new FVector();
        Scale3D.Serialize(reader);

        Translation = new FVector();
        Translation.Serialize(reader);
    }
}