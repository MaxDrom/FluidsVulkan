using System.Runtime.InteropServices;
using FluidsVulkan.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace FluidsVulkan.FluidGPU;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Fluid : IVertexData<Fluid>
{
    [VertexInputDescription(2, Format.R32G32Sfloat)]
    public Vector2D<float> position;

    [VertexInputDescription(3, Format.R32G32Sfloat)]
    public Vector2D<float> velocity;

    [VertexInputDescription(4, Format.R32Sfloat)]
    public float Density;

    [VertexInputDescription(5, Format.R32Sfloat)]
    public float Pressure;
}