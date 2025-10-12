using System;
using System.Linq;

namespace VorTech.App.Services
{
    public static class BarcodeUtil
    {
        /// <summary>
        /// Génère un EAN-13 valide.
        /// - prefix : chaîne numérique (ex: "200" = codes internes 200-299)
        /// - seed   : si fourni, on en extrait uniquement les chiffres pour compléter à 12 digits avant checksum
        /// </summary>
        public static string GenerateEAN13(string? seed = null, string prefix = "200")
        {
            var pref = new string((prefix ?? "200").Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(pref)) pref = "200";

            var coreDigits = (seed ?? Guid.NewGuid().ToString("N")).Where(char.IsDigit).ToArray();
            var base12 = (pref + new string(coreDigits)).PadRight(12, '0').Substring(0, 12);

            int checksum = ComputeChecksum(base12);
            return base12 + checksum.ToString();
        }

        public static bool IsValidEAN13(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 13 || !code.All(char.IsDigit)) return false;
            return ComputeChecksum(code[..12]) == (code[12] - '0');
        }

        private static int ComputeChecksum(string base12)
        {
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int d = base12[i] - '0';
                sum += ((i % 2) == 0) ? d : d * 3;
            }
            int mod = sum % 10;
            return (10 - mod) % 10;
        }
    }
}
