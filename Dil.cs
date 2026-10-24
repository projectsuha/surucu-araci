// Dil.cs - Arayuz metinleri: Turkce ve Ingilizce
using System.Globalization;

namespace SurucuAraci
{
    static class Dil
    {
        public static bool Ingilizce;

        // "sistem" secildiyse Windows'un dili kullanilir
        public static void Ayarla(string secim)
        {
            Ingilizce = secim == "en" || (secim != "tr" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "tr");
        }

        public static string M(string tr, string en)
        {
            return Ingilizce ? en : tr;
        }
    }
}
