using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DiscordOverlay
{
    public interface IOverlay
    {
        bool OverlayVisible { get; set; }

        int ZOrder { get; }
    }

    public static class OverlayExtensions
    {
        public static Window ToWindow(
            this IOverlay overlay)
            => overlay as Window;

        public static bool SetOverlayVisible(
            this IOverlay overlay,
            ref bool overlayVisible,
            bool newValue,
            double opacity = 1.0d)
        {
            if (overlayVisible != newValue)
            {
                overlayVisible = newValue;
                if (overlayVisible)
                {
                    overlay.ShowOverlay(opacity);
                }
                else
                {
                    overlay.HideOverlay();
                }

                return true;
            }

            return false;
        }

        public static void InitializeOverlayVisible(
            this IOverlay overlay,
            ref bool overlayVisible,
            bool newValue,
            double opacity = 1.0d)
        {
            overlayVisible = newValue;

            if (overlayVisible)
            {
                overlay.ShowOverlay(opacity);
            }
            else
            {
                overlay.HideOverlay();
            }
        }

        public static bool ShowOverlay(
            this IOverlay overlay,
            double opacity = 1.0d)
        {
            var r = false;

            if (overlay is Window w)
            {
                if (w.Opacity <= 0)
                {
                    w.Opacity = opacity;
                    r = true;
                }
            }

            return r;
        }

        public static void HideOverlay(
            this IOverlay overlay)
        {
            if (overlay is Window w)
            {
                w.Opacity = 0;
            }
        }

        public static void ToNonActive(
            this Window window)
        {
            window.SourceInitialized += (s, e) =>
            {
                // Get this window's handle
                var hwnd = new WindowInteropHelper(window).Handle;

                // Change the extended window style to include WS_EX_TRANSPARENT
                var extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

                NativeMethods.SetWindowLong(
                    hwnd,
                    NativeMethods.GWL_EXSTYLE,
                    extendedStyle | NativeMethods.WS_EX_NOACTIVATE);
            };
        }

        public static void ToNotTransparent(
            this Window window)
        {
            // Get this window's handle
            IntPtr hwnd = new WindowInteropHelper(window).Handle;

            // Change the extended window style to include WS_EX_TRANSPARENT
            int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

            NativeMethods.SetWindowLong(
                hwnd,
                NativeMethods.GWL_EXSTYLE,
                extendedStyle & ~NativeMethods.WS_EX_TRANSPARENT);
        }

        public static void ToTransparent(
            this Window window)
        {
            // Get this window's handle
            var hwnd = new WindowInteropHelper(window).Handle;

            // Change the extended window style to include WS_EX_TRANSPARENT
            var extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

            NativeMethods.SetWindowLong(
                hwnd,
                NativeMethods.GWL_EXSTYLE,
                extendedStyle | NativeMethods.WS_EX_TRANSPARENT);
        }

        #region ZOrder Corrector

        private static readonly DispatcherTimer ZOrderCorrector = new DispatcherTimer(DispatcherPriority.ContextIdle)
        {
            Interval = TimeSpan.FromSeconds(15)
        };

        private static readonly List<IOverlay> ToCorrectOverlays = new List<IOverlay>(64);

        public static void SubscribeZOrderCorrector(
            this IOverlay overlay)
        {
            lock (ZOrderCorrector)
            {
                if (!ZOrderCorrector.IsEnabled)
                {
                    ZOrderCorrector.Tick -= ZOrderCorrectorOnTick;
                    ZOrderCorrector.Tick += ZOrderCorrectorOnTick;
                }

                if (overlay is Window window)
                {
                    window.Closing += (x, y) =>
                    {
                        if (x is IOverlay o)
                        {
                            o.UnsubscribeZOrderCorrector();
                        }
                    };
                }

                if (!ToCorrectOverlays.Contains(overlay))
                {
                    ToCorrectOverlays.Add(overlay);
                }

                if (ToCorrectOverlays.Any() &&
                    !ZOrderCorrector.IsEnabled)
                {
                    ZOrderCorrector.Start();
                }
            }
        }

        public static void UnsubscribeZOrderCorrector(
            this IOverlay overlay)
        {
            lock (ZOrderCorrector)
            {
                ToCorrectOverlays.Remove(overlay);

                if (!ToCorrectOverlays.Any())
                {
                    ZOrderCorrector.Stop();
                }
            }
        }

        private static void ZOrderCorrectorOnTick(
            object sender,
            EventArgs e)
        {
            lock (ZOrderCorrector)
            {
                if (!ToCorrectOverlays.Any())
                {
                    ZOrderCorrector.Stop();
                    return;
                }

                var targets = ToCorrectOverlays.OrderBy(x => x.ZOrder);
                foreach (var overlay in ToCorrectOverlays)
                {
                    if (overlay == null)
                    {
                        continue;
                    }

                    if (!overlay.OverlayVisible)
                    {
                        continue;
                    }

                    if (overlay is Window window &&
                        window.IsLoaded)
                    {
                        overlay.EnsureTopMost();
                    }
                }
            }
        }

        public static IntPtr GetHandle(
            this IOverlay overlay) =>
            new WindowInteropHelper(overlay as Window).Handle;

        private static bool IsOverlayTop(
            this IOverlay overlay)
        {
            var handle = overlay.GetHandle();

            var preHandle = NativeMethods.GetWindow(handle, NativeMethods.GW_HWNDFIRST);

            return handle == preHandle;
        }

        /// <summary>
        /// Windowを最前面に持ってくる
        /// </summary>
        /// <param name="overlay"></param>
        public static void EnsureTopMost(
            this IOverlay overlay)
        {
            NativeMethods.SetWindowPos(
                overlay.GetHandle(),
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE);
        }

        #endregion ZOrder Corrector
    }

    /// <summary>
    /// ネイティブ関数を提供します。
    /// </summary>
    public static class NativeMethods
    {
        public struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        public const byte AC_SRC_ALPHA = 1;
        public const byte AC_SRC_OVER = 0;

        public struct Point
        {
            public int X;
            public int Y;
        }

        public struct Size
        {
            public int Width;
            public int Height;
        }

        //public struct Rect
        //{
        //    public int Left;
        //    public int Top;
        //    public int Right;
        //    public int Bottom;
        //}

        [DllImport("user32")]
        public static extern bool UpdateLayeredWindow(
            IntPtr hWnd,
            IntPtr hdcDst,
            [In] ref Point pptDst,
            [In]ref Size pSize,
            IntPtr hdcSrc,
            [In]ref Point pptSrc,
            int crKey,
            [In]ref BlendFunction pBlend,
            uint dwFlags);

        public const int ULW_ALPHA = 2;

        [DllImport("gdi32")]
        public static extern IntPtr SelectObject(
            IntPtr hdc,
            IntPtr hgdiobj);

        [DllImport("gdi32")]
        public static extern bool DeleteObject(
            IntPtr hObject);

        [DllImport("gdi32")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct BitmapInfo
        {
            public BitmapInfoHeader bmiHeader;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.Struct)]
            public RgbQuad[] bmiColors;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct BitmapInfoHeader
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public BitmapCompressionMode biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;

            public void Init()
            {
                biSize = (uint)Marshal.SizeOf(this);
            }
        }

        public enum BitmapCompressionMode : uint
        {
            BI_RGB = 0,
            BI_RLE8 = 1,
            BI_RLE4 = 2,
            BI_BITFIELDS = 3,
            BI_JPEG = 4,
            BI_PNG = 5
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct RgbQuad
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }

        [DllImport("gdi32")]
        public static extern IntPtr CreateDIBSection(
            IntPtr hdc,
            [In] ref BitmapInfo pbmi,
            uint iUsage,
            out IntPtr ppvBits,
            IntPtr hSection,
            uint dwOffset);

        public const int DIB_RGB_COLORS = 0x0000;

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0xC00000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("kernel32")]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        // For hide from ALT+TAB preview

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        public static extern void SetLastError(int dwErrorCode);

        private static int ToIntPtr32(IntPtr intPtr)
        {
            return unchecked((int)intPtr.ToInt64());
        }

        public static IntPtr SetWindowLongA(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            int error = 0;
            IntPtr result = IntPtr.Zero;

            SetLastError(0);

            if (IntPtr.Size == 4)
            {
                Int32 result32 = SetWindowLong(hWnd, nIndex, ToIntPtr32(dwNewLong));
                error = Marshal.GetLastWin32Error();
                result = new IntPtr(result32);
            }
            else
            {
                result = SetWindowLongPtr(hWnd, nIndex, dwNewLong);
                error = Marshal.GetLastWin32Error();
            }

            if ((result == IntPtr.Zero) && (error != 0))
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            return result;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(
            IntPtr hWnd,  // 元ウィンドウのハンドル
            uint uCmd     // 関係
        );

        public const uint GW_HWNDFIRST = 0x0000;
        public const uint GW_HWNDPREV = 0x0003;

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(
            IntPtr hWnd,             // ウィンドウのハンドル
            IntPtr hWndInsertAfter,  // 配置順序のハンドル
            int X,                   // 横方向の位置
            int Y,                   // 縦方向の位置
            int cx,                  // 幅
            int cy,                  // 高さ
            uint uFlags              // ウィンドウ位置のオプション
        );

        public static readonly IntPtr HWND_TOP = new IntPtr(0);
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public const uint SWP_FRAMECHANGED = 0x20;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        public static object GetWindow(object handle, uint gW_HWNDPREV)
        {
            throw new NotImplementedException();
        }

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_CHAR = 0x0102;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_SYSCHAR = 0x0106;
    }
}
