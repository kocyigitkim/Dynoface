using System;
using System.Reflection;


namespace Dynoface
{
    internal class CachedInterface
    {
        public Type DeclarationType { get; set; }
        public MethodInfo[] MethodInfo { get; set; }
    }
}
