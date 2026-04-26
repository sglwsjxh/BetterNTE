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

	public static Mat CaptureScreen() {
		var frame = new Mat();
		CaptureScreen(frame);
		return frame;
	}

	public static void CaptureScreen(Mat target) {
		var width = GetSystemMetrics(SM_CXSCREEN);
		var height = GetSystemMetrics(SM_CYSCREEN);

		lock (_sync) {
			EnsureBuffer(width, height);
			_bufferGraphics!.CopyFromScreen(0, 0, 0, 0, _bufferBitmap!.Size);
			BitmapToMat(_bufferBitmap, target);
		}
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
