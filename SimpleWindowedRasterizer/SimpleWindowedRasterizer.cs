using RasterizerCommon;
//using SharpDX;
using System.Numerics;
using Matrix = System.Numerics.Matrix4x4;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WritableBitmapWindow;

namespace SimpleWindowedRasterizer
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            //var program = new Program();
            //program.Run();

            var program = new SimpleWindowedRasterizer();
            program.Run();
        }

        //WriteableBitmapWindow window;

        //Stopwatch stopwatch = Stopwatch.StartNew();
        //double time;

        //int x = 0;
        //int y = 0;

        //System.Windows.Media.Color pixelColor;

        //public void Run()
        //{
        //    System.Windows.Media.CompositionTarget.Rendering += Update;

        //    pixelColor = System.Windows.Media.Color.FromRgb(255, 255, 255);

        //    window = new WriteableBitmapWindow();
        //    window.Show();

        //    Application app = new Application();
        //    app.Run();
        //}

        //private void Update(object sender, EventArgs e)
        //{
        //    var oldTime = time;
        //    time = stopwatch.Elapsed.TotalSeconds;
        //    var frameTime = (time - oldTime);

        //    window.Title = string.Format("Writeable Bitmap - {0}", (1.0f / frameTime).ToString("F1"));

        //    window.DrawPixel(x, y, pixelColor);
        //    x++;
        //    y++;

        //    if (x >= window.WriteableBitmap.PixelWidth)
        //    {
        //        x = 0;
        //    }

        //    if (y >= window.WriteableBitmap.PixelHeight)
        //    {
        //        y = 0;
        //    }
        //}
    }

    public class SimpleWindowedRasterizer
    {
        WriteableBitmapWindow window;

        Stopwatch stopwatch = Stopwatch.StartNew();
        double time;

        System.Windows.Media.Color pixelColor;

        ObjModel model;
        Vector2 outputResolution;

        public void Run()
        {
            pixelColor = System.Windows.Media.Color.FromRgb(255, 255, 255);

            model = ObjModel.LoadObj("gourd.obj");
            //model = ObjModel.LoadObj("male_head.obj");

            window = new WriteableBitmapWindow();
            window.Loaded += Window_Loaded;
            window.Show();

            Application app = new Application();
            app.Run();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            outputResolution = new Vector2((float)window.ActualWidth, (float)window.ActualHeight);

            System.Windows.Media.CompositionTarget.Rendering += Update;
        }

        public void Rasterize(ObjModel model, Matrix worldViewProjMatrix, WriteableBitmapWindow outputBitmap)
        {
            var width = (float)outputBitmap.WriteableBitmap.PixelWidth;
            var height = (float)outputBitmap.WriteableBitmap.PixelHeight;

            outputBitmap.LockBitmap();

            for (int i = 0; i < model.Indices.Length; i += 3)
            {
                // get modelspace verts
                var msVert0 = model.VertexData[model.Indices[i + 0]];
                var msVert1 = model.VertexData[model.Indices[i + 1]];
                var msVert2 = model.VertexData[model.Indices[i + 2]];

                // convert to screenspace verts
                var ssVert0 = Vector4.Transform(msVert0.Position, worldViewProjMatrix);
                var ssVert1 = Vector4.Transform(msVert1.Position, worldViewProjMatrix);
                var ssVert2 = Vector4.Transform(msVert2.Position, worldViewProjMatrix);

                Vector2 vert0 = ssVert0.ConvertToScreenCoords(width, height);
                Vector2 vert1 = ssVert1.ConvertToScreenCoords(width, height);
                Vector2 vert2 = ssVert2.ConvertToScreenCoords(width, height);

                // compute AABB
                Vector2 aabbMin = Vector2.Min(vert0, Vector2.Min(vert1, vert2));
                Vector2 aabbMax = Vector2.Max(vert0, Vector2.Max(vert1, vert2));

                // clip AABB to screen
                aabbMin = Vector2.Max(aabbMin, Vector2.Zero);
                aabbMax = Vector2.Min(aabbMax, new Vector2(width, height));

                // Round AABB to pixels
                //Vector2 TriMin = Vector2.Floor(vBBMin);
                //Vector2 TriMax = Vector2.Ceil(vBBMax);

                // loop over all of the pixels in the AABB
                if (aabbMin.AllLess(aabbMax))
                {
                    for (int y = (int)aabbMin.Y; y < aabbMax.Y; y++)
                    {
                        for (int x = (int)aabbMin.X; x < aabbMax.X; x++)
                        {
                            //determine if inside or outside of triangle
                            //outputBitmap.SetPixel(x, y, System.Drawing.Color.White);

                            bool inside = true;
                            var point = new Vector2(x, y);
                            inside &= EdgeFunction(ref vert0, ref vert1, ref point);
                            inside &= EdgeFunction(ref vert1, ref vert2, ref point);
                            inside &= EdgeFunction(ref vert2, ref vert0, ref point);

                            if (inside)
                            {
                                outputBitmap.SetPixel(x, y, System.Windows.Media.Color.FromRgb(0, 0, 255));
                            }
                            else
                            {
                                //outputBitmap.SetPixel(x, y, System.Drawing.Color.White);
                            }
                        }
                    }
                }
            }

            outputBitmap.UnlockBitmap();
        }

        // From: https://www.scratchapixel.com/lessons/3d-basic-rendering/rasterization-practical-implementation/rasterization-stage
        public bool EdgeFunction(ref Vector2 a, ref Vector2 b, ref Vector2 c)
        {
            return ((c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X) >= 0);
        }

        float angleInDegrees = 90;

        private void Update(object sender, EventArgs e)
        {
            var oldTime = time;
            time = stopwatch.Elapsed.TotalSeconds;
            var frameTime = (time - oldTime);

            window.Title = string.Format("Simple Rasterizer - {0}", (1.0f / frameTime).ToString("F1"));

            // https://gist.github.com/axefrog/b51b4e149c329608eae6
            //var worldMatrix = Matrix.Translation(-2, 1, 5); //Matrix.Identity;
            var rotationAngle = angleInDegrees++ * (3.1415f / 180);
            var worldMatrix = Matrix.CreateRotationY(rotationAngle) * Matrix.CreateTranslation(-2, 1, 5); //gourd
            //var worldMatrix = Matrix.RotationY(rotationAngle) * Matrix.Translation(-2, 1, 50); //face

            Matrix viewMatrix = MatrixExtensions.LookAtLH(new Vector3(0, 0, -1), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            //Matrix projMatrix = Matrix.PerspectiveLH(outputResolution.X, outputResolution.Y, 0.01f, 1000f);
            Matrix projMatrix = MatrixExtensions.PerspectiveFovLH((float)Math.PI / 3f, outputResolution.X / outputResolution.Y, 0.01f, 1000f);

            var viewProjMatrix = Matrix.Multiply(viewMatrix, projMatrix);
            var worldViewProjMatrix = worldMatrix * viewProjMatrix;
            //worldViewProjMatrix.Transpose();

            window.Clear(System.Windows.Media.Color.FromRgb(0, 0, 0));
            Rasterize(model, worldViewProjMatrix, window);
        }
    }
}
