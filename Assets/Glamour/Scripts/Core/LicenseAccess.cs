#if UNITY_5_3_OR_NEWER
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GlamourGames
{
    /// <summary>Direct-distribution billing. Editors and the software preview are never billing clients.</summary>
    public sealed class LicenseAccess : MonoBehaviour
    {
        public static bool Required
        {
            get
            {
#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_ANDROID)
                return true;
#else
                return false;
#endif
            }
        }
        static LicenseAccess instance;
        readonly LicenseLease lease = new LicenseLease();
        readonly Stopwatch clock = Stopwatch.StartNew();
        HttpClient http;
        string key, inputKey, device, message = "Online-Pruefung erforderlich.", approval;
        Settings settings;
        bool busy, reveal;
        int generation;
        double retryAt;
        static string PlatformId => Application.platform == RuntimePlatform.Android ? "android" : "windows";
        public static bool Allowed => !Required || (instance != null && instance.lease.Allows(instance.clock.Elapsed.TotalSeconds));
        [Serializable, UnityEngine.Scripting.Preserve] sealed class Settings { public string endpoint = "", modulus = "", exponent = ""; }
        [Serializable, UnityEngine.Scripting.Preserve] sealed class Request { public string action, key, platform, nonce, device; }
        [Serializable, UnityEngine.Scripting.Preserve] sealed class Reply { public string payload = "", signature = "", approval_url = "", error = ""; }

        public static void Initialize(GameObject owner)
        {
            if (Required && instance == null) owner.AddComponent<LicenseAccess>();
        }
        void Awake()
        {
            instance = this;
            var asset = Resources.Load<TextAsset>("Glamour/LicenseSettings");
            if (asset != null) settings = JsonUtility.FromJson<Settings>(asset.text);
            key = PlayerPrefs.GetString("glamour.license.key", "");
            if (!ValidKey(key)) { key = RandomHex(); PlayerPrefs.SetString("glamour.license.key", key); PlayerPrefs.Save(); }
            inputKey = key;
            var osDevice = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(osDevice) || osDevice == SystemInfo.unsupportedIdentifier)
            {
                message = "Die Geraetekennung konnte nicht ermittelt werden.";
                return;
            }
            device = LicenseLease.Account("glamour-device-v1/" + PlatformId + "/" + osDevice);
            http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(40) };
            Check();
        }
        static string RandomHex()
        {
            var bytes = new byte[32]; using var random = RandomNumberGenerator.Create(); random.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
        static bool ValidKey(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'f') && !(c >= '0' && c <= '9')) return false;
            return true;
        }
        void Update() { if (!busy && clock.Elapsed.TotalSeconds >= retryAt) Check(); }
        void Revoke()
        {
            generation++; busy = false; lease.Revoke(); approval = null;
            retryAt = clock.Elapsed.TotalSeconds;
        }
        void OnApplicationPause(bool paused) { Revoke(); }
        void OnApplicationFocus(bool focused) { Revoke(); }
        void OnDestroy() { Revoke(); http?.Dispose(); if (instance == this) instance = null; }
        public void Check() => Send("verify");
        async void Send(string action)
        {
            if (busy || http == null) return;
            if (settings == null || settings.endpoint != "https://domezos-ware.com/glamour_licensing/index.php" ||
                string.IsNullOrEmpty(settings.modulus) || string.IsNullOrEmpty(settings.exponent))
            {
                message = "Lizenzdienst noch nicht eingerichtet. Bitte spaeter erneut versuchen.";
                retryAt = clock.Elapsed.TotalSeconds + 30; return;
            }
            busy = true; int current = generation; double began = clock.Elapsed.TotalSeconds;
            string nonce = RandomHex();
            try
            {
                var body = JsonUtility.ToJson(new Request { action = action, key = key, platform = PlatformId, nonce = nonce, device = device });
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await http.PostAsync(settings.endpoint, content);
                var text = await response.Content.ReadAsStringAsync();
                if (current != generation) return;
                if (text.Length > 16384) throw new InvalidOperationException();
                var result = JsonUtility.FromJson<Reply>(text);
                if (result == null || !response.IsSuccessStatusCode)
                {
                    lease.Revoke();
                    message = result?.error ?? "Keine Online-Bestaetigung. Spiele gesperrt."; return;
                }
                if (action == "checkout")
                {
                    if (!Uri.TryCreate(result.approval_url, UriKind.Absolute, out var url) || url.Scheme != "https" ||
                        (url.Host != "www.paypal.com" && url.Host != "www.sandbox.paypal.com")) throw new InvalidOperationException();
                    approval = result.approval_url;
                    message = "PayPal-Seite bereit. Danach zur App zurueckkehren und Zahlung pruefen.";
                    // Explicit button after async completion also works on platforms requiring a user gesture.
                    return;
                }
                var payload = Convert.FromBase64String(result.payload);
                var signature = Convert.FromBase64String(result.signature);
                if (!LicenseLease.Verify(payload, signature, settings.modulus, settings.exponent) ||
                    !lease.Accept(JsonUtility.FromJson<LicenseClaims>(Encoding.UTF8.GetString(payload)), LicenseLease.Account(key),
                        PlatformId, nonce, began, clock.Elapsed.TotalSeconds)) throw new InvalidOperationException();
                message = "Zugang bestaetigt. Testzeit ohne Zahlungsmittel.";
            }
            catch (Exception)
            {
                if (current == generation) { lease.Revoke(); message = "Keine sichere Online-Bestaetigung. Spiele gesperrt."; }
            }
            finally
            {
                if (current == generation) { busy = false; retryAt = clock.Elapsed.TotalSeconds + (Allowed ? 45 : 30); }
            }
        }
        void OnGUI()
        {
            if (Allowed) return;
            GUI.depth = -1000;
            GUI.color = new Color(.02f, .04f, .09f, 1);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var old = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 650f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1000 * scale) / 2, (Screen.height - 650 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            GUI.Label(new Rect(70, 35, 860, 60), "GLAMOUR GAMES - Zugang", label);
            GUI.Label(new Rect(70, 105, 860, 75), PlatformId == "android"
                ? "7 Tage kostenlos ohne Zahlungsmittel. Abo danach optional: 1,99 EUR / 30 Tage."
                : "7 Tage kostenlos ohne Zahlungsmittel. Abo danach optional: 2,99 EUR / 30 Tage.", label);
            GUI.Label(new Rect(70, 190, 860, 75), busy ? "Online-Pruefung laeuft ..." : message, label);
            GUI.enabled = !busy;
            if (GUI.Button(new Rect(80, 285, 400, 65), "Abo mit PayPal abschliessen", button)) Send("checkout");
            if (GUI.Button(new Rect(520, 285, 400, 65), "Zahlung pruefen", button)) Check();
            if (!string.IsNullOrEmpty(approval) && GUI.Button(new Rect(80, 365, 840, 55), "PayPal im Browser oeffnen", button)) Application.OpenURL(approval);
            GUI.Label(new Rect(80, 425, 840, 45), "Zugangsschluessel sichern / vorhandenen Zugang wiederherstellen:", new GUIStyle(label) { fontSize = 18 });
            inputKey = reveal ? GUI.TextField(new Rect(80, 475, 670, 40), inputKey, 64)
                : GUI.PasswordField(new Rect(80, 475, 670, 40), inputKey, '*', 64);
            if (GUI.Button(new Rect(770, 475, 150, 40), reveal ? "Verbergen" : "Anzeigen")) reveal = !reveal;
            if (GUI.Button(new Rect(80, 530, 400, 55), "Zugang wiederherstellen", button))
            {
                if (ValidKey(inputKey)) { Revoke(); key = inputKey; PlayerPrefs.SetString("glamour.license.key", key); PlayerPrefs.Save(); Check(); }
                else message = "Der Zugangsschluessel muss 64 Zeichen enthalten (0-9, a-f).";
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(520, 530, 400, 55), "Beenden", button)) App.Quit();
            GUI.Label(new Rect(70, 595, 860, 50), "Internet ist zum Start und fuer regelmaessige Lizenzpruefungen erforderlich.", new GUIStyle(label) { fontSize = 18 });
            GUI.matrix = old;
        }
    }
}
#endif
