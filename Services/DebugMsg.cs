using System.Windows;

namespace VorTech.App.Services
{
    public static class DebugMsg
    {
        // Mets false quand tu veux couper toutes les popups d’un coup
        public static bool Enabled = false;

        public static void Show(string title, string message)
        {
            if (!Enabled) return;
            MessageBox.Show(message, $"TRACE · {title}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
