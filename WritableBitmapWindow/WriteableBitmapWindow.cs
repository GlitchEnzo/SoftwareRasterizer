using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WritableBitmapWindow
{
    public class WriteableBitmapWindow : Window
    {
        public Image Image { get; set; }

        private WriteableBitmap writeableBitmap;
        public WriteableBitmap WriteableBitmap
        {
            get
            {
                return writeableBitmap;
            }
            set
            {
                writeableBitmap = value;
                Image.Source = writeableBitmap;
            }
        }

        public WriteableBitmapWindow(double width = 1024, double height = 768) :
            base()
        {
            Image = new Image();
            Image.Width = width;
            Image.Height = height;
            RenderOptions.SetBitmapScalingMode(Image, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(Image, EdgeMode.Aliased);

            Content = Image;
            Title = "WriteableBitmapWindow";
            //Show();

            Image.Stretch = Stretch.None;
            Image.HorizontalAlignment = HorizontalAlignment.Left;
            Image.VerticalAlignment = VerticalAlignment.Top;

            Loaded += WindowLoaded;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            WriteableBitmap = new WriteableBitmap(
                (int)Image.Width,
                (int)Image.Height,
                267,
                267,
                PixelFormats.Bgr32,
                null);

            _blankImage = new byte[WriteableBitmap.PixelHeight * WriteableBitmap.PixelWidth * RgbBytesPerPixel];

            Image.Source = WriteableBitmap;

            SizeToContent = SizeToContent.WidthAndHeight;
        }

        public void DrawPixel(int x, int y, Color color)
        {
            try
            {
                // Reserve the back buffer for updates.
                WriteableBitmap.Lock();

                unsafe
                {
                    // Get a pointer to the back buffer.
                    long pBackBuffer = (long)WriteableBitmap.BackBuffer;

                    // Find the address of the pixel to draw.
                    pBackBuffer += y * WriteableBitmap.BackBufferStride;
                    pBackBuffer += x * 4;

                    // Compute the pixel's color.
                    int color_data = color.R << 16; // R
                    color_data |= color.G << 8; // G
                    color_data |= color.B << 0; // B

                    // Assign the color data to the pixel.
                    *((int*)pBackBuffer) = color_data;
                }

                // Specify the area of the bitmap that changed.
                WriteableBitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                WriteableBitmap.Unlock();
            }
        }

        public void LockBitmap()
        {
            // Reserve the back buffer for updates.
            WriteableBitmap.Lock();
        }

        public void UnlockBitmap()
        {
            try
            {
                // Assume the entire bitmap changed.
                WriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, WriteableBitmap.PixelWidth, WriteableBitmap.PixelHeight));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                WriteableBitmap.Unlock();
            }
        }

        public void SetPixel(int x, int y, Color color)
        {
            unsafe
            {
                // Get a pointer to the back buffer.
                long pBackBuffer = (long)WriteableBitmap.BackBuffer;

                // Find the address of the pixel to draw.
                pBackBuffer += y * WriteableBitmap.BackBufferStride;
                pBackBuffer += x * 4;

                // Compute the pixel's color.
                int color_data = color.R << 16; // R
                color_data |= color.G << 8; // G
                color_data |= color.B << 0; // B

                // Assign the color data to the pixel.
                *((int*)pBackBuffer) = color_data;
            }
        }

        //public void AdjustPixel(int x, int y, byte adjustment)
        //{
        //    Color color = GetPixel(x, y);
        //    color.R += adjustment;
        //    color.G += adjustment;
        //    color.B += adjustment;

        //    SetPixel(x, y, color);
        //}

        public void SetGrayscaleImage(float[] grayscaleData)
        {
            LockBitmap();

            for (int y = 0; y < Image.Height; y++)
            {
                for (int x = 0; x < Image.Width; x++)
                {
                    var byteValue = (byte)(grayscaleData[x + (y * (int)Image.Width)] * 255);
                    Color color = Color.FromArgb(255, byteValue, byteValue, byteValue);
                    SetPixel(x, y, color);
                }
            }

            UnlockBitmap();
        }

        // https://stackoverflow.com/a/27449843
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        private const int RgbBytesPerPixel = 3;
        private byte[] _blankImage;
        public void Clear(Color color)
        {
            unsafe
            {
                fixed (byte* b = _blankImage)
                {
                    CopyMemory(WriteableBitmap.BackBuffer, (IntPtr)b, (uint)_blankImage.Length);
                }
                //Application.Current.Dispatcher.InvokeAsync(() =>
                //{
                    WriteableBitmap.Lock();
                    WriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, WriteableBitmap.PixelWidth, WriteableBitmap.PixelHeight));
                    WriteableBitmap.Unlock();
                //});
            }

            //GCHandle pinnedArray = new GCHandle();
            //IntPtr pointer = IntPtr.Zero;
            //try
            //{
            //    //n.b. If pinnedArray is used often wrap it in a class with IDisopsable and keep it around
            //    pinnedArray = GCHandle.Alloc(_blankImage, GCHandleType.Pinned);
            //    pointer = pinnedArray.AddrOfPinnedObject();

            //    CopyMemory(WriteableBitmap.BackBuffer, pointer, (uint)_blankImage.Length);

            //    Application.Current.Dispatcher.InvokeAsync(() =>
            //    {
            //        WriteableBitmap.Lock();
            //        WriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, WriteableBitmap.PixelWidth, WriteableBitmap.PixelHeight));
            //        WriteableBitmap.Unlock();
            //    });
            //}

            //finally
            //{
            //    pointer = IntPtr.Zero;
            //    pinnedArray.Free();
            //}
        }
    }
}
