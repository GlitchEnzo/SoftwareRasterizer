using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace WritableBitmapWindow
{
    internal class Test
    {
        [STAThread]
        static void Main(string[] args)
        {
            var test = new Test();
            test.Run();
        }

        WriteableBitmapWindow window;

        Stopwatch stopwatch = Stopwatch.StartNew();
        double time;

        int x = 0;
        int y = 0;

        Color pixelColor;

        public void Run()
        {
            CompositionTarget.Rendering += Update;

            //pixelColor = Color.FromRgb(255, 128, 255);
            //pixelColor = Color.FromRgb(255, 0, 0);
            pixelColor = Color.FromRgb(255, 255, 255);

            window = new WriteableBitmapWindow();
            window.Show();

            Application app = new Application();
            app.Run();
        }

        private void Update(object sender, EventArgs e)
        {
            var oldTime = time;
            time = stopwatch.Elapsed.TotalSeconds;
            var frameTime = (time - oldTime);

            window.Title = string.Format("Writeable Bitmap - {0}", (1.0f / frameTime).ToString("F1"));

            window.DrawPixel(x, y, pixelColor);
            x++;
            y++;

            if (x >= window.WriteableBitmap.PixelWidth)
            {
                x = 0;
            }

            if (y >= window.WriteableBitmap.PixelHeight)
            {
                y = 0;
            }
        }
    }
}
