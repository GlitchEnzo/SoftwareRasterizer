using SharpDX;

namespace RasterizerCommon
{
    public struct VertexPositionTextureNormal
    {
        public Vector4 Position;

        public Vector3 TextureCoordinates;

        public Vector3 Normal;

        public VertexPositionTextureNormal(Vector4 position, Vector3 texCoords, Vector3 normal)
        {
            Position = position;
            TextureCoordinates = texCoords;
            Normal = normal;
        }
    }
}