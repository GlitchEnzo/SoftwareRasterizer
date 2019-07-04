using RasterizerCommon;
using SharpDX;
using System;
using System.Diagnostics;
using System.Windows;
using WritableBitmapWindow;

namespace OneBitRasterizer
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var program = new OneBitRasterizer();
            program.Run();
        }
    }

    public class OneBitRasterizer
    {
        WriteableBitmapWindow window;

        Stopwatch stopwatch = Stopwatch.StartNew();
        double time;

        System.Windows.Media.Color pixelColor;

        ObjModel model;

        Vector3 lightDirection = new Vector3(-1, -1, -1);
        Vector3 ambientLight = new Vector3(0.1f, 0.1f, 0.1f);

        Vector2 outputResolution = new Vector2(400, 240);
        float[] grayscaleOutput = new float[400 * 240];
        byte[] blackAndWhiteOutput = new byte[400 * 240]; // really only ever 0 or 1

        public void Run()
        {
            pixelColor = System.Windows.Media.Color.FromRgb(255, 255, 255);

            model = ObjModel.LoadObj("gourd.obj");
            //model = ObjModel.LoadObj("male_head.obj");
            model.CalculateNormals();

            window = new WriteableBitmapWindow(outputResolution.X, outputResolution.Y);
            System.Windows.Media.CompositionTarget.Rendering += Update;
            window.Show();

            Application app = new Application();
            app.Run();
        }

        public void Rasterize(ObjModel model, Matrix worldViewProjMatrix)
        {
            var width = (int)outputResolution.X;
            var height = (int)outputResolution.Y;

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
                                // color = saturate( dot( vLightDir, input.Normal) * vLightColor );
                                float howLit = Vector3.Dot(lightDirection, msVert0.Normal);
                                howLit = MathUtil.Clamp(howLit, 0.2f, 1.0f);
                                //byte colorChannel = (byte)(howLit * 255);
                                //outputBitmap.SetPixel(x, y, System.Windows.Media.Color.FromRgb(colorChannel, colorChannel, colorChannel));
                                grayscaleOutput[x + (y * width)] = howLit;
                            }
                            else
                            {
                                //outputBitmap.SetPixel(x, y, System.Drawing.Color.White);
                            }
                        }
                    }
                }
            }
        }

        public void Rasterize_old(ObjModel model, Matrix worldViewProjMatrix, WriteableBitmapWindow outputBitmap)
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
                                // color = saturate( dot( vLightDir, input.Normal) * vLightColor );
                                float howLit = Vector3.Dot(lightDirection, msVert0.Normal);
                                howLit = SharpDX.MathUtil.Clamp(howLit, 0.2f, 1.0f);
                                byte colorChannel = (byte)(howLit * 255);
                                outputBitmap.SetPixel(x, y, System.Windows.Media.Color.FromRgb(colorChannel, colorChannel, colorChannel));
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
            var worldMatrix = Matrix.RotationY(rotationAngle) * Matrix.Translation(-2, 1, 5); //gourd
            //var worldMatrix = Matrix.RotationY(rotationAngle) * Matrix.Translation(-2, 1, 50); //face

            Matrix viewMatrix = Matrix.LookAtLH(new Vector3(0, 0, -1), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            //Matrix projMatrix = Matrix.PerspectiveLH(outputResolution.X, outputResolution.Y, 0.01f, 1000f);
            Matrix projMatrix = Matrix.PerspectiveFovLH((float)Math.PI / 3f, outputResolution.X / outputResolution.Y, 0.01f, 1000f);

            var viewProjMatrix = Matrix.Multiply(viewMatrix, projMatrix);
            var worldViewProjMatrix = worldMatrix * viewProjMatrix;
            //worldViewProjMatrix.Transpose();

            window.Clear(System.Windows.Media.Color.FromRgb(0, 0, 0));
            Array.Clear(grayscaleOutput, 0, grayscaleOutput.Length);
            Rasterize(model, worldViewProjMatrix);
            
            // TODO: Convert grayscale image to "1-bit" black and white
            //Array.Clear(blackAndWhiteOutput, 0, blackAndWhiteOutput.Length);
            dither_floyd_steinberg();

            window.SetGrayscaleImage(grayscaleOutput);
        }

        private void dither_floyd_steinberg()
        {
            for (var y = 0; y < (int)outputResolution.Y; y++)
            {
                for (var x = 0; x < (int)outputResolution.X; x++)
                {
                    var grayscaleValue = grayscaleOutput[x + (y * (int)outputResolution.X)];
                    var roundedValue = grayscaleValue < 0.5f ? 0.0f : 1.0f;
                    var error = grayscaleValue - roundedValue;

                    //blackAndWhiteOutput[x + (y * (int)outputResolution.X)] = grayscaleValue < 0.5f ? (byte)0 : (byte)255;
                    grayscaleOutput[x + (y * (int)outputResolution.X)] = roundedValue;

                    if (x + 1 < outputResolution.X)
                        grayscaleOutput[x + 1 + ((y + 0) * (int)outputResolution.X)] += error * 7 / 16.0f;
                    if (y + 1 < outputResolution.Y)
                        grayscaleOutput[x - 1 + ((y + 1) * (int)outputResolution.X)] += error * 3 / 16.0f;
                    if (y + 1 < outputResolution.Y)
                        grayscaleOutput[x + 0 + ((y + 1) * (int)outputResolution.X)] += error * 5 / 16.0f;
                    if (x + 1 < outputResolution.X && y + 1 < outputResolution.Y)
                        grayscaleOutput[x + 1 + ((y + 1) * (int)outputResolution.X)] += error * 1 / 16.0f;
                }
            }
        }

        //private void dither_floyd_steinberg(WriteableBitmapWindow window)
        //{
        //    window.LockBitmap();

        //    for (var y = 0; y < window.Image.Height; y++)
        //    {
        //        for (var x = 0; x < window.Image.Width; x++)
        //        {
        //            //var pixelValue = (float)window.GetPixel(x, y).R;
        //            //var roundedValue = (float)Math.Round(pixelValue);
        //            //var error = pixelValue - roundedValue;

        //            var pixelValue = window.GetPixel(x, y).R;
        //            var roundedValue = pixelValue < 128 ? (byte)0 : (byte)255;
        //            var error = (byte)(pixelValue - roundedValue);

        //            var roundedColor = new System.Windows.Media.Color();
        //            roundedColor.R = roundedColor.G = roundedColor.B = roundedValue;
        //            window.SetPixel(x, y, roundedColor);

        //            window.AdjustPixel(x+1,   y, (byte)(error * 7 / 16.0f));
        //            window.AdjustPixel(x-1, y+1, (byte)(error * 3 / 16.0f));
        //            window.AdjustPixel(  x, y+1, (byte)(error * 5 / 16.0f));
        //            window.AdjustPixel(x+1, y+1, (byte)(error * 1 / 16.0f));
        //        }
        //    }

        //    window.UnlockBitmap();
        //}
    }
}
