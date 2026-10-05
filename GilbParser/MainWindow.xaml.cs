using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace GilbParser;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        CodeEditor.Text = ReadSampleOrEmbedded();
        FileLabel.Text = "Образец: Sample/Sample.kt (Kotlin)";
    }

    private static string ReadSampleOrEmbedded()
    {
        string[] paths =
        [
            Path.Combine(AppContext.BaseDirectory, "Sample", "Sample.kt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Sample", "Sample.kt")
        ];
        foreach (var path in paths)
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        return SampleCodes.LabProgram;
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл Kotlin",
            Filter = "Kotlin (*.kt)|*.kt|Все файлы (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            CodeEditor.Text = File.ReadAllText(dlg.FileName);
            FileLabel.Text = "Файл: " + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка чтения файла", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSample_Click(object sender, RoutedEventArgs e)
    {
        CodeEditor.Text = ReadSampleOrEmbedded();
        FileLabel.Text = "Образец лабораторной (все циклы Kotlin + if + when)";
    }

    private void Calculate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool loops = LoopNestingCheck.IsChecked == true;
            var result = KotlinGilbAnalyzer.Analyze(CodeEditor.Text, loops);
            ClText.Text = result.CL.ToString();
            RelText.Text = result.RelativeFormatted;
            CliText.Text = result.CLI.ToString();
            TotalText.Text = result.TotalOperators.ToString();
            DetailsList.ItemsSource = result.Details;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка анализа", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
