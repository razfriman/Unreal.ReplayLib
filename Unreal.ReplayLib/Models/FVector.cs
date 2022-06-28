using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models;

public struct FVector : IProperty
{
    public FVector(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public override string ToString() => $"X: {X}, Y: {Y}, Z: {Z}";

    public float Size() => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

    public void Serialize(UnrealBinaryReader reader)
    {
        X = reader.ReadSingle();
        Y = reader.ReadSingle();
        Z = reader.ReadSingle();
    }

    public static FVector operator -(FVector v1, FVector v2) => new(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);

    public static bool operator ==(FVector v1, FVector v2) =>
        Equals(v1.X, v2.X) && Equals(v1.Y, v2.Y) && Equals(v1.Z, v2.Z);

    public static bool operator !=(FVector v1, FVector v2) =>
        !Equals(v1.X, v2.X) || !Equals(v1.Y, v2.Y) || !Equals(v1.Z, v2.Z);

    public static FVector operator *(FVector v1, double val) =>
        new((float)(v1.X * val), (float)(v1.Y * val), (float)(v1.Z * val));

    public static FVector operator /(FVector v1, double val) =>
        new((float)(v1.X / val), (float)(v1.Y / val), (float)(v1.Z / val));

    public double DistanceTo(FVector? vector) => vector.HasValue
        ? Math.Sqrt(DistanceSquared(vector.Value))
        : -1;

    private double DistanceSquared(FVector vector) =>
        Math.Pow(vector.X - X, 2) + Math.Pow(vector.Y - Y, 2) + Math.Pow(vector.Z - Z, 2);

    public bool Equals(FVector other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    public override bool Equals(object obj) => obj is FVector other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
}