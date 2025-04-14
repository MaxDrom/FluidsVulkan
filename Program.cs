using System.Globalization;
using Autofac;
using FluidsVulkan.FluidGPU;
using FluidsVulkan.VkAllocatorSystem;
using Silk.NET.Core.Native;
using Silk.NET.Windowing;
using YamlDotNet.Serialization;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace FluidsVulkan;

public class DisplayFormat
{
    public Format Format { get; set; }
    public ColorSpaceKHR ColorSpace { get; set; }

    public WindowOptions WindowOptions { get; set; }
}


internal class Program
{
    private static async Task Main(string[] args)
    {
        ThreadPool.SetMaxThreads(Environment.ProcessorCount,
            Environment.ProcessorCount);
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        CultureInfo.CurrentCulture.NumberFormat
            .CurrencyDecimalDigits = 28;
        var deserializer = new DeserializerBuilder().Build();

        var windowOptions = WindowOptions.DefaultVulkan;
        var window = Window.Create(windowOptions);
        //
        var numberOfParticles = 256*1024;
        var instances = new Fluid[numberOfParticles];
        var random = new Random();
        for (var i = 0; i < numberOfParticles; i++)
        {
            var phi = random.NextDouble() * Math.PI * 2;
            var r = random.NextDouble()*0.3+0.1;
            var force = 0.001/ (r * r );
            var speed = 0.1;//Math.Sqrt(force * r);
            var position = new Vector2D<float>((float)Math.Cos(phi),
                (float)Math.Sin(phi))*(float)r+new Vector2D<float>(0.5f, 0.5f);
            var velocity = new Vector2D<float>((float)Math.Sin(phi),
                -(float)Math.Cos(phi))*(float)speed;

            instances[i] = new Fluid()
            {
                position = position,
                velocity = velocity,
            };
        }

        var builder = new ContainerBuilder();
        
        InitVulkan(builder, window, windowOptions);
        
        builder.RegisterType<FluidEngineGpu>().As<IParticleSystem>()
            .WithParameter("initialData", instances).SingleInstance();
        builder.RegisterType<EventHandler>().AsSelf();
        
        
        var container = builder.Build();
        var gameWindow = container.Resolve<GameWindow>();
        
        gameWindow.Run();

        
        container.Dispose();
        
    }

    private static void InitVulkan(ContainerBuilder builder,
        IWindow window,
        WindowOptions windowOptions)
    {
        window.Initialize();
        if (window.VkSurface is null)
            throw new Exception(
                "Windowing platform doesn't support Vulkan.");

        string[] extensions;
        unsafe
        {
            var pp =
                window.VkSurface
                    .GetRequiredExtensions(out var count);
            extensions = new string[count];
            SilkMarshal.CopyPtrToStringArray((nint)pp, extensions);
        }
        
        builder.RegisterInstance(window).SingleInstance();
       
        var ctx
            = new VkContext(window, extensions);
        var physicalDevice = ctx.Api
            .GetPhysicalDevices(ctx.Instance).ToArray()[0];
        string deviceName;
        unsafe
        {
            var property =
                ctx.Api.GetPhysicalDeviceProperty(physicalDevice);
            deviceName =
                SilkMarshal.PtrToString((nint)property.DeviceName)!;

            uint nn;
            ctx.SurfaceApi.GetPhysicalDeviceSurfaceFormats(
                physicalDevice, ctx.Surface, &nn, null);
            var formats = new SurfaceFormatKHR[nn];
            fixed (SurfaceFormatKHR* pFormat = formats)
            {
                ctx.SurfaceApi.GetPhysicalDeviceSurfaceFormats(
                    physicalDevice, ctx.Surface, &nn, pFormat);
            }

            var format = formats[0].Format;
            var colorSpace = formats[0].ColorSpace;
            foreach (var formatCap in formats)
                if (formatCap.Format == Format.R16G16B16A16Sfloat)
                {
                    format = formatCap.Format;
                    colorSpace = formatCap.ColorSpace;
                    break;
                }

            builder.RegisterInstance(
                    new DisplayFormat()
                    {
                        Format = format, ColorSpace = colorSpace,
                        WindowOptions = windowOptions
                    })
                .SingleInstance();
        }


        Console.WriteLine(deviceName);

        builder.RegisterInstance(ctx).SingleInstance();
        builder.RegisterType<VkDevice>().AsSelf()
            .WithParameter("physicalDevice", physicalDevice)
            .WithParameter("enabledLayersNames", new List<string>())
            .WithParameter("enabledExtensionsNames",
                new List<string>()
                {
                    KhrSwapchain.ExtensionName
                })
            .SingleInstance();
       

        builder.RegisterType<StupidAllocator>().As<VkAllocator>()
            .WithParameter("requiredProperties",
                MemoryPropertyFlags.None)
            .WithParameter("preferredFlags",
                MemoryHeapFlags.DeviceLocalBit)
            .WithMetadata("Type", "DeviceLocal")
            .SingleInstance();


        builder.RegisterType<StupidAllocator>().As<VkAllocator>()
            .WithParameter("requiredProperties",
                MemoryPropertyFlags.HostVisibleBit |
                MemoryPropertyFlags.HostCoherentBit)
            .WithParameter("preferredFlags",
                MemoryHeapFlags.None)
            .WithMetadata("Type", "HostVisible")
            .SingleInstance();


        builder.RegisterType<GameWindow>().AsSelf().SingleInstance();
    }
}