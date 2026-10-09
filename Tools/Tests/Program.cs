using System;

namespace GlamourGames.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            NetTests.All();
            TransportContract.Run(new MemEnv());
            TransportContract.Run(new TcpEnv());
            PokerTests.All();
            Console.WriteLine($"\n{T.Run} Pruefungen, {T.Fail} Fehler, {sw.ElapsedMilliseconds} ms");
            return T.Fail == 0 ? 0 : 1;
        }
    }
}
