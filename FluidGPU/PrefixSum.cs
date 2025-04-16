using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace FluidsVulkan.FluidGPU;

internal sealed class PrefixSumGPU(VkContext ctx,
    VkDevice device
)
    : IDisposable
{
    private readonly ComputeShader<uint> _computeShader = new(ctx, device,
        "shader_objects/prefixSum.comp.spv");

    public void RecordBuffer(VkImageView source,
        VkImageView destination,
        (int, int) textureSize)
    {
        _computeShader.SetImageStorage(0, source,
            AccessFlags.ShaderReadBit);
        _computeShader.SetImageStorage(1, destination,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);

        for (uint offset = 0;
             offset < textureSize.Item1 * textureSize.Item2;
             offset += 1024)
        {
            _computeShader.SetPushConstant(offset);
            _computeShader.Dispatch(1, 1, 1);
        }
    }

    public void Dispose()
    {
        _computeShader.Dispose();
    }
}