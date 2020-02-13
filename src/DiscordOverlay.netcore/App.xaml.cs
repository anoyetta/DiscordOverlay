using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DiscordOverlay
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CefSharpResolver;

            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            this.Startup += this.App_Startup;
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            Config.Load();
        }

        private static Assembly CefSharpResolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("CefSharp", StringComparison.OrdinalIgnoreCase))
            {
                var assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                var archSpecificPath = Path.Combine(
                    AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                    Environment.Is64BitProcess ? "x64" : "x86",
                    assemblyName);

                return File.Exists(archSpecificPath) ?
                    Assembly.LoadFile(archSpecificPath) :
                    null;
            }

            return null;
        }
    }
}
