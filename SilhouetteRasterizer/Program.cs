using RasterizerCommon;
//using SharpDX;
using System.Numerics;
using Matrix = System.Numerics.Matrix4x4;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace SilhouetteRasterizer
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
            var outputResolution = new Vector2(1280, 720);
            var bitmap = new Bitmap((int)outputResolution.X, (int)outputResolution.Y, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            //var model = ObjModel.LoadObj("gourd.obj");
            var model = ObjModel.LoadObj("male_head.obj");

            // https://gist.github.com/axefrog/b51b4e149c329608eae6
            var rotationAngle = 90 * (3.1415f / 180);
            //var worldMatrix = Matrix.Identity;
            //var worldMatrix = Matrix.Translation(-2, 1, 50);
            //var worldMatrix = Matrix.Translation(-2, 1, 50) * Matrix.RotationY(rotationAngle);
            var worldMatrix = Matrix.CreateRotationY(rotationAngle) * Matrix.CreateTranslation(-2, 1, 50); //correct

            var cameraPos = new Vector3(0, 0, -1);
            var cameraTarget = new Vector3(0, 0, 0);
            var cameraUp = new Vector3(0, 1, 0);
            var viewMatrix = MatrixExtensions.LookAtLH(cameraPos, cameraTarget, cameraUp);
            //var projMatrix = Matrix.PerspectiveLH(outputResolution.X, outputResolution.Y, 0.01f, 1000f);
            var projMatrix = MatrixExtensions.PerspectiveFovLH((float)Math.PI / 3f, outputResolution.X / outputResolution.Y, 0.01f, 1000f);

            var viewProjMatrix = Matrix.Multiply(viewMatrix, projMatrix);
                
            //var worldViewProjMatrix = viewProjMatrix * worldMatrix;
            var worldViewProjMatrix = worldMatrix * viewProjMatrix; //correct
            //worldViewProjMatrix.Transpose();

            FindSilhouetteLines(model, worldMatrix, cameraPos);

            Rasterize(model, worldViewProjMatrix, bitmap);

            // save the rasterized image
            bitmap.Save("rasterOutput.png");
            Process.Start("rasterOutput.png");
        }

        public void FindSilhouetteLines(ObjModel model, Matrix worldMatrix, Vector3 cameraPosition)
        {
            for (int i = 0; i < model.Indices.Length; i += 3)
            {
                // get modelspace verts
                var vert0 = model.VertexData[model.Indices[i + 0]];
                var vert1 = model.VertexData[model.Indices[i + 1]];
                var vert2 = model.VertexData[model.Indices[i + 2]];

                // convert to worldspace verts
                var wsVert0 = Vector4.Transform(vert0.Position, worldMatrix);
                var wsVert1 = Vector4.Transform(vert1.Position, worldMatrix);
                var wsVert2 = Vector4.Transform(vert2.Position, worldMatrix);

                // calculate the dot product of the vertex normal and the view vector to each vert
                var viewDirection0 = wsVert0.ToVector3() - cameraPosition;
                viewDirection0.Normalize();
                var v0NdotV = Vector3.Dot(vert0.Normal, viewDirection0);

                var viewDirection1 = wsVert1.ToVector3() - cameraPosition;
                viewDirection1.Normalize();
                var v1NdotV = Vector3.Dot(vert1.Normal, viewDirection1);

                var viewDirection2 = wsVert2.ToVector3() - cameraPosition;
                viewDirection2.Normalize();
                var v2NdotV = Vector3.Dot(vert2.Normal, viewDirection2);

                var d0Positive = v0NdotV >= 0;
                var d1Positive = v1NdotV >= 0;
                var d2Positive = v2NdotV >= 0;

                var abs0 = Math.Abs(v0NdotV);
                var abs1 = Math.Abs(v1NdotV);
                var abs2 = Math.Abs(v2NdotV);

                List<Vector3> silhouettePoints = new List<Vector3>();
                if (d0Positive != d1Positive)
                {
                    var silPoint = Lerp(abs0, abs1, wsVert0.ToVector3(), wsVert1.ToVector3());
                    silhouettePoints.Add(silPoint);
                }

                if (d1Positive != d2Positive)
                {
                    var silPoint = Lerp(abs1, abs2, wsVert1.ToVector3(), wsVert2.ToVector3());
                    silhouettePoints.Add(silPoint);
                }

                if (d2Positive != d0Positive)
                {
                    var silPoint = Lerp(abs2, abs0, wsVert2.ToVector3(), wsVert0.ToVector3());
                    silhouettePoints.Add(silPoint);
                }

                if (silhouettePoints.Count == 2)
                {
                    Console.WriteLine("Found silhouette line! {0} to {1}", silhouettePoints[0], silhouettePoints[1]);
                }
                else if (silhouettePoints.Count > 2)
                {
                    Console.WriteLine("ERROR!");
                }
            }
        }

        private Vector3 Lerp(float di, float dj, Vector3 xi, Vector3 xj)
        {
            return dj / (di + dj) * xi + di / (di + dj) * xj;
        }

        public void Rasterize(List<Line> lines, Matrix viewProjectionMatrix, Bitmap outputBitmap)
        {

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

        // From: https://www.scratchapixel.com/lessons/3d-basic-rendering/rasterization-practical-implementation/rasterization-stage
        public bool EdgeFunction(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X) >= 0);
        }
    }
}