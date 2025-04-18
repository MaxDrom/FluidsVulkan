using System.Numerics;
using System.Runtime.InteropServices;
using Autofac.Features.AttributeFilters;
using FluidsVulkan.ComputeScheduling;
using FluidsVulkan.ImGui;
using FluidsVulkan.Vulkan;
using FluidsVulkan.Vulkan.Builders;
using FluidsVulkan.Vulkan.VkAllocatorSystem;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace FluidsVulkan.FluidGPU;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PushConstant
{
    public Vector2D<float> xrange;
    public Vector2D<float> yrange;
    public Vector2D<float> minMax;
    public int visualizationIndex;
}

public sealed class FluidView : IDisposable, IParametrized
{
    private readonly uint[] _indices = [0, 1, 2, 2, 3, 0];

    private readonly Vertex[] _vertices =
    [
        new()
        {
            position = new Vector2D<float>(-1f, -1f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },

        new()
        {
            position = new Vector2D<float>(1f, -1f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },

        new()
        {
            position = new Vector2D<float>(1f, 1f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },

        new()
        {
            position = new Vector2D<float>(-1f, 1f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },
    ];

    private readonly VkRenderPass _renderPass;
    private VkContext _ctx;
    private VkDevice _device;
    private readonly VkGraphicsPipeline _graphicsPipeline;

    private VkBuffer<Fluid> _instanceBuffer;
    private readonly VkBuffer<Vertex> _vertexBuffer;
    private readonly VkBuffer<uint> _indexBuffer;
    private readonly VkCommandPool _commandPoolTransfer;
    private readonly VkCommandBuffer _copyBuffer;
    private VkAllocator _stagingAllocator;
    private VkFrameBuffer _framebuffer;
    private VkFence _copyFence;


    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            if (_scale <= 0)
                _scale = 0.001f;
        }
    }

    private float _scale = 1f;

    public Vector2D<float> BoxCenter
    {
        get => _boxCenter;
        set => _boxCenter = value;
    }

    private Vector2D<float> _boxCenter = new(0.5f, 0.5f);
    private readonly Extent2D _extent;
    private readonly IParticleSystem _fluidEngine;
    private Vector2 _tempMinMax = new(5, 30);
    private double _fixedUpdateInterval = 1 / 60.0;
    private double _timeFromFixedUpdate;

    [SliderFloat2("Colormap min max", -1000,
        1000, "%.1f", ImGuiSliderFlags.Logarithmic)]
    public Vector2 TempMinMax
    {
        get => _tempMinMax;
        set
        {
            _tempMinMax = value;
            if (_tempMinMax.X > _tempMinMax.Y)
                _tempMinMax.X = _tempMinMax.Y;
        }
    }

    [SliderFloat("Simulation Speed", 0f, 5.0f, "%.1f")]
    public float SimulationSpeed { get; set; } = 1f;


    [Combo("Color map visualization target",
        ["Density", "Pressure", "Velocity"])]
    public int VisualisationIndex { get; set; }

    public FluidView(VkContext ctx,
        VkDevice device,
        [MetadataFilter("Type", "DeviceLocal")]
        VkAllocator allocator,
        [MetadataFilter("Type", "HostVisible")]
        VkAllocator stagingAllocator,
        IParticleSystem fluidEngine,
        GameWindow window)
    {
        _fluidEngine = fluidEngine;
        _device = device;
        _stagingAllocator = stagingAllocator;
        _ctx = ctx;
        var renderTarget = window.RenderTarget;
        _extent = window.WindowSize;

        window.OnRender += RecordDraw;
        window.OnUpdateAsync += Update;

        var subPass = new VkSubpassInfo(PipelineBindPoint.Graphics, [
            new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal,
            },
        ]);

        var attachmentDescription = new AttachmentDescription
        {
            Format = renderTarget.Image.Format,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.General,
            FinalLayout = ImageLayout.General,
        };

        var dependency = new SubpassDependency
        {
            SrcSubpass = ~0u,
            DstSubpass = 0u,
            SrcStageMask =
                PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstStageMask = PipelineStageFlags
                .ColorAttachmentOutputBit,
        };

        _renderPass = new VkRenderPass(_ctx, _device, [subPass],
            [dependency], [attachmentDescription]);

        _framebuffer =
            new VkFrameBuffer(_ctx, _device, _renderPass,
                _extent.Width,
                _extent.Height, 1, [renderTarget]);

        _graphicsPipeline = CreateGraphicsPipeline(_ctx, _device,
            _renderPass, new Extent2D(1, 1));

        _commandPoolTransfer = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.FamilyIndex);
        _copyBuffer = _commandPoolTransfer
            .AllocateBuffers(CommandBufferLevel.Primary, 1).First();

        _vertexBuffer = new VkBuffer<Vertex>(_vertices.Length,
            BufferUsageFlags.VertexBufferBit |
            BufferUsageFlags.TransferDstBit, SharingMode.Exclusive,
            allocator);
        _indexBuffer = new VkBuffer<uint>(_indices.Length,
            BufferUsageFlags.IndexBufferBit |
            BufferUsageFlags.TransferDstBit, SharingMode.Exclusive,
            allocator);
        _copyFence = new VkFence(_ctx, _device);

        CopyDataToBuffer(_indices, _indexBuffer);
        CopyDataToBuffer(_vertices, _vertexBuffer);

        _instanceBuffer = new VkBuffer<Fluid>(
            _fluidEngine.Buffer.Size,
            BufferUsageFlags.VertexBufferBit |
            BufferUsageFlags.TransferDstBit,
            SharingMode.Exclusive,
            allocator);
    }

