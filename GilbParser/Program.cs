using System.IO;
using System.Text;
using System.Windows;

namespace GilbParser;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
            return SelfTest.Run();

        if (args.Length >= 2 && args[0] == "--analyze")
        {
            Console.OutputEncoding = Encoding.UTF8;
            bool loops = !args.Contains("--no-loop-nesting");
            var text = File.ReadAllText(args[1], Encoding.UTF8);
            var m = KotlinGilbAnalyzer.Analyze(text, loops);
            Console.WriteLine($"CL={m.CL}");
            Console.WriteLine($"cl={m.RelativeFormatted}");
            Console.WriteLine($"CLI={m.CLI}");
            Console.WriteLine($"operators={m.TotalOperators}");
            foreach (var d in m.Details)
                Console.WriteLine(" - " + d);
            return 0;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
        return 0;
    }
}
