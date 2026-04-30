namespace PrintAgent;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Bootstrap implemented in Task 10.1
    }
}
