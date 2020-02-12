using System.Windows;
using System.Windows.Threading;

namespace DiscordOverlay
{
    public static class WPFHelper
    {
        public static Window MainWindow => Application.Current?.MainWindow;

        public static Dispatcher Dispatcher => Application.Current?.Dispatcher;

#if DEBUG
        private readonly static bool isDebugMode = true;
        private static bool isDesignMode = false;
#else
        private readonly static bool isDebugMode = false;
#endif

        public static bool IsDebugMode => isDebugMode;

        public static bool IsDesignMode
        {
            get
            {
#if DEBUG
                if (!isDebugMode)
                {
                    if (System.ComponentModel.LicenseManager.UsageMode ==
                        System.ComponentModel.LicenseUsageMode.Designtime)
                    {
                        isDesignMode = true;
                    }
                    else
                    {
                        using (var p = System.Diagnostics.Process.GetCurrentProcess())
                        {
                            isDesignMode =
                                p.ProcessName.Equals("DEVENV", System.StringComparison.OrdinalIgnoreCase) ||
                                p.ProcessName.Equals("XDesProc", System.StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }

                return isDesignMode;
#else
                return false;
#endif
            }
        }
    }
}
