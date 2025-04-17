using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace FluidsVulkan.Vulkan;

public unsafe class VkDevice : IDisposable
{
    private readonly Queue _computeQueue;
    private readonly VkContext _ctx;

    private readonly Device _device;

    private readonly uint? _graphicsFamilyIndex;
    private readonly Queue _graphicsQueue;
    private readonly Queue _presentQueue;

    private readonly Queue _transferQueue;
    private bool _disposedValue;

    public VkDevice(VkContext ctx,
        PhysicalDevice physicalDevice,
        List<string> enabledLayersNames,
        List<string> enabledExtensionsNames)
    {
        _ctx = ctx;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            enabledExtensionsNames.Add("VK_KHR_portability_subset");

        PhysicalDevice = physicalDevice;
        var pEnabledLayersNames =
            (byte**)SilkMarshal.StringArrayToPtr(enabledLayersNames);
        var pEnabledExtensionNames =
            (byte**)SilkMarshal.StringArrayToPtr(
                enabledExtensionsNames);

        uint queueFamilyPropertiesCount = 0;
        _ctx.Api.GetPhysicalDeviceQueueFamilyProperties(
            physicalDevice, ref queueFamilyPropertiesCount, null);
        var queueFamiliesProperties =
            new QueueFamilyProperties [queueFamilyPropertiesCount];
        fixed (QueueFamilyProperties* pQueueFamilies =
                   queueFamiliesProperties)
        {
            _ctx.Api.GetPhysicalDeviceQueueFamilyProperties(
                physicalDevice, ref queueFamilyPropertiesCount,
                pQueueFamilies);
        }


        for (var i = 0u; i < queueFamiliesProperties.Length; i++)
        {
            _ctx.SurfaceApi.GetPhysicalDeviceSurfaceSupport(
                physicalDevice, i, _ctx.Surface, out var result);

            if (result &&
                queueFamiliesProperties[i].QueueFlags
                    .HasFlag(QueueFlags.GraphicsBit |
                             QueueFlags.ComputeBit |
                             QueueFlags.TransferBit))
            {
                _graphicsFamilyIndex = i;
                break;
            }
        }

        if (_graphicsFamilyIndex == null)
            throw new Exception(
                "Failed to find suitable queue family");


        
        var queueCount = Math.Min(3u,
            queueFamiliesProperties[_graphicsFamilyIndex!.Value]
                .QueueCount);
        var defaultProperites = stackalloc float[(int)queueCount];
        for (var i = 0; i < queueCount; i++)
            defaultProperites[i] = 1.0f;
        if (queueCount < 2)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                "⚠️ Warning: async compute не поддерживается на этом устройстве.");
            Console.ResetColor();
        }

        var defaultPropery = 1.0f;
        var queueCreateInfos = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueCount = queueCount,
            QueueFamilyIndex = _graphicsFamilyIndex.Value,
            PQueuePriorities = &defaultPropery,
        };
        


        DeviceCreateInfo deviceCreateInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            EnabledLayerCount =
                (uint)enabledLayersNames.Count,
            PpEnabledLayerNames = pEnabledLayersNames,
            EnabledExtensionCount =
                (uint)enabledExtensionsNames.Count,
            PpEnabledExtensionNames = pEnabledExtensionNames,
            QueueCreateInfoCount =
                1,
            PQueueCreateInfos = &queueCreateInfos,
        };


        if (_ctx.Api.CreateDevice(physicalDevice,
                in deviceCreateInfo, null, out _device) !=
            Result.Success)
            throw new Exception("Could not create device");

        SilkMarshal.Free((nint)pEnabledLayersNames);
        SilkMarshal.Free((nint)pEnabledExtensionNames);

        var index = 0u;
        _ctx.Api.GetDeviceQueue(_device,
            _graphicsFamilyIndex!.Value, index, out _graphicsQueue);
        index = (++index) % queueCount;

        _ctx.Api.GetDeviceQueue(_device,
            _graphicsFamilyIndex!.Value, index, out _computeQueue);
        index = (++index) % queueCount;
        
        _ctx.Api.GetDeviceQueue(_device,
            _graphicsFamilyIndex!.Value, index, out _presentQueue);
        index = (++index) % queueCount;
        
        _ctx.Api.GetDeviceQueue(_device,
            _graphicsFamilyIndex!.Value, index, out _transferQueue);
    }

    internal Device Device => _device;

    internal PhysicalDevice PhysicalDevice { get; private set; }

    public uint FamilyIndex => _graphicsFamilyIndex!.Value;


    public Queue GraphicsQueue => _graphicsQueue;

    public Queue PresentQueue => _presentQueue;

    public Queue ComputeQueue => _computeQueue;

    public Queue TransferQueue => _transferQueue;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _ctx.Api.DestroyDevice(_device, null);
            _disposedValue = true;
        }
    }

    ~VkDevice()
    {
        Dispose(false);
    }
}