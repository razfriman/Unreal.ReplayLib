namespace Unreal.ReplayLib.Exceptions;

public class ReplayException : Exception
{
    public ReplayException()
    {
    }

    public ReplayException(string msg) : base(msg)
    {
    }

    public ReplayException(string msg, Exception exception) : base(msg, exception)
    {
    }
}