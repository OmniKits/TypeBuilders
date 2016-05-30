using System;

static class NewException
{
    public static ArgumentException ForArgument(string name)
     => new ArgumentException(null, name);
    public static Exception ForInvalidArgument(string name)
     => ForArgument(name);
}