    public void RecordDraw(
        VkCommandRecordingScope recording,
        Rect2D scissor)
    {
        var viewport = new Viewport()
        {
            X = 0,
            Y = 0,
            Width = _extent.Width,
            Height = _extent.Height
        };
        using var renderRecording =
            recording.BeginRenderPass(_renderPass,
                _framebuffer, scissor);
        recording.BindPipeline(_graphicsPipeline);

        var pushConstant = new PushConstant()
        {
            xrange =
                new Vector2D<float>(BoxCenter.X-Scale/2, BoxCenter.X + Scale/2),
            yrange =
                new Vector2D<float>(BoxCenter.Y-Scale/2, BoxCenter.Y + Scale/2),
            minMax =
                new Vector2D<float>(_tempMinMax.X, _tempMinMax.Y),
            visualizationIndex = VisualisationIndex,
        };
        recording.SetPushConstant(_graphicsPipeline,
            ShaderStageFlags.VertexBit, ref pushConstant);
        recording.BindIndexBuffer(_indexBuffer, 0,
            IndexType.Uint32);
        recording.BindVertexBuffers(0,
            [_vertexBuffer, _instanceBuffer], [0, 0]);
        renderRecording.SetViewport(ref viewport);
        renderRecording.SetScissor(ref scissor);
        renderRecording.DrawIndexed((uint)_indices.Length,
            (uint)(_instanceBuffer.Size /
                   (ulong)Marshal.SizeOf<Fluid>()), 0, 0);
    }

    public async Task Update(double frameTime, double totalTime)
    {
        _timeFromFixedUpdate += frameTime;
        for (int step = 0;
             step < 1 && _timeFromFixedUpdate >= _fixedUpdateInterval;
             step++)
        {
            await _fluidEngine.Update(
                _fixedUpdateInterval * SimulationSpeed / 5,
                totalTime);
            _timeFromFixedUpdate -= _fixedUpdateInterval;
        }

        ComputeScheduler.Instance.AddTask(new CopyBufferTask(
            _fluidEngine.Buffer, _instanceBuffer,
            _instanceBuffer.Size));
    }

    private static VkGraphicsPipeline CreateGraphicsPipeline(
        VkContext ctx,
        VkDevice device,
        VkRenderPass renderPass,
        Extent2D extent)
    {
        using var vertModule = new VkShaderModule(ctx, device,
            "shader_objects/base.vert.spv");
        using var fragModule = new VkShaderModule(ctx, device,
            "shader_objects/base.frag.spv");

        var pushConstantRange = new PushConstantRange(
            ShaderStageFlags.VertexBit, 0,
            (uint)Marshal.SizeOf<PushConstant>());


        var viewport = new Viewport
        {
            X = 0.0f,
            Y = 0.0f,
            Width = extent.Width,
            Height = extent.Height,
        };

        Rect2D scissor = new(new Offset2D(0, 0), extent);

        PipelineColorBlendAttachmentState colorBlend = new()
        {
            ColorWriteMask =
                ColorComponentFlags.RBit |
                ColorComponentFlags.GBit |
                ColorComponentFlags.BBit |
                ColorComponentFlags.ABit,
            BlendEnable = true,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.Zero,
            AlphaBlendOp = BlendOp.Add,
            SrcColorBlendFactor = BlendFactor.SrcAlpha,
            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
        };

        var pipeline = new GraphicsPipelineBuilder()
            .ForRenderPass(renderPass)
            .WithDynamicStages([
                DynamicState.Viewport, DynamicState.Scissor,
            ]).WithFixedFunctions(z =>
                z.ColorBlending([colorBlend]).Rasterization(y =>
                        y.WithSettings(PolygonMode.Fill,
                            CullModeFlags.BackBit,
                            FrontFace.Clockwise,
                            1.0f))
                    .Multisampling(SampleCountFlags.Count1Bit))
            .WithVertexInput(z => z
                .AddBindingFor<Vertex>(0, VertexInputRate.Vertex)
                .AddBindingFor<Fluid>(1, VertexInputRate.Instance))
            .WithInputAssembly(PrimitiveTopology.TriangleList)
            .WithViewportAndScissor(viewport, scissor)
            .WithPipelineStages(z =>
                z.Vertex(new VkShaderInfo(vertModule, "main"))
                    .Fragment(new VkShaderInfo(fragModule, "main")))
            .WithLayouts([], [pushConstantRange])
            .Build(ctx, device, 0);
        return pipeline;
    }

    private void CopyDataToBuffer<T>(T[] data, VkBuffer<T> buffer)
        where T : unmanaged
    {
        using var stagingBuffer = new VkBuffer<T>(data.Length,
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _stagingAllocator);
        using (var mapped = stagingBuffer.Map(0, data.Length))
        {
            for (var i = 0; i < data.Length; i++) mapped[i] = data[i];
        }

        using (var recording =
               _copyBuffer.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            recording.CopyBuffer(stagingBuffer, buffer, 0, 0,
                stagingBuffer.Size);
        }

        _copyBuffer.Submit(_device.TransferQueue, VkFence.NullHandle,
            [], []);
        _ctx.Api.QueueWaitIdle(_device.TransferQueue);
    }


    public void Dispose()
    {
        _graphicsPipeline.Dispose();
        _instanceBuffer.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _commandPoolTransfer.Dispose();
        _framebuffer.Dispose();
        _renderPass.Dispose();
        _copyFence.Dispose();
    }
}