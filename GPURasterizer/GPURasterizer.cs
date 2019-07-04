using RasterizerCommon;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DirectInput;
using SharpDX.DXGI;
using SharpDX.Windows;
using SharpDXHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

using Device = SharpDX.Direct3D11.Device;

namespace GPURasterizer
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            GPURasterizer program = new GPURasterizer();
            program.Run();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MyConstants
    {
        public Matrix worldViewProjMatrix; // 4 bytes * 4 * 4 = 64 bytes
        public Vector2 outputResolution;   // 4 bytes * 2 = 8 bytes
        public Vector2 padding;            // 4 bytes * 2 = 8 bytes --> TOTAL BYTES = 64 + 8 + 8 = 80 = [16] * 5 (must be multiple of 16)
    }

    public class GPURasterizer : IDisposable
    {
        RenderForm renderForm;

        const int Width = 1280;
        const int Height = 720;

        Device device;
        DeviceContext context;
        SwapChain swapChain;
        RenderTargetView renderTargetView;
        UnorderedAccessView backbufferUAV;
        SharpDX.Mathematics.Interop.RawColor4 clearColor = new SharpDX.Mathematics.Interop.RawColor4();

        Stopwatch stopwatch = Stopwatch.StartNew();
        double time;

        Keyboard keyboard;

        ComputeShader computeShader;
        MyConstants constants;
        SharpDX.Direct3D11.Buffer constantBuffer;

        //UnorderedAccessView vertexBufferUAV;
        //UnorderedAccessView indexBufferUAV;

        ShaderResourceView vertexBufferView;
        ShaderResourceView indexBufferView;

        ObjModel model;

        public GPURasterizer()
        {
            renderForm = new RenderForm("GPU Rasterizer")
            {
                ClientSize = new System.Drawing.Size(Width, Height),
                AllowUserResizing = false
            };

            //renderForm.MouseMove += (sender, args) => {
            //    mousePosition.X = args.X;
            //    mousePosition.Y = args.Y;

            //    constants.mousePosition.X = args.X;
            //    constants.mousePosition.Y = args.Y;
            //};

            model = ObjModel.LoadObj("gourd.obj");
            //model = ObjModel.LoadObj("male_head.obj");

            InitializeDeviceResources();
            InitializeComputeShader("computeRasterizer.hlsl");

            var directInput = new DirectInput();
            keyboard = new Keyboard(directInput);
            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
        }

        private void InitializeDeviceResources()
        {
            ModeDescription backBufferDesc = new ModeDescription(Width, Height, new Rational(60, 1), Format.R8G8B8A8_UNorm);

            SwapChainDescription swapChainDesc = new SwapChainDescription()
            {
                ModeDescription = backBufferDesc,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput | Usage.UnorderedAccess, // THIS IS KEY!
                BufferCount = 1,
                OutputHandle = renderForm.Handle,
                IsWindowed = true
            };

            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.Debug, swapChainDesc, out device, out swapChain);
            context = device.ImmediateContext;

            using (Texture2D backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
            {
                renderTargetView = new RenderTargetView(device, backBuffer);
                backbufferUAV = new UnorderedAccessView(device, backBuffer);
            }
        }

        void InitializeComputeShader(string shaderPath)
        {
            var compilationResult = ShaderBytecode.CompileFromFile(shaderPath, "main", "cs_5_0", ShaderFlags.Debug);
            computeShader = new ComputeShader(device, compilationResult.Bytecode);

            // create the vertex buffer
            //     0 
            //    / \
            //   1---2
            Vector3[] modelspaceVertices = new Vector3[]
            {
                new Vector3( 0.0f, 1.0f, 1.0f),
                new Vector3(-0.5f, -1.0f, 1.0f),
                new Vector3( 0.5f, -1.0f, 1.0f),
            };
            //var vertexBufferBuffer = Helper.CreateStructuredBufferFromArray(device, modelspaceVertices);

            var verts = model.VertexData.Select(v => v.Position.ToVector3()).ToArray(); // whew, that is probably super inefficient! good thing it's only done once.
            var vertexBufferBuffer = Helper.CreateStructuredBufferFromArray(device, verts);
            //vertexBufferUAV = Helper.CreateBufferUAV(device, vertexBufferBuffer);
            vertexBufferView = Helper.CreateBufferSRV(device, vertexBufferBuffer);

            // create the index buffer
            uint[] vertexIndices = new uint[] { 0, 1, 2 };
            //var indexBufferBuffer = Helper.CreateStructuredBufferFromArray(device, vertexIndices);
            var indexBufferBuffer = Helper.CreateStructuredBufferFromArray(device, model.Indices);
            //indexBufferUAV = Helper.CreateBufferUAV(device, indexBufferBuffer);
            indexBufferView = Helper.CreateBufferSRV(device, indexBufferBuffer);

            // create the world view projection matrix
            Matrix worldMatrix = Matrix.Identity;
            //Matrix worldMatrix = Matrix.Translation(-2, 1, 5); //Matrix.Identity;
            Matrix viewMatrix = Matrix.LookAtLH(new Vector3(0, 0, -100), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            //Matrix projMatrix = Matrix.PerspectiveLH(outputResolution.X, outputResolution.Y, 0.01f, 1000f);
            Matrix projMatrix = Matrix.PerspectiveFovLH((float)Math.PI / 3f, Width / Height, 0.01f, 1000f);
            var viewProjMatrix = Matrix.Multiply(viewMatrix, projMatrix);
            var worldViewProjMatrix = worldMatrix * viewProjMatrix;
            //worldViewProjMatrix.Transpose();

            // create the constant buffer
            constants.outputResolution = new Vector2(Width, Height);
            constants.worldViewProjMatrix = worldViewProjMatrix;
            constantBuffer = Helper.CreateConstantBuffer(device, constants);
        }

        public void Run()
        {
            RenderLoop.Run(renderForm, RenderCallback);
        }

        private void RenderCallback()
        {
            // exit when the escape key is pressed
            keyboard.Poll();
            var keyboardBufferData = keyboard.GetBufferedData();
            if (keyboardBufferData.Any(x => x.Key == Key.Escape))
            {
                renderForm.Close();
            }

            // update the framerate counter
            var oldTime = time;
            time = stopwatch.Elapsed.TotalSeconds;
            var frameTime = (time - oldTime);
            renderForm.Text = string.Format("GPU Rasterizer - {0}", (1.0f / frameTime).ToString("F1"));

            context.OutputMerger.SetRenderTargets(renderTargetView);
            context.ClearRenderTargetView(renderTargetView, clearColor);

            Helper.UpdateConstantBuffer(device, constantBuffer, constants);

            DispatchComputeShader();

            swapChain.Present(0, PresentFlags.None);
        }

        void DispatchComputeShader()
        {
            device.ImmediateContext.ComputeShader.Set(computeShader);
            device.ImmediateContext.ComputeShader.SetConstantBuffer(0, constantBuffer);
            //device.ImmediateContext.ComputeShader.SetUnorderedAccessView(0, vertexBufferUAV);
            //device.ImmediateContext.ComputeShader.SetUnorderedAccessView(1, indexBufferUAV);
            device.ImmediateContext.ComputeShader.SetShaderResource(0, vertexBufferView);
            device.ImmediateContext.ComputeShader.SetShaderResource(1, indexBufferView);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(2, backbufferUAV);
            device.ImmediateContext.Dispatch(model.Indices.Length / 3, 1, 1);
        }

        public void Dispose()
        {
            renderForm.Dispose();
        }
    }
}
