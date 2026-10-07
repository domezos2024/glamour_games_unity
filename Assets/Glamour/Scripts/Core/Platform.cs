using UnityEngine;

namespace GlamourGames
{
    /// <summary>Plattformabhaengige Helfer: Touch-Geraet (Android) oder Maus/Tastatur (Editor, Desktop).</summary>
    public static class Platform
    {
        /// <summary>true auf Touch-Geraeten: Touch-Eingabe, Zurueck-Taste, Bildschirmtastatur, sicherer Bereich, Vibration.</summary>
        public static bool Touch => Application.isMobilePlatform;
        /// <summary>Bildschirmtastatur fuer Namenseingaben (am Telefon gibt es keine Hardware-Tastatur).</summary>
        public static bool VirtualKeyboard => Touch;
        /// <summary>Hinweistext je nach Eingabeart: pc = Maus/Tastatur, touch = Finger.</summary>
        public static string Pick(string pc, string touch) => Touch ? touch : pc;
    }
}
