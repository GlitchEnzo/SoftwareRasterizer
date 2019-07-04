using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace SharpDXHelper
{
    public static class Helper
    {
        /// <summary>
        /// Create a new GPU buffer from the given CPU struct array, optionally copying the data by default.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="device"></param>
        /// <param name="dataArray"></param>
        /// <param name="copyData"></param>
        /// <returns></returns>
        public static Buffer CreateStructuredBufferFromArray<T>(Device device, T[] dataArray, bool copyData = true) where T : struct
        {
            var structSize = Marshal.SizeOf(default(T));

            var desc = new BufferDescription()
            {
                BindFlags = BindFlags.UnorderedAccess | BindFlags.ShaderResource,
                StructureByteStride = structSize,
                OptionFlags = ResourceOptionFlags.BufferStructured,
                SizeInBytes = structSize * dataArray.Length,
            };

            if (copyData)
            {
                var dataPtr = Marshal.UnsafeAddrOfPinnedArrayElement(dataArray, 0);
                return new Buffer(device, dataPtr, desc);
            }

            return new Buffer(device, desc);
        }
        
        /// <summary>
        /// Create a GPU buffer represending an HLSL constant buffer using the given CPU struct.
        /// See here: https://gamedev.stackexchange.com/questions/71172/how-to-set-shader-global-variable-in-sharpdx-without-using-effect-class
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="device"></param>
        /// <param name="cpuData"></param>
        /// <returns></returns>
        public static Buffer CreateConstantBuffer<T>(Device device, T cpuData) where T : struct
        {
            var structSize = Marshal.SizeOf(default(T));

            // Verify that the incoming size is a multiple of 16 bytes, since that is a requirement for constant buffers
            System.Diagnostics.Trace.Assert(structSize % 16 == 0, string.Format("The given struct ({0}) is not a multiple of 16 bytes.", typeof(T)));

            var desc = new BufferDescription()
            {
                BindFlags = BindFlags.ConstantBuffer,// | BindFlags.ShaderResource,
                StructureByteStride = 0,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = structSize,
                Usage = ResourceUsage.Dynamic,
                CpuAccessFlags = CpuAccessFlags.Write
            };

            var buffer = Buffer.Create(device, ref cpuData, desc);

            return buffer;
        }

        // https://www.gamedev.net/forums/topic/649238-sharpdx-how-to-initialize-constantbuffer/
        public static void UpdateConstantBuffer<T>(Device device, Buffer resource, T cpuData) where T : struct
        {
            var mappedSubresource = device.ImmediateContext.MapSubresource(resource, 0, MapMode.WriteDiscard, MapFlags.None);

            try
            {
                Utilities.Write(mappedSubresource.DataPointer, ref cpuData);
                
                //device.ImmediateContext.UpdateSubresource(ref cpuData, resource);
                //device.ImmediateContext.UpdateSubresource(ref cpuData, resource, 0, Marshal.SizeOf(default(T)), 0, null);
            }
            finally
            {
                device.ImmediateContext.UnmapSubresource(resource, 0);
            }
        }

        public static ShaderResourceView CreateBufferSRV(Device device, Buffer buffer)
        {
            // https://github.com/walbourn/directx-sdk-samples/blob/master/BasicCompute11/BasicCompute11.cpp
            var desc = new ShaderResourceViewDescription()
            {
                Dimension = ShaderResourceViewDimension.ExtendedBuffer,
                Format = Format.Unknown,
            };

            desc.BufferEx.FirstElement = 0;
            desc.BufferEx.ElementCount = buffer.Description.SizeInBytes / buffer.Description.StructureByteStride;

            return new ShaderResourceView(device, buffer, desc);
        }

        public static UnorderedAccessView CreateTextureUAV(Device device, Texture2D texture)
        {
            var desc = new UnorderedAccessViewDescription()
            {
                Dimension = UnorderedAccessViewDimension.Texture2D,
                Format = texture.Description.Format,
                Texture2D = { MipSlice = 0 }
            };

            return new UnorderedAccessView(device, texture, desc);
        }

        public static UnorderedAccessView CreateBufferUAV(Device device, Buffer buffer)
        {
            var desc = new UnorderedAccessViewDescription()
            {
                Dimension = UnorderedAccessViewDimension.Buffer,
                Format = Format.Unknown,
            };

            desc.Buffer.FirstElement = 0;
            desc.Buffer.ElementCount = buffer.Description.SizeInBytes / buffer.Description.StructureByteStride;

            return new UnorderedAccessView(device, buffer, desc);
        }

        /// <summary>
        /// Copies the data in a GPU buffer into a CPU struct array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="device"></param>
        /// <param name="gpuBuffer"></param>
        /// <param name="cpuArray"></param>
        public static void CopyBufferDataIntoArray<T>(Device device, Buffer gpuBuffer, T[] cpuArray) where T : struct
        {
            var gpuDesc = gpuBuffer.Description;

            var cpuDesc = new BufferDescription()
            {
                BindFlags = BindFlags.None,
                Usage = ResourceUsage.Staging,
                StructureByteStride = gpuDesc.StructureByteStride,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = gpuDesc.SizeInBytes,
                CpuAccessFlags = CpuAccessFlags.Read
            };

            var cpuBuffer = new Buffer(device, cpuDesc);

            device.ImmediateContext.CopyResource(gpuBuffer, cpuBuffer);
            var mappedSubresource = device.ImmediateContext.MapSubresource(cpuBuffer, 0, MapMode.Read, MapFlags.None);
            var mappedSubresourceDataPointer = mappedSubresource.DataPointer;

            try
            {
                var structSize = Marshal.SizeOf(default(T));
                var arrayPointer = Marshal.UnsafeAddrOfPinnedArrayElement(cpuArray, 0);
                Utilities.CopyMemory(arrayPointer, mappedSubresourceDataPointer, structSize * cpuArray.Length);
            }
            finally
            {
                device.ImmediateContext.UnmapSubresource(cpuBuffer, 0);
            }
        }

        // TODO: Fix the R and B channels being swapped.
        // The method here doesn't appear to work: https://github.com/sharpdx/SharpDX-Samples/blob/master/Desktop/Direct3D11.1/ScreenCapture/Program.cs
        public static Bitmap CopyTextureToBitmap(Device device, Texture2D gpuTexture)
        {
            var gpuDesc = gpuTexture.Description;

            var cpuDesc = new Texture2DDescription()
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = gpuDesc.Format, // Make this the proper swapped channels?
                //Format = Format.B8G8R8A8_UNorm,
                Width = gpuDesc.Width,
                Height = gpuDesc.Height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };

            var cpuTexture = new Texture2D(device, cpuDesc);

            device.ImmediateContext.CopyResource(gpuTexture, cpuTexture);
            var mappedSubresource = device.ImmediateContext.MapSubresource(cpuTexture, 0, MapMode.Read, MapFlags.None);
            var mappedSubresourceDataPointer = mappedSubresource.DataPointer;

            try
            {
                Bitmap bitmap = new Bitmap(gpuDesc.Width, gpuDesc.Height, PixelFormat.Format32bppArgb);
                BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, gpuDesc.Width, gpuDesc.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

                try
                {
                    Utilities.CopyMemory(bitmapData.Scan0, mappedSubresourceDataPointer, gpuDesc.Width * gpuDesc.Height * 4);
                    return bitmap;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            finally
            {
                device.ImmediateContext.UnmapSubresource(cpuTexture, 0);
            }
        }
    }
}
