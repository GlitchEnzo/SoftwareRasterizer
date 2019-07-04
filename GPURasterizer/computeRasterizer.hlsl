cbuffer MyConstants : register(b0) // Shader Model 5.0 syntax
//struct MyConstants // Shader Model 5.1+ syntax
{
	//float2 mousePosition; // 4 bytes * 2 = 8 bytes
	//float deltaTime;      // 4 bytes * 1 = 4 bytes
	//float padding; // constant buffers require sizes that are multiples of 16 bytes

	float4x4 worldViewProjMatrix; // 4 bytes * 4 * 4 = 64 bytes
	float2 outputResolution; // 4 bytes * 2 = 8 bytes
	float2 padding; // 4 bytes * 2 = 8 bytes --> TOTAL BYTES = 64 + 8 + 8 = 80 = [16] * 5 (must be multiple of 16)
};
//ConstantBuffer<MyConstants> myData : register(b0); // Shader Model 5.1+ syntax

//RWStructuredBuffer<float3> vertices : register(u0);
//RWStructuredBuffer<uint> indices : register(u1); // should this be uint3 to represent the indices for each triangle instead of sampling 3 times?
StructuredBuffer<float3> vertices : register(t0);
StructuredBuffer<uint3> indices : register(t1);
RWTexture2D<float4> Output : register(u2);

// From: https://www.scratchapixel.com/lessons/3d-basic-rendering/rasterization-practical-implementation/rasterization-stage
bool EdgeFunction(float2 a, float2 b, float2 c)
{
	return ((c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x) >= 0);
}

// https://stackoverflow.com/questions/28543294/convert-screen-space-vertex-to-pixel-space-point-directx
// https://stackoverflow.com/questions/15693231/normalized-device-coordinates
float2 ConvertToScreenCoords(float4 a)
{
	float screenX = (a.x / a.w + 1) * 0.5f * outputResolution.x;
	float screenY = (-a.y / a.w + 1) * 0.5f * outputResolution.y;

	return float2(screenX, screenY);
}

[numthreads(1, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
	// get triangle indices
	//uint index0 = indices[id.x];
	//uint index1 = indices[id.x + 1];
	//uint index2 = indices[id.x + 2];
	uint3 index = indices[id.x];

	// get modelspace vertex positions
	float3 msVert0 = vertices[index.x];
	float3 msVert1 = vertices[index.y];
	float3 msVert2 = vertices[index.z];

	// convert to screenspace vertex positions
	float4 ssVert0 = mul(float4(msVert0, 1), worldViewProjMatrix);
	float4 ssVert1 = mul(float4(msVert1, 1), worldViewProjMatrix);
	float4 ssVert2 = mul(float4(msVert2, 1), worldViewProjMatrix);

	float2 vert0 = ConvertToScreenCoords(ssVert0);
	float2 vert1 = ConvertToScreenCoords(ssVert1);
	float2 vert2 = ConvertToScreenCoords(ssVert2);

	// clip-space to viewport transform
	//float2 vert0 = outputResolution * (float2(0.5, -0.5) * ssVert0.xy + float2(0.5, 0.5));
	//float2 vert1 = outputResolution * (float2(0.5, -0.5) * ssVert1.xy + float2(0.5, 0.5));
	//float2 vert2 = outputResolution * (float2(0.5, -0.5) * ssVert2.xy + float2(0.5, 0.5));

	// compute screenspace AABB
	float2 aabbMin = min(vert0, min(vert1, vert2));
	float2 aabbMax = max(vert0, max(vert1, vert2));

	// clip AABB to screen
	aabbMin = max(aabbMin, 0);
	aabbMax = min(aabbMax, outputResolution);

	// reject out-of-bounds triangles
	[branch]
	if (all(aabbMin < aabbMax))
	{
		// iterate over all the pixels in the AABB
		float2 vPos;
		for (int y = (int)aabbMin.y; y < aabbMax.y; y++)
		{
			for (int x = (int)aabbMin.x; x < aabbMax.x; x++)
			{
				vPos = float2(x, y);
				//Output[uint2(x, y)] = float4(1, 1, 1, 1);

				//determine if inside or outside of triangle
				bool inside = true;
				inside = inside && EdgeFunction(vert0, vert1, vPos);
				inside = inside && EdgeFunction(vert1, vert2, vPos);
				inside = inside && EdgeFunction(vert2, vert0, vPos);

				if (inside)
				{
					Output[uint2(x, y)] = float4(0, 0, 1, 1);
				}
				else
				{
					//Output[uint2(x, y)] = float4(1, 0, 0, 1);
				}
			}
		}
	}	
}