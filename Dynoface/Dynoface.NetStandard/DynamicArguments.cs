
namespace Dynoface
{
    public class DynamicArguments
    {
        private dynamic[] args;
        public DynamicArguments(params dynamic[] args)
        {
            this.args = args;
        }
        public int Count => args.Length;
        public T Get<T>(int index)
        {
#pragma warning disable CS8603 // Possible null reference return.
            if (index > args.Length - 1) return default(T);
#pragma warning restore CS8603 // Possible null reference return.
            return (T)args[index];
        }
        public dynamic[] GetAll()
        {
            return args;
        }
    }
}
