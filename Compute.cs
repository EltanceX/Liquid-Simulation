using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SPH2D_2
{
    public class Compute
    {
        public static IWindow window;
        public static GL gl;
        public static string DensitySource;
        public static string PressureSource;
        public const string DensityPath = "density.csh";
        public const string PressurePath = "pressure.csh";
        public static uint DensityProgram;
        public static uint PressureProgram;
        public static Thread thread;
        public static ManualResetEvent manualResetEvent = new(false);
        public static BlockingCollection<Action> _glTaskQueue = new BlockingCollection<Action>();
        public static ManualResetEvent ComputeTask = new(false);
        public unsafe static void Initialize()
        {
            // 创建窗口
            var options = WindowOptions.Default;
            options.Size = new Silk.NET.Maths.Vector2D<int>(100, 100);
            options.Title = "Compute Shader Example";

            options.WindowState = WindowState.Minimized; // 最小化窗口
            options.WindowBorder = WindowBorder.Hidden; // 隐藏窗口边框
            options.ShouldSwapAutomatically = false; // 不需要自动交换缓冲区
            options.IsVisible = false; // 隐藏窗口
            //options.ShouldSwapAutomatically = false; // 不需要自动交换缓冲区
            //options.
            // 获取显卡信息
            //string vendor = gl.GetStringS(StringName.Vendor);
            //string renderer = gl.GetStringS(StringName.Renderer);

            //Console.WriteLine("GPU Vendor: " + vendor);
            //Console.WriteLine("GPU Renderer: " + renderer);

            window = Window.Create(options);

            window.Load += OnLoadInit;
            window.Run();
        }
        public static uint CompileAndLinkShader(string shaderSource)
        {
            var computeShader = gl.CreateShader(ShaderType.ComputeShader);
            gl.ShaderSource(computeShader, shaderSource);
            gl.CompileShader(computeShader);

            //检查着色器编译是否成功
            gl.GetShader(computeShader, ShaderParameterName.CompileStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine("Shader compilation failed: " + gl.GetShaderInfoLog(computeShader));
                return 0;
            }

            //创建着色器程序
            var program = gl.CreateProgram();
            gl.AttachShader(program, computeShader);
            gl.LinkProgram(program);

            //检查程序链接是否成功
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out status);
            if (status == 0)
            {
                Console.WriteLine("Program linking failed: " + gl.GetProgramInfoLog(program));
                return 0;
            }

            //删除着色器对象（不再需要）
            gl.DeleteShader(computeShader);

            return program;
        }
        public unsafe static float[] ComputeDensity(SPH2D sph)
        {
            uint program = DensityProgram;
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine("Program linking failed: " + gl.GetProgramInfoLog(program));
                return null;
            }
            Vector2[] inputA = sph.particleDataList.PredictedPositions;
            uint bufferA = gl.GenBuffer();
            float[] output = new float[inputA.Length];
            uint bufferOutput = gl.GenBuffer();

            //gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferA);
            //fixed (float* ptr = inputA)
            //{
            //    gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(inputA.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            //}
            //绑定缓冲区上传vec2
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferA);
            fixed (Vector2* ptr = inputA)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(inputA.Length * sizeof(Vector2)), ptr, BufferUsageARB.StaticDraw);
            }
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            fixed (float* ptr = output)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(output.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            //绑定缓冲区到绑定点
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, bufferA);
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, bufferOutput);

            gl.UseProgram(program);//必须在设置uniform之前
            gl.Uniform1(gl.GetUniformLocation(program, "mass"), sph.mass);
            gl.Uniform1(gl.GetUniformLocation(program, "smoothingRadius"), sph.smoothingRadius);



            gl.DispatchCompute((uint)((inputA.Length + 64f) / 64f), 1, 1);

            //确保计算完成
            gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);

            //返回结果
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            //float* resultPtr = (float*)gl.MapBuffer(BufferTargetARB.ShaderStorageBuffer, BufferAccessARB.ReadOnly);
            //for (int i = 0; i < output.Length; i++)
            //{
            //    output[i] = resultPtr[i];
            //}
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            void* resultPtr = gl.MapBuffer(BufferTargetARB.ShaderStorageBuffer, BufferAccessARB.ReadOnly);
            Marshal.Copy(new IntPtr(resultPtr), output, 0, output.Length);


            gl.UnmapBuffer(BufferTargetARB.ShaderStorageBuffer);
            //Console.WriteLine("Result: " + string.Join(", ", output));
            //释放
            gl.DeleteBuffer(bufferA);
            gl.DeleteBuffer(bufferOutput);

            return output;
        }
        public unsafe static Vector2[] ComputePressure(SPH2D sph, float[] densityUpdated)
        {
            uint program = PressureProgram;
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine("Program linking failed: " + gl.GetProgramInfoLog(program));
                return null;
            }


            Vector2[] inputA = sph.particleDataList.PredictedPositions;
            float[] inputB = densityUpdated;
            Vector2[] inputC = sph.particleDataList.Velocities;

            // 创建缓冲区
            uint bufferA = gl.GenBuffer();
            uint bufferB = gl.GenBuffer();
            uint bufferC = gl.GenBuffer();
            Vector2[] output = new Vector2[inputA.Length];
            uint bufferOutput = gl.GenBuffer();

            //绑定缓冲区上传vec2
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferA);
            fixed (Vector2* ptr = inputA)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(inputA.Length * sizeof(Vector2)), ptr, BufferUsageARB.StaticDraw);
            }
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferB);
            fixed (float* ptr = inputB)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(inputB.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferC);
            fixed (Vector2* ptr = inputC)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(inputC.Length * sizeof(Vector2)), ptr, BufferUsageARB.StaticDraw);
            }


            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            fixed (Vector2* ptr = output)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(output.Length * sizeof(Vector2)), ptr, BufferUsageARB.StaticDraw);
            }

            //绑定缓冲区到绑定点
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, bufferA);
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, bufferB);
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, bufferC);
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 3, bufferOutput);

            gl.UseProgram(program);//必须在设置uniform之前
            gl.Uniform1(gl.GetUniformLocation(program, "mass"), sph.mass);
            gl.Uniform1(gl.GetUniformLocation(program, "smoothingRadius"), sph.smoothingRadius);
            gl.Uniform1(gl.GetUniformLocation(program, "targetDensity"), sph.targetDensity);
            gl.Uniform1(gl.GetUniformLocation(program, "pressureMultiplier"), sph.pressureMultiplier);



            gl.DispatchCompute((uint)((inputA.Length + 64f) / 64f), 1, 1);

            //确保计算完成
            gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);

            //返回结果
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            //float* resultPtr = (float*)gl.MapBuffer(BufferTargetARB.ShaderStorageBuffer, BufferAccessARB.ReadOnly);
            //for (int i = 0; i < output.Length; i++)
            //{
            //    output[i] = resultPtr[i];
            //}
            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            void* resultPtr = gl.MapBuffer(BufferTargetARB.ShaderStorageBuffer, BufferAccessARB.ReadOnly);
            int byteSize = output.Length * sizeof(Vector2);
            //Marshal.Copy(new IntPtr(resultPtr), output, 0, byteSize);
            System.Buffer.MemoryCopy(resultPtr, Unsafe.AsPointer(ref output[0]), byteSize, byteSize);


            gl.UnmapBuffer(BufferTargetARB.ShaderStorageBuffer);
            //Console.WriteLine("Result: " + string.Join(", ", output));
            //释放
            gl.DeleteBuffer(bufferA);
            gl.DeleteBuffer(bufferB);
            gl.DeleteBuffer(bufferC);
            gl.DeleteBuffer(bufferOutput);

            return output;
        }
        public struct ComputeData
        {
            public float[] Densities;
            public Vector2[] PressureForces;
            public ComputeData(float[] densities, Vector2[] pressureForces)
            {
                Densities = densities;
                PressureForces = pressureForces;
            }
        }
        public unsafe static ComputeData ComputeParticleData(SPH2D sph)
        {
            float[] DensityRes = sph.particleDataList.Densities;
            Vector2[] PressureForces = sph.particleDataList.Velocities;
            ComputeTask.Reset();
            ExecuteInGLThread(() =>
            {
                Stopwatch sw = Stopwatch.StartNew();

                DensityRes = ComputeDensity(sph);
                PressureForces = ComputePressure(sph, DensityRes);

                sw.Stop();
                Console.WriteLine($"GPU Cost: {sw.Elapsed.TotalMilliseconds}");


                ComputeTask.Set();
            });
            ComputeTask.WaitOne(1000);
            return new ComputeData(DensityRes,PressureForces);
        }
        public static void ExecuteInGLThread(Action action)
        {
            // 将任务加入队列
            _glTaskQueue.Add(action);
        }
        public static unsafe void OnLoadInit()
        {
            gl = GL.GetApi(window);

            DensitySource = File.ReadAllText(DensityPath);
            DensityProgram = CompileAndLinkShader(DensitySource);

            PressureSource = File.ReadAllText(PressurePath);
            PressureProgram = CompileAndLinkShader(PressureSource);


            manualResetEvent.Set();
            //manualResetEvent.
            //Console.WriteLine("oook");


            while (window.IsClosing == false)
            {
                if (_glTaskQueue.TryTake(out var task/*, TimeSpan.FromMilliseconds(1)*/)) // 超时时间1ms
                {
                    task.Invoke();
                }

                //Thread.Sleep(1); // 1000 FPS
            }
            return;
            //var computeShaderSource = "...";

            //var computeShader = gl.CreateShader(ShaderType.ComputeShader);
            //gl.ShaderSource(computeShader, computeShaderSource);
            //gl.CompileShader(computeShader);

            //gl.GetShader(computeShader, ShaderParameterName.CompileStatus, out var status);
            //if (status == 0)
            //{
            //    Console.WriteLine("Shader compilation failed: " + gl.GetShaderInfoLog(computeShader));
            //    return;
            //}

            //var program = gl.CreateProgram();
            //gl.AttachShader(program, computeShader);
            //gl.LinkProgram(program);

            uint program = DensityProgram;
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine("Program linking failed: " + gl.GetProgramInfoLog(program));
                return;
            }

            float[] inputA = { 1.0f, 2.0f, 3.0f, 4.0f };
            float[] inputB = { 5.0f, 6.0f, 7.0f, 8.0f };
            float[] output = new float[inputA.Length];

            uint bufferA = gl.GenBuffer();
            uint bufferB = gl.GenBuffer();
            uint bufferOutput = gl.GenBuffer();

            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferA);
            fixed (float* ptr = inputA)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(inputA.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferB);
            fixed (float* ptr = inputB)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(inputB.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            fixed (float* ptr = output)
            {
                gl.BufferData(BufferTargetARB.ShaderStorageBuffer, (nuint)(output.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, bufferA);
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, bufferB);
            gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, bufferOutput);

            gl.UseProgram(program);

            gl.DispatchCompute((uint)inputA.Length, 1, 1);

            gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);

            gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, bufferOutput);
            float* resultPtr = (float*)gl.MapBuffer(BufferTargetARB.ShaderStorageBuffer, BufferAccessARB.ReadOnly);
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = resultPtr[i];
            }
            gl.UnmapBuffer(BufferTargetARB.ShaderStorageBuffer);

            Console.WriteLine("Result: " + string.Join(", ", output));

            gl.DeleteBuffer(bufferA);
            gl.DeleteBuffer(bufferB);
            gl.DeleteBuffer(bufferOutput);
            gl.DeleteProgram(program);
            //gl.DeleteShader(computeShader);
        }
        public static void Close()
        {
            window.Close();
        }
    }
}
