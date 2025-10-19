using System.Windows;
using System.Windows.Documents;

namespace VorTech.App.Views
{
    public partial class NoteEditorWindow : Window
    {
        public string ResultText { get; private set; } = "";

        public NoteEditorWindow(string? initialText = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialText))
            {
                Editor.Document.Blocks.Clear();
                Editor.Document.Blocks.Add(new Paragraph(new Run(initialText)));
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultText = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text.TrimEnd();
            DialogResult = true;
            Close();
        }
    }
}
