namespace Gateway.Tests.Load
{
    using System.Threading;

    class IdProvider
    {
        readonly string idPattern;
        int lastId;

        public IdProvider(int from, string idPattern)
        {
            this.idPattern = idPattern;
            this.lastId = from - 1;
        }

        public string Get()
        {
            return string.Format(this.idPattern, Interlocked.Increment(ref this.lastId));
        }
    }
}