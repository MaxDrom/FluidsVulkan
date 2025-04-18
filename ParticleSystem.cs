using FluidsVulkan.FluidGPU;
using FluidsVulkan.Vulkan;

namespace FluidsVulkan;

public interface IParticleSystem : IDisposable
{
    VkBuffer<Fluid> Buffer { get; }

    Task Update(double delta, double totalTime);
}