using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;

class Capture {
	const int SM_CXSCREEN = 0;
	const int SM_CYSCREEN = 1;

	[DllImport("user32.dll")]
	static extern int GetSystemMetrics(int nIndex);

	public static Mat CaptureScreen() {
		var width = GetSystemMetrics(SM_CXSCREEN);
		var height = GetSystemMetrics(SM_CYSCREEN);

		using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		using (var g = Graphics.FromImage(bitmap)) {
			g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
		}

		return BitmapToMat(bitmap);
	}

	static Mat BitmapToMat(Bitmap bitmap) {
		var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
		var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

		try {
			using var temp = Mat.FromPixelData(bitmap.Height, bitmap.Width, MatType.CV_8UC3, data.Scan0, data.Stride);
			return temp.Clone();
		} finally {
			bitmap.UnlockBits(data);
		}
	}
}
