using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames.Tests
{
    /// <summary>Poker-Regeln ohne Unity: Handbewertung, schnelle Bewertung, Entscheidungen (nur zulaessige Aktionen).</summary>
    public static class PokerTests
    {
        static PokerCard C(string s) { int r = "23456789TJQKA".IndexOf(s[0]) + 2; int su = "shdc".IndexOf(s[1]); return new PokerCard(r, su); }
        static IEnumerable<PokerCard> H(string s) => s.Split(' ').Select(C);

        public static void All()
        {
            T.Cur = "Poker-Regeln"; Console.WriteLine("> " + T.Cur);
            var cases = new (string hand, int cat)[]
            {
                ("As Ks Qs Js Ts 2d 3c", 8), ("9h 9d 9c 9s 2d 3c 4h", 7), ("Ah Ad Ac Kd Ks 2c 3h", 6), ("2h 7h 9h Jh Kh 3c 4d", 5),
                ("5s 6d 7c 8h 9s Kc 2d", 4), ("As 2d 3c 4h 5s Kc 9d", 4), ("Qh Qd Qc 2s 5d 8c 9h", 3), ("Jh Jd 4c 4s Ad 8c 9h", 2), ("Th Td 4c 6s Ad 8c 9h", 1), ("2h 5d 7c 9s Jd Kc 3h", 0)
            };
            foreach (var c in cases) T.Eq(c.cat, PokerEval.Eval(H(c.hand)).cat, "Kategorie " + c.hand);
            T.Eq("Royal Flush", PokerEval.Eval(H("As Ks Qs Js Ts 2d 3c")).name, "Royal Flush erkannt");
            T.True(PokerEval.Eval(H("As 2d 3c 4h 5s Kc 9d")).score < PokerEval.Eval(H("2s 3d 4c 5h 6s Kc 9d")).score, "Wheel schlaegt 6-hoch nicht");
            T.True(PokerEval.Eval(H("Ah Ad Ac Kd Ks 2c 3h")).score > PokerEval.Eval(H("2h 7h 9h Jh Kh 3c 4d")).score, "Full House schlaegt Flush");
            T.True(PokerEval.Eval(H("Ah Kd 9c 5s 3d 2c 7h")).score < PokerEval.Eval(H("2h 2d 9c 5s 3d 8c 7h")).score, "Paar schlaegt hohe Karte");
            // Kicker entscheidet
            T.True(PokerEval.Eval(H("Ah Ad Kc 5s 3d 2c 7h")).score > PokerEval.Eval(H("As Ac Qc 5h 3c 2d 7s")).score, "Kicker K schlaegt Q");

            // schnelle Bewertung == Original
            var rnd = new Random(11); int diff = 0;
            for (int i = 0; i < 3000; i++)
            {
                var deck = Enumerable.Range(0, 52).OrderBy(_ => rnd.Next()).Take(7).Select(PokerCard.FromCode).ToList();
                if (PokerEval.Eval(deck).score != PokerEval.Fast(deck)) diff++;
            }
            T.Eq(0, diff, "Fast == Eval bei 3000 zufaelligen 7-Karten-Haenden");

            // Equity: Paar Asse gegen eine Zufallshand klar vorn, 7-2 offsuit hinten
            var eqAA = PokerAi.Equity(new[] { C("As"), C("Ah") }, new List<PokerCard>(), 1, 4000, new Random(3));
            var eq72 = PokerAi.Equity(new[] { C("7s"), C("2h") }, new List<PokerCard>(), 1, 4000, new Random(3));
            T.True(eqAA > .8 && eqAA < .9, $"Equity AA gegen 1 Gegner ~85 % (erhalten {eqAA:0.00})");
            T.True(eq72 > .3 && eq72 < .45, $"Equity 72o gegen 1 Gegner ~35 % (erhalten {eq72:0.00})");

            // KI: nie unzulaessig (Raise nur im Rahmen MinTo..MaxTo, Fold nur wenn zu zahlen, Raise nur wenn erlaubt)
            int bad = 0; var r2 = new Random(5);
            foreach (var lvl in new[] { PokerLevel.Original, PokerLevel.Easy, PokerLevel.Medium, PokerLevel.Hard })
                for (int i = 0; i < 150; i++)
                {
                    var deck = Enumerable.Range(0, 52).OrderBy(_ => r2.Next()).Take(7).Select(PokerCard.FromCode).ToList();
                    int nb = new[] { 0, 3, 4, 5 }[r2.Next(4)]; int toCall = r2.Next(3) == 0 ? 0 : r2.Next(1, 200); int chips = r2.Next(100, 2000); bool can = r2.Next(5) != 0;
                    var s = new PokerSpot { Hole = deck.Take(2).ToArray(), Board = deck.Skip(2).Take(nb).ToList(), NOpp = r2.Next(1, 4), ToCall = Math.Min(toCall, chips), CurBet = toCall, MyBet = 0, MyChips = chips, Pot = r2.Next(30, 800), BB = 20, MinTo = Math.Min(chips, toCall + 20), MaxTo = chips, CanRaise = can, EffStack = chips };
                    var d = PokerAi.Decide(lvl, s, r2);
                    if (d.act == "raise" && (!can || d.to < s.MinTo || d.to > s.MaxTo)) bad++;
                    if (d.act == "fold" && s.ToCall == 0) bad++;
                    if (d.act != "fold" && d.act != "call" && d.act != "raise") bad++;
                }
            T.Eq(0, bad, "KI liefert nur zulaessige Aktionen (600 Faelle)");
        }
    }
}
