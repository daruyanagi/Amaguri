using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class BitmapHelper
{
    // http://www.nuits.jp/entry/2016/10/17/181232

    // [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    // [return: MarshalAs(UnmanagedType.Bool)]
    // public static extern bool DeleteObject([In] IntPtr hObject);
    // 
    // public static ImageSource ToImageSource(this Bitmap bmp)
    // {
    //     var handle = bmp.GetHbitmap();
    //     try
    //     {
    //         return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
    //     }
    //     finally { DeleteObject(handle); }
    // }

    public static BitmapSource ToBitmapSource(this Bitmap source)
    {
        return Imaging.CreateBitmapSourceFromHBitmap(
            source.GetHbitmap(),
            IntPtr.Zero,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
    }

    public static Bitmap ToBitmap(this BitmapSource bitmapsource)
    {
        Bitmap bitmap;
        using (var outStream = new System.IO.MemoryStream())
        {
            BitmapEncoder enc = new BmpBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmapsource));
            enc.Save(outStream);
            bitmap = new Bitmap(outStream);
        }
        return bitmap;
    }

    public static Bitmap ToBitmap(this BitmapSource bitmapSource, System.Drawing.Imaging.PixelFormat pixelFormat)
    {
        int width = bitmapSource.PixelWidth;
        int height = bitmapSource.PixelHeight;
        int stride = width * ((bitmapSource.Format.BitsPerPixel + 7) / 8);  // 行の長さは色深度によらず8の倍数のため
        var intPtr = IntPtr.Zero;
        try
        {
            intPtr = Marshal.AllocCoTaskMem(height * stride);
            bitmapSource.CopyPixels(new Int32Rect(0, 0, width, height), intPtr, height * stride, stride);
            using (var bitmap = new Bitmap(width, height, stride, pixelFormat, intPtr))
            {
                // IntPtrからBitmapを生成した場合、Bitmapが存在する間、AllocCoTaskMemで確保したメモリがロックされたままとなる
                // （FreeCoTaskMemするとエラーとなる）
                // そしてBitmapを単純に開放しても解放されない
                // このため、明示的にFreeCoTaskMemを呼んでおくために一度作成したBitmapから新しくBitmapを
                // 再作成し直しておくとメモリリークを抑えやすい
                return new Bitmap(bitmap);
            }
        }
        finally
        {
            if (intPtr != IntPtr.Zero)
                Marshal.FreeCoTaskMem(intPtr);
        }
    }
}