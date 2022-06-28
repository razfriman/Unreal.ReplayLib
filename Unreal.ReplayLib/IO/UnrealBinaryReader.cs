using System.Buffers;
using System.Text;
using Unreal.ReplayLib.Models;
using Unreal.ReplayLib.Models.Enums;

namespace Unreal.ReplayLib.IO;

public class UnrealBinaryReader : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly MemoryStream _stream;

    public UnrealBinaryReader(byte[] buffer)
    {
        _stream = new MemoryStream(buffer);
        _reader = new BinaryReader(_stream);
    }

    public UnrealBinaryReader(MemoryStream stream)
    {
        _stream = stream;
        _reader = new BinaryReader(_stream);
    }

    public EngineNetworkVersionHistory EngineNetworkVersion { get; set; }
    public ReplayHeaderFlags ReplayHeaderFlags { get; set; }
    public NetworkVersionHistory NetworkVersion { get; set; }
    public ReplayVersionHistory ReplayVersion { get; set; }
    public NetworkReplayVersion NetworkReplayVersion { get; set; }
    public long Position => _stream.Position;
    public bool AtEnd() => _stream.Position >= _stream.Length;
    public bool CanRead(int count) => _stream.Position + count < _stream.Length;

    public bool ReadBoolean() => _reader.ReadBoolean();

    public byte ReadByte() => _reader.ReadByte();

    public T ReadByteAsEnum<T>() => (T)Enum.ToObject(typeof(T), ReadByte());

    public byte[] ReadBytes(int byteCount) => _reader.ReadBytes(byteCount);

    public byte[] ReadBytes(uint byteCount) => _reader.ReadBytes((int)byteCount);

    public string ReadBytesToString(int count)
    {
        if (count < 1024)
        {
            Span<byte> buffer = stackalloc byte[count];
            _reader.Read(buffer);
            return Convert.ToHexString(buffer);
        }
        else
        {
            var buffer = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                _reader.Read(buffer);
                return Convert.ToHexString(buffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public string ReadFString()
    {
        var length = ReadInt32();

        if (length == 0)
        {
            return string.Empty;
        }

        var isUnicode = length < 0;
        var encoding = isUnicode ? Encoding.Unicode : Encoding.Default;
        if (isUnicode)
        {
            length *= -2;
        }

        Span<byte> buffer = stackalloc byte[length];
        _reader.Read(buffer);
        return encoding.GetString(buffer).Trim(' ', '\0');
    }

    public void SkipFString()
    {
        var length = ReadInt32();
        length = length < 0 ? -2 * length : length;
        SkipBytes(length);
    }

    public string ReadGuid() => ReadBytesToString(16);

    public string ReadGuid(int size) => ReadBytesToString(size);

    public short ReadInt16() => _reader.ReadInt16();

    public int ReadInt32() => _reader.ReadInt32();

    public bool ReadInt32AsBoolean() => _reader.ReadInt32() == 1;

    public long ReadInt64() => _reader.ReadInt64();

    public uint ReadPackedUInt32()
    {
        uint value = 0;
        byte count = 0;
        var remaining = true;

        while (remaining)
        {
            var nextByte = ReadByte();
            remaining = (nextByte & 1) == 1; // Check 1 bit to see if theres more after this
            nextByte >>= 1; // Shift to get actual 7 bit value
            value += (uint)nextByte << (7 * count++); // Add to total value
        }

        return value;
    }

    public sbyte ReadSByte() => _reader.ReadSByte();

    public float ReadSingle() => _reader.ReadSingle();

    public (T, TU)[] ReadTupleArray<T, TU>(Func<T> func1, Func<TU> func2)
    {
        var count = ReadUInt32();
        var arr = new (T, TU)[count];
        for (var i = 0; i < count; i++)
        {
            arr[i] = (func1(), func2());
        }
        return arr;
    }

    public T[] ReadArray<T>(Func<T> func1)
    {
        var count = ReadUInt32();
        var arr = new T[count];
        for (var i = 0; i < count; i++)
        {
            arr[i] = func1.Invoke();
        }

        return arr;
    }

    public ushort ReadUInt16() => _reader.ReadUInt16();

    public uint ReadUInt32() => _reader.ReadUInt32();

    public bool ReadUInt32AsBoolean() => ReadUInt32() == 1u;

    public T ReadUInt32AsEnum<T>() => (T)Enum.ToObject(typeof(T), ReadUInt32());

    public ulong ReadUInt64() => _reader.ReadUInt64();

    public void Seek(long offset, SeekOrigin seekOrigin = SeekOrigin.Begin) =>
        _stream.Seek(offset, seekOrigin);

    public void SkipBytes(uint byteCount) => _stream.Seek(byteCount, SeekOrigin.Current);

    public void SkipBytes(int byteCount) => _stream.Seek(byteCount, SeekOrigin.Current);

    public FVector ReadQuantizedVector()
    {
        var a = ReadInt32();
        var dx = ReadInt32();
        var dy = ReadInt32();
        var dz = ReadInt32();
        var bias = 1 << (a + 1);
        var max = 1 << (a + 2);
        var scaleFactor = 1;
        var x = (float)(dx - bias) / scaleFactor;
        var y = (float)(dy - bias) / scaleFactor;
        var z = (float)(dz - bias) / scaleFactor;
        return new FVector(x, y, z);
    }

    public DateTimeOffset ReadDate() => DateTime.FromBinary(ReadInt64()).ToUniversalTime();

    public bool HasLevelStreamingFixes() => ReplayHeaderFlags.HasFlag(ReplayHeaderFlags.HasStreamingFixes);

    public bool HasDeltaCheckpoints() => ReplayHeaderFlags.HasFlag(ReplayHeaderFlags.DeltaCheckpoints);

    public bool HasGameSpecificFrameData() =>
        ReplayHeaderFlags.HasFlag(ReplayHeaderFlags.GameSpecificFrameData);

    public void Dispose() => Dispose(true);

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream?.Dispose();
            _reader?.Dispose();
        }
    }
}