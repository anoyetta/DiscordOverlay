using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Media;
using CefSharp;
using CefSharp.Wpf;
using Prism.Commands;
using Prism.Mvvm;

namespace DiscordOverlay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Overlay : Window
    {
        public Overlay()
        {
            this.InitializeComponent();
            InitializeCef();

            Config.Current.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Config.IsLayoutLocked):
                        this.ApplyLayoutLock();
                        break;

                    case nameof(Config.VoiceWidgetUri):
                        this.SetUri();
                        break;
                }
            };

            this.Loaded += (_, __) =>
            {
                this.BackgroundTextGrid.Visibility = !string.IsNullOrEmpty(this.Config.BackgroundText) ?
                    Visibility.Visible :
                    Visibility.Collapsed;

                this.ApplyLayoutLock();
                this.SetUri();
                this.WebGrid.Children.Add(this.CefBrowser);
            };

            this.MouseLeftButtonDown += (_, __) => this.StartDragMove();

            this.LeftThumb.DragDelta += (_, e) =>
            {
                if (!this.Config.IsLayoutLocked)
                {
                    this.Left += e.HorizontalChange;
                    this.Width -= e.HorizontalChange;
                }
            };

            this.RightThumb.DragDelta += (_, e) =>
            {
                if (!this.Config.IsLayoutLocked)
                {
                    this.Width += e.HorizontalChange;
                }
            };

            this.TopThumb.DragDelta += (_, e) =>
            {
                if (!this.Config.IsLayoutLocked)
                {
                    this.Top += e.VerticalChange;
                    this.Height -= e.VerticalChange;
                }
            };

            this.BottomThumb.DragDelta += (_, e) =>
            {
                if (!this.Config.IsLayoutLocked)
                {
                    this.Height += e.VerticalChange;
                }
            };
        }

        private void SetUri()
        {
            if (!string.IsNullOrEmpty(Config.Current.VoiceWidgetUri))
            {
                this.CefBrowser.Address = Config.Current.VoiceWidgetUri;
                this.CefBrowser.Visibility = Visibility.Visible;
            }
            else
            {
                this.CefBrowser.Address = "about:blank";
                this.CefBrowser.Visibility = Visibility.Hidden;
            }
        }

        private void StartDragMove()
        {
            if (!this.Config.IsLayoutLocked)
            {
                this.DragMove();
            }
        }

        private readonly SolidColorBrush SemiTransparent = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#01000000"));

        private void ApplyLayoutLock()
        {
            this.ResizeMode = this.Config.IsLayoutLocked ?
                ResizeMode.NoResize :
                ResizeMode.CanResizeWithGrip;
            this.Background = this.Config.IsLayoutLocked ?
                Brushes.Transparent :
                this.SemiTransparent;
        }

        public Config Config => Config.Current;

        private readonly Lazy<ChromiumWebBrowser> LazyCefBrowser = new Lazy<ChromiumWebBrowser>(() =>
        {
            var browser = new ChromiumWebBrowser()
            {
                RequestHandler = new WidgetRequestHandler(),
                BrowserSettings = new BrowserSettings()
                {
                    FileAccessFromFileUrls = CefState.Enabled,
                    UniversalAccessFromFileUrls = CefState.Enabled,
                    WindowlessFrameRate = 30,
                },
            };

            return browser;
        });

        public ChromiumWebBrowser CefBrowser => this.LazyCefBrowser.Value;

        private static volatile bool isInitialized = false;
        private static readonly object CefLocker = new object();

        public static void InitializeCef()
        {
            lock (CefLocker)
            {
                if (isInitialized)
                {
                    return;
                }

                isInitialized = true;
            }

            var settings = new CefSettings();

            settings.BrowserSubprocessPath = Path.Combine(
                AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                Environment.Is64BitProcess ? "x64" : "x86",
                "CefSharp.BrowserSubprocess.exe");

            settings.Locale = CultureInfo.CurrentCulture.Parent.ToString();
            settings.AcceptLanguageList = CultureInfo.CurrentCulture.Name;

            settings.CachePath = Path.Combine(
                Path.GetTempPath(),
                "DISCORD Overlay");
            settings.LogFile = Path.Combine(
                Path.GetTempPath(),
                "DISCORD Overlay",
                "browser.log");
            settings.LogSeverity = LogSeverity.Warning;

            // GPUアクセラレータを切る
            settings.DisableGpuAcceleration();

            Cef.EnableHighDPISupport();
            Cef.Initialize(settings);

            // shutdown を仕込む
            App.Current.Exit += (_, __) => Cef.Shutdown();
        }

        private DelegateCommand _openOptionsCommand;

        public DelegateCommand OpenOptionsCommand =>
            this._openOptionsCommand ?? (this._openOptionsCommand = new DelegateCommand(this.ExecuteOpenOptionsCommand));

        private void ExecuteOpenOptionsCommand()
        {
            var window = new OptionsWindow()
            {
                Owner = this
            };

            window.Show();
        }
    }

    public class WidgetRequestHandler : IRequestHandler
    {
        public CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
            => CefReturnValue.Continue;

        public bool CanGetCookies(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request)
            => true;

        public bool CanSetCookie(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, Cookie cookie)
            => true;

        public IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            => null;

        public bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
            => false;

        public bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
            => true;

        public bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
            => false;

        public void OnPluginCrashed(IWebBrowser chromiumWebBrowser, IBrowser browser, string pluginPath)
        {
        }

        public bool OnProtocolExecution(IWebBrowser chromiumWebBrowser, IBrowser browser, string url)
            => true;

        public bool OnQuotaRequest(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
            => true;

        public void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser, CefTerminationStatus status)
        {
        }

        public void OnRenderViewReady(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public void OnResourceLoadComplete(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        {
        }

        public void OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
        {
        }

        public bool OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            => false;

        public bool OnSelectClientCertificate(IWebBrowser chromiumWebBrowser, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates, ISelectClientCertificateCallback callback)
            => false;

        public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
            => null;

        public bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
            => true;
    }

    public class ZoomItem : BindableBase
    {
        private string text;

        public string Text
        {
            get => this.text;
            set => this.SetProperty(ref this.text, value);
        }

        private double zoomLevel;

        public double ZoomLevel
        {
            get => this.zoomLevel;
            set => this.SetProperty(ref this.zoomLevel, value);
        }
    }
}
