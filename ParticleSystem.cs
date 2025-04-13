using FluidsVulkan.FluidGPU;

namespace FluidsVulkan;

public interface IParticleSystem : IDisposable
{
    VkBuffer<Fluid> Buffer { get; }
    
    Task Update(double delta, double totalTime);
}