namespace Feyra;
public partial class App : System.Windows.Application
{
    public App()
    {
        // Run installer/update hooks before WPF creates the browser window.
        Velopack.VelopackApp.Build().Run();
    }
}
