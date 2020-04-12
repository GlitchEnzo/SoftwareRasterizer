//using SharpDX;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.IO;

namespace RasterizerCommon
{
    public class ObjModel
    {
        private struct FaceIndex
        {
            public int VertexIndex;
            public int UVIndex;
            public int NormalIndex;

            private static char[] IndexSeperator = new[] { '/' };

            public FaceIndex(string indexString)
            {
                VertexIndex = 0;
                UVIndex = 0;
                NormalIndex = 0;

                var indexData = indexString.Split(IndexSeperator);

                if (indexData.Length == 1)
                {
                    // index only
                    VertexIndex = int.Parse(indexData[0]) - 1;
                }
                else if (indexData.Length == 2)
                {
                    // index & tex coords
                    VertexIndex = int.Parse(indexData[0]) - 1;
                    UVIndex = int.Parse(indexData[1]) - 1;
                }
                else if (indexData.Length == 3)
                {
                    // index, possible tex coords, & normal
                    VertexIndex = int.Parse(indexData[0]) - 1;

                    // ensure UVs present, since they are optional
                    if (int.TryParse(indexData[1], out UVIndex))
                    {
                        UVIndex--;
                    }

                    NormalIndex = int.Parse(indexData[2]) - 1;
                }
            }
        }

        public VertexPositionTextureNormal[] VertexData;

        public uint[] Indices;

        public void CalculateNormals(bool isSmoothSurface = false)
        {
            if (isSmoothSurface)
            {
                // it is a smooth surface where each vertex is shared, therefore the vertex normal should be the "average" of the normals of the faces that share the vertex
                // requires finding the adjacent triangles
            }
            else
            {
                // calculate for each face/triangle
                // this can be horribly broken if verts are shared, but it's not a smooth surface - think of a cube
                // the last triangle normal calculated will be the final normal for the vertex, regardless of how horrible that may look

                for (int i = 0; i < Indices.Length; i+=3)
                {
                    // TODO: Ensure the winding is correct so that the normal points OUT of the mesh instead of INSIDE the mesh
                    var indexA = Indices[i];
                    var indexB = Indices[i+1];
                    var indexC = Indices[i+2];

                    var vectorAB = Vector3.Normalize(VertexData[indexB].Position.ToVector3() - VertexData[indexA].Position.ToVector3());
                    var vectorAC = Vector3.Normalize(VertexData[indexC].Position.ToVector3() - VertexData[indexA].Position.ToVector3());

                    var normal = Vector3.Cross(vectorAB, vectorAC);

                    VertexData[indexA].Normal = normal;
                    VertexData[indexB].Normal = normal;
                    VertexData[indexC].Normal = normal;
                }
            }
        }

        // From: https://github.com/GlitchEnzo/Hayao/blob/master/Vapor/Graphics/ObjLoader.cs
        public static ObjModel LoadObj(string filepath)
        {
            var lines = File.ReadLines(filepath);

            List<Vector4> positions = new List<Vector4>();
            List<Vector3> texcoords = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();

            List<VertexPositionTextureNormal> vertexData = null;
            List<uint> indices = new List<uint>();

            var tokenSeparator = new[] { ' ' };
            //var faceSeparator = new[] { '/' };
            foreach (var line in lines)
            {
                // skip comments
                if (line.StartsWith("#"))
                {
                    continue;
                }

                var tokens = line.Split(tokenSeparator, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                {
                    continue;
                }

                if (tokens[0] == "v")
                {
                    // read vertex positions
                    if (tokens.Length == 4)
                    {
                        Vector4 position = new Vector4(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]), 1.0f);
                        positions.Add(position);
                    }
                    else if (tokens.Length == 5)
                    {
                        Vector4 position = new Vector4(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]), float.Parse(tokens[4]));
                        positions.Add(position);
                    }
                }
                else if (tokens[0] == "vt")
                {
                    // read texture coordinates
                    if (tokens.Length == 3)
                    {
                        Vector3 uv = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), 0.0f);
                        texcoords.Add(uv);
                    }
                    else if (tokens.Length == 4)
                    {
                        Vector3 uvw = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]));
                        texcoords.Add(uvw);
                    }
                }
                else if (tokens[0] == "vn")
                {
                    // read normals
                    Vector3 normal = new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3]));
                    normals.Add(normal);
                }
                else if (tokens[0] == "vp")
                {
                    // TODO: read parameter space vertices
                    Console.WriteLine("Parameter space vertices not yet supported!");
                }
                else if (tokens[0] == "f")
                {
                    // read faces

                    // create the initial vertex list from the positions
                    if (vertexData == null)
                    {
                        vertexData = new List<VertexPositionTextureNormal>();

                        foreach (var position in positions)
                        {
                            VertexPositionTextureNormal vertex = new VertexPositionTextureNormal();
                            vertex.Position = position;
                            vertexData.Add(vertex);
                        }
                    }

                    var faceIndices = new List<FaceIndex>();
                    // read the indices, tex coords, and normal
                    for (int i = 1; i < tokens.Length; i++)
                    {
                        //var vertData = tokens[i].Split(faceSeparator);
                        var faceIndex = new FaceIndex(tokens[i]);
                        faceIndices.Add(faceIndex);

                        // Update the vertex with UVs and Normal
                        var vertex = vertexData[faceIndex.VertexIndex];
                        if (faceIndex.UVIndex < texcoords.Count) vertex.TextureCoordinates = texcoords[faceIndex.UVIndex];
                        if (faceIndex.NormalIndex < normals.Count) vertex.Normal = normals[faceIndex.NormalIndex];
                        vertexData[faceIndex.VertexIndex] = vertex;
                    }

                    // Load any triangle strip
                    // See here: http://www.alecjacobson.com/weblog/?p=1548#comment-353129
                    // See also here: https://stackoverflow.com/a/43422763

                    // From here: https://stackoverflow.com/a/43422763
                    indices.Add((uint)faceIndices[0].VertexIndex);
                    indices.Add((uint)faceIndices[1].VertexIndex);
                    //indices.Add((uint)faceIndices[0].VertexIndex);
                    indices.Add((uint)faceIndices[2].VertexIndex);

                    for (int i = 3; i < faceIndices.Count; i++)
                    {
                        indices.Add((uint)faceIndices[i-3].VertexIndex);
                        indices.Add((uint)faceIndices[i-1].VertexIndex);
                        //indices.Add((uint)faceIndices[i - 3].VertexIndex);
                        indices.Add((uint)faceIndices[i].VertexIndex);
                    }
                }
            }

            var mesh = new ObjModel();
            mesh.VertexData = vertexData.ToArray();
            mesh.Indices = indices.ToArray();
            return mesh;
        }
    }
}
