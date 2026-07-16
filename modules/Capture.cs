using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;

class Capture {
	const int SM_CXSCREEN = 0;
	const int SM_CYSCREEN = 1;
	static readonly object _sync = new();
	static Bitmap? _bufferBitmap;
	static Graphics? _bufferGraphics;
	static int _bufferWidth;
	static int _bufferHeight;

	[DllImport("user32.dll")]
	static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll")]
	static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

	[DllImport("user32.dll")]
	static extern IntPtr GetForegroundWindow();

	[StructLayout(LayoutKind.Sequential)]
	struct RECT { public int Left, Top, Right, Bottom; }

	[StructLayout(LayoutKind.Sequential)]
	struct POINT { public int X, Y; }

	public static (int Width, int Height) GetScreenSize() {
		return (GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
	}

	public static (int Width, int Height)? GetWindowClientSize(IntPtr hWnd) {
		if (hWnd == IntPtr.Zero || !GetClientRect(hWnd, out var rect))
			return null;
		return (rect.Right - rect.Left, rect.Bottom - rect.Top);
	}

	public static Mat CaptureScreen() {
		var frame = new Mat();
		CaptureScreen(frame);
		return frame;
	}

	public static void CaptureScreen(Mat target) {
		var (width, height) = GetScreenSize();

		lock (_sync) {
			EnsureBuffer(width, height);
			_bufferGraphics!.CopyFromScreen(0, 0, 0, 0, _bufferBitmap!.Size);
			BitmapToMat(_bufferBitmap, target);
		}
	}

	public static bool CaptureWindow(Mat target, IntPtr hWnd) {
		if (hWnd == IntPtr.Zero || !GetClientRect(hWnd, out var rect))
			return false;

		int w = rect.Right - rect.Left;
		int h = rect.Bottom - rect.Top;
		if (w <= 0 || h <= 0)
			return false;

		var pt = new POINT { X = 0, Y = 0 };
		if (!ClientToScreen(hWnd, ref pt))
			return false;

		lock (_sync) {
			EnsureBuffer(w, h);
			_bufferGraphics!.CopyFromScreen(pt.X, pt.Y, 0, 0, new System.Drawing.Size(w, h));
			BitmapToMat(_bufferBitmap!, target);
		}
		return true;
	}

	static void EnsureBuffer(int width, int height) {
		if (_bufferBitmap != null && _bufferWidth == width && _bufferHeight == height)
			return;

		_bufferGraphics?.Dispose();
		_bufferBitmap?.Dispose();

		_bufferBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		_bufferGraphics = Graphics.FromImage(_bufferBitmap);
		_bufferWidth = width;
		_bufferHeight = height;
	}

	static void BitmapToMat(Bitmap bitmap, Mat target) {
		var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
		var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

		try {
			if (target.Empty() || target.Width != bitmap.Width || target.Height != bitmap.Height || target.Type() != MatType.CV_8UC3)
				target.Create(bitmap.Height, bitmap.Width, MatType.CV_8UC3);

			using var temp = Mat.FromPixelData(bitmap.Height, bitmap.Width, MatType.CV_8UC3, data.Scan0, data.Stride);
			temp.CopyTo(target);
		} finally {
			bitmap.UnlockBits(data);
		}
	}
}
