using System;

namespace Unreal.ReplayLib.Exceptions;

public class CompressionException : Exception
{
    public CompressionException(string message) : base(message)
    {
    }
}