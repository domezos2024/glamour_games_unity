using System;
using System.Security.Cryptography;
using System.Text;

namespace GlamourGames
{
    [Serializable]
#if UNITY_5_3_OR_NEWER
    [UnityEngine.Scripting.Preserve]
#endif
    public sealed class LicenseClaims
    {
        public string account, platform, nonce;
        public long issued_at, expires_at, entitlement_until;
    }

    /// <summary>Memory-only lease. No client wall clock or saved "unlocked" flag is trusted.</summary>
    public sealed class LicenseLease
    {
        double deadline = double.NegativeInfinity;
        public bool Allows(double monotonicSeconds) => !double.IsNaN(monotonicSeconds) && monotonicSeconds < deadline;
        public void Revoke() => deadline = double.NegativeInfinity;
        public bool Accept(LicenseClaims claims, string account, string platform, string nonce,
                           double requestStarted, double responseReceived)
        {
            Revoke();
            if (claims == null || claims.account != account || claims.platform != platform || claims.nonce != nonce ||
                claims.issued_at <= 0 || claims.expires_at <= claims.issued_at ||
                claims.expires_at - claims.issued_at > 120 || claims.expires_at > claims.entitlement_until ||
                double.IsNaN(requestStarted) || double.IsNaN(responseReceived) ||
                double.IsInfinity(requestStarted) || double.IsInfinity(responseReceived) || responseReceived < requestStarted)
                return false;
            // Count the whole request round trip against the lease, including provider latency.
            deadline = requestStarted + claims.expires_at - claims.issued_at;
            if (!Allows(responseReceived)) { Revoke(); return false; }
            return true;
        }
        public static string Account(string key)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant();
        }
        public static bool Verify(byte[] payload, byte[] signature, string modulus, string exponent)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters { Modulus = Convert.FromBase64String(modulus), Exponent = Convert.FromBase64String(exponent) });
                return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (CryptographicException) { return false; }
            catch (FormatException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }
}
