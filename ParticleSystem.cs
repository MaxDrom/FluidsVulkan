using FluidsVulkan.FluidGPU;
using FluidsVulkan.ResourceManagement;
using FluidsVulkan.Vulkan;

namespace FluidsVulkan;

public interface IParticleSystem : IDisposable
{
    VersionBufferStorage<Fluid> Buffer { get; }

    Task Update(double delta, double totalTime);
}