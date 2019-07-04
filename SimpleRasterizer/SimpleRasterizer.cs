using RasterizerCommon;
using SharpDX;
using System;
using System.Diagnostics;
using System.Drawing;

namespace SimpleRasterizer
{
    class Program
    {
        static void Main(string[] args)
        {
            var rasterizer = new SimpleRasterizer();
            rasterizer.Run();
        }
    }

    public class SimpleRasterizer
    {
        public void Run()
        {
            Vector2 outputResolution = new Vector2(1280, 720);
            Bitmap bitmap = new Bitmap((int)outputResolution.X, (int)outputResolution.Y, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            ObjModel model = ObjModel.LoadObj("gourd.obj");

            //     0 
            //    / \
            //   1---2
            //
            //   0
            //   | \
            //   1---2
            //	Vector4[] modelspaceVertices = new Vector4[]
            //	{
            //		new Vector4(0.5f, 0.25f, 1.0f, 1.0f),
            //		//new Vector4(0.25f, 0.25f, 1.0f, 1.0f),
            //		new Vector4(0.25f, 0.75f, 1.0f, 1.0f),
            //		new Vector4(0.75f, 0.75f, 1.0f, 1.0f)
            //	};

            Vector4[] modelspaceVertices = new Vector4[]
            {
                new Vector4( 0.0f, 1.0f, 1.0f, 1.0f),
                new Vector4(-0.5f, -1.0f, 1.0f, 1.0f),
                new Vector4( 0.5f, -1.0f, 1.0f, 1.0f),

                new Vector4( 1.0f, 1.0f, 1.0f, 1.0f),
                new Vector4( 0.5f, -1.0f, 1.0f, 1.0f),
                new Vector4( 1.5f, -1.0f, 1.0f, 1.0f),
            };

            // https://gist.github.com/axefrog/b51b4e149c329608eae6
            Matrix worldMatrix = Matrix.Translation(-2, 1, 5); //Matrix.Identity;
            Matrix viewMatrix = Matrix.LookAtLH(new Vector3(0, 0, -1), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            //Matrix projMatrix = Matrix.PerspectiveLH(outputResolution.X, outputResolution.Y, 0.01f, 1000f);
            Matrix projMatrix = Matrix.PerspectiveFovLH((float)Math.PI / 3f, outputResolution.X / outputResolution.Y, 0.01f, 1000f);

            var viewProjMatrix = Matrix.Multiply(viewMatrix, projMatrix);
            var worldViewProjMatrix = worldMatrix * viewProjMatrix;
            //worldViewProjMatrix.Transpose();

            Vector4[] viewspaceVertices = new Vector4[modelspaceVertices.Length];
            for (int i = 0; i < modelspaceVertices.Length; i++)
            {
                viewspaceVertices[i] = Vector4.Transform(modelspaceVertices[i], viewMatrix);
            }

            Vector4[] screenspaceVertices = new Vector4[modelspaceVertices.Length];
            for (int i = 0; i < viewspaceVertices.Length; i++)
            {
                screenspaceVertices[i] = Vector4.Transform(viewspaceVertices[i], projMatrix);
            }

            Vector4[] screenspaceVertices2 = new Vector4[modelspaceVertices.Length];
            for (int i = 0; i < viewspaceVertices.Length; i++)
            {
                screenspaceVertices2[i] = Vector4.Transform(modelspaceVertices[i], worldViewProjMatrix);
            }

            //Rasterize(screenspaceVertices2, bitmap);
            Rasterize(model, worldViewProjMatrix, bitmap);

            // save the rasterized image
            //var currentDirectory = Path.GetDirectoryName(Util.CurrentQueryPath);
            //var pngFilepath = currentDirectory + "\\rasterOutput.png";
            bitmap.Save("rasterOutput.png");

            //Console.WriteLine("Saved: {0}", pngFilepath);
            Process.Start("rasterOutput.png");
        }

        public void Rasterize(ObjModel model, Matrix worldViewProjMatrix, Bitmap outputBitmap)
        {
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

                Vector2 vert0 = ssVert0.ConvertToScreenCoords(outputBitmap.Width, outputBitmap.Height);
                Vector2 vert1 = ssVert1.ConvertToScreenCoords(outputBitmap.Width, outputBitmap.Height);
                Vector2 vert2 = ssVert2.ConvertToScreenCoords(outputBitmap.Width, outputBitmap.Height);

                // compute AABB
                Vector2 aabbMin = Vector2.Min(vert0, Vector2.Min(vert1, vert2));
                Vector2 aabbMax = Vector2.Max(vert0, Vector2.Max(vert1, vert2));

                // clip AABB to screen
                aabbMin = Vector2.Max(aabbMin, Vector2.Zero);
                aabbMax = Vector2.Min(aabbMax, new Vector2(outputBitmap.Width, outputBitmap.Height));

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
                            inside &= EdgeFunction(vert0, vert1, new Vector2(x, y));
                            inside &= EdgeFunction(vert1, vert2, new Vector2(x, y));
                            inside &= EdgeFunction(vert2, vert0, new Vector2(x, y));

                            if (inside)
                            {
                                outputBitmap.SetPixel(x, y, System.Drawing.Color.Blue);
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

        public void Rasterize(Vector4[] screenspaceVerts, Bitmap outputBitmap)
        {
            for (int i = 0; i < screenspaceVerts.Length; i += 3)
            {
                Vector2 vert0 = screenspaceVerts[i + 0].ConvertToScreenCoords(outputBitmap.Width, outputBitmap.Height);
                Vector2 vert1 = screenspaceVerts[i + 1].ConvertToScreenCoords(outputBitmap.Width, outputBitmap.Height);
                Vector2 vert2 = screenspaceVerts[i + 2].ConvertToScreenCoords(outputBitmap.Width, outputBitmap.Height);

                // compute AABB
                Vector2 aabbMin = Vector2.Min(vert0, Vector2.Min(vert1, vert2));
                Vector2 aabbMax = Vector2.Max(vert0, Vector2.Max(vert1, vert2));

                // clip AABB to screen
                aabbMin = Vector2.Max(aabbMin, Vector2.Zero);
                aabbMax = Vector2.Min(aabbMax, new Vector2(outputBitmap.Width, outputBitmap.Height));

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
                            inside &= EdgeFunction(vert0, vert1, new Vector2(x, y));
                            inside &= EdgeFunction(vert1, vert2, new Vector2(x, y));
                            inside &= EdgeFunction(vert2, vert0, new Vector2(x, y));

                            if (inside)
                            {
                                outputBitmap.SetPixel(x, y, System.Drawing.Color.Blue);
                            }
                            else
                            {
                                outputBitmap.SetPixel(x, y, System.Drawing.Color.White);
                            }
                        }
                    }
                }
            }
        }

        // From: https://www.scratchapixel.com/lessons/3d-basic-rendering/rasterization-practical-implementation/rasterization-stage
        public bool EdgeFunction(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X) >= 0);
        }
    }
}