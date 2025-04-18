using Silk.NET.Vulkan;

namespace FluidsVulkan.Vulkan;

public class VkSampler : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly Sampler _sampler;

    public VkSampler(VkContext ctx,
        VkDevice device,
        SamplerCreateFlags flags,
        Filter minFilter,
        Filter magFilter,
        SamplerMipmapMode mipmapMode,
        SamplerAddressMode addressModeU = SamplerAddressMode.Repeat,
        SamplerAddressMode addressModeV = SamplerAddressMode.Repeat,
        float mipLodBias = 0,
        SamplerAddressMode addressModeW = SamplerAddressMode.Repeat,
        bool anisotropyEnable = false,
        int maxAnisotropy = 1,
        bool compareEnable = false,
        CompareOp compareOp = CompareOp.Less,
        float minLod = 0.0f,
        float maxLod = 0.0f,
        BorderColor borderColor = BorderColor.IntOpaqueWhite,
        bool unnormalizedCoordinates = false
    )
    {
        _ctx = ctx;
        _device = device;

        unsafe
        {
            var createInfo = new SamplerCreateInfo(
                StructureType.SamplerCreateInfo,
                null,
                flags,
                minFilter: minFilter,
                magFilter: magFilter,
                mipmapMode: mipmapMode,
                addressModeU: addressModeU,
                addressModeV: addressModeV,
                mipLodBias: mipLodBias,
                addressModeW: addressModeW,
                anisotropyEnable: anisotropyEnable,
                maxAnisotropy: maxAnisotropy,
                compareEnable: compareEnable,
                compareOp: compareOp,
                minLod: minLod,
                maxLod: maxLod,
                borderColor: borderColor,
                unnormalizedCoordinates: unnormalizedCoordinates
            );

            if (_ctx.Api.CreateSampler(_device.Device, in createInfo,
                    null, out _sampler) != Result.Success)
                throw new Exception("Failed to create sampler");
        }
    }

    public Sampler Sampler => _sampler;

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    private void ReleaseUnmanagedResources()
    {
        unsafe
        {
            _ctx.Api.DestroySampler(_device.Device, _sampler, null);
        }
    }

    ~VkSampler()
    {
        ReleaseUnmanagedResources();
    }
}