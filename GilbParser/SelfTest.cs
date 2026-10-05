namespace GilbParser;

internal static class SelfTest
{
    public static int Run()
    {
        var book = KotlinGilbAnalyzer.Analyze(SampleCodes.TextbookExample, true);
        var lab = KotlinGilbAnalyzer.Analyze(SampleCodes.LabProgram, true);

        Console.WriteLine("TEXTBOOK CL={0} cl={1} CLI={2} N={3}",
            book.CL, book.RelativeFormatted, book.CLI, book.TotalOperators);
        Console.WriteLine("LAB CL={0} cl={1} CLI={2} N={3}",
            lab.CL, lab.RelativeFormatted, lab.CLI, lab.TotalOperators);
        foreach (var d in lab.Details)
            Console.WriteLine(" - " + d);

        bool ok = book.CL == 4 && book.TotalOperators == 11 && book.CLI == 3
                  && Math.Abs(book.RelativeCl - 4.0 / 11.0) < 1e-9;
        Console.WriteLine(ok ? "SELFTEST OK" : "SELFTEST FAIL");
        return ok ? 0 : 1;
    }
}
