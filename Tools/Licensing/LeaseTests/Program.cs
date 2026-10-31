using System;
using System.Security.Cryptography;
using System.Text;
using GlamourGames;

static class Tests
{
    static int count;
    static void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
    static LicenseClaims Claims(long duration = 120) => new LicenseClaims
    { account = "account", platform = "windows", nonce = "challenge", issued_at = 1000, expires_at = 1000 + duration, entitlement_until = 1120 };
    static bool Accept(LicenseLease lease, LicenseClaims claims, double start = 10, double end = 11) =>
        lease.Accept(claims, "account", "windows", "challenge", start, end);
    static void Main()
    {
        var lease = new LicenseLease();
        Check(!lease.Allows(0), "cold start is locked");
        Check(Accept(lease, Claims()), "valid lease");
        Check(lease.Allows(129.999), "before exact deadline");
        Check(!lease.Allows(130), "exact deadline locks");
        Check(!lease.Allows(131), "after deadline locks");
        Check(Accept(lease, Claims(1), 10, 10.5) && !lease.Allows(11), "trial boundary caps lease");
        Check(!Accept(lease, Claims(1), 10, 11), "latency consumes entire remaining trial");
        var claims = Claims(); claims.nonce = "old";
        Check(!Accept(lease, claims), "replay challenge rejected");
        claims = Claims(); claims.platform = "android";
        Check(!Accept(lease, claims), "other platform rejected");
        claims = Claims(); claims.account = "stolen";
        Check(!Accept(lease, claims), "other account rejected");
        Check(!Accept(lease, Claims(121)), "overlong lease rejected");
        Check(!Accept(lease, Claims(0)), "empty lease rejected");
        claims = Claims(); claims.entitlement_until = 1119;
        Check(!Accept(lease, claims), "lease past paid entitlement rejected");
        Check(!Accept(lease, Claims(), double.NaN), "NaN rejected");
        Check(!Accept(lease, Claims(), double.PositiveInfinity), "infinity rejected");
        Check(!Accept(lease, Claims(), 20, 19), "backwards monotonic clock rejected");
        Accept(lease, Claims()); lease.Revoke();
        Check(!lease.Allows(11), "standby or focus loss revokes");
        Check(!new LicenseLease().Allows(11), "restart never restores previous lease");
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportParameters(false);
        var bytes = Encoding.UTF8.GetBytes("challenge-bound-lease");
        var sig = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string modulus = Convert.ToBase64String(publicKey.Modulus), exponent = Convert.ToBase64String(publicKey.Exponent);
        Check(LicenseLease.Verify(bytes, sig, modulus, exponent), "server signature verified");
        bytes[0] ^= 1;
        Check(!LicenseLease.Verify(bytes, sig, modulus, exponent), "tampered payload rejected");
        Check(!LicenseLease.Verify(bytes, sig, "invalid", exponent), "invalid key rejected");
        Check(LicenseLease.Account("abc") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "account hashing matches server");
        Console.WriteLine($"{count} license checks passed");
    }
}
