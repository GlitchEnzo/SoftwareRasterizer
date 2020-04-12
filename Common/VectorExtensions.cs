//using SharpDX;
using System.Numerics;

namespace RasterizerCommon
{
    public static class VectorExtensions
    {
        public static bool AllLess(this Vector2 a, Vector2 b)
        {
            return a.X < b.X && a.Y < b.Y;
        }

        public static Vector2 ConvertToScreenCoords(this Vector3 a, float screenWidth, float screenHeight)
        {
            // TODO: have vec4 with a W?
            //float w = 0;
            //float screenX = (a.X / w + 1) * 0.5f * screenWidth;
            //float screenY = (a.Y / w + 1) * 0.5f * screenHeight;

            float screenX = a.X * screenWidth;
            float screenY = a.Y * screenHeight;

            return new Vector2(screenX, screenY);
        }

        // https://stackoverflow.com/questions/28543294/convert-screen-space-vertex-to-pixel-space-point-directx
        // https://stackoverflow.com/questions/15693231/normalized-device-coordinates
        public static Vector2 ConvertToScreenCoords(this Vector4 a, float screenWidth, float screenHeight)
        {
            float screenX = (a.X / a.W + 1) * 0.5f * screenWidth;
            float screenY = (-a.Y / a.W + 1) * 0.5f * screenHeight;

            //float screenX = (a.X / a.W) * 0.5f * screenWidth;
            //float screenY = (a.Y / a.W) * 0.5f * screenHeight;

            //float screenX = a.X * screenWidth;
            //float screenY = a.Y * screenHeight;

            return new Vector2(screenX, screenY);
        }

        public static Vector3 ToVector3(this Vector4 a)
        {
            return new Vector3(a.X, a.Y, a.Z);
        }

        public static void Normalize(this Vector3 a)
        {
            a = Vector3.Normalize(a);
        }
    }
}
