using System.Numerics;
using System.Runtime.InteropServices;
using Autofac.Features.AttributeFilters;
using FluidsVulkan.Builders;
using FluidsVulkan.VkAllocatorSystem;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace FluidsVulkan.ImGui;

using ImGui = ImGuiNET.ImGui;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ImGuiVertex : IVertexData<ImGuiVertex>
{
    [VertexInputDescription(0, Format.R32G32Sfloat)]
    public Vector2D<float> position;

    [VertexInputDescription(1, Format.R32G32Sfloat)]
    public Vector2D<float> uv;

    [VertexInputDescription(2, Format.R8G8B8A8Unorm)]
    public uint color;
}

public struct ImGuiPushConstant
{
    public Vector2D<float> Scale;
    public Vector2D<float> Translate;
}

public class ImGuiController : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly VkAllocator _allocator;
    private readonly VkAllocator _stagingAllocator;
    private VkTexture _fontTexture;
    private VkImageView _fontImageView;
    private VkCommandPool _commandPool;
    private VkCommandBuffer _cmdBuffer;
    private VkSampler _fontSampler;
    private readonly VkRenderPass _renderPass;
    private readonly VkFrameBuffer _framebuffer;
    private readonly VkDescriptorPool _descriptorPool;
    private readonly DescriptorSet _descriprotSet;
    private readonly VkBuffer<ImGuiVertex> _vertexBuffer;
    private readonly VkMappedMemory<ImGuiVertex> _vertexMapped;
    private readonly VkBuffer<ushort> _indexBuffer;
    private readonly VkMappedMemory<ushort> _indexBufferMapped;
    private readonly VkGraphicsPipeline _pipeline;
    private Dictionary<IEditorComponent, bool> _tabs;
    private IntPtr _ImGuiCtx = ImGui.CreateContext();
    private IWindow _window;
    private IEditorComponent[] _components;
    private GameWindow _gameWindow;

    public ImGuiController(VkContext ctx,
        VkDevice device,
        [MetadataFilter("Type", "DeviceLocal")]
        VkAllocator allocator,
        [MetadataFilter("Type", "HostVisible")]
        VkAllocator stagingAllocator,
        GameWindow gameWindow,
        EventHandler eventHandler,
        IWindow window,
        IEditorComponent[] components)
    {
        IInputContext inputContext = eventHandler.InputContext;
        ImGui.StyleColorsClassic();
        _components = components;
        _tabs = new Dictionary<IEditorComponent, bool>();
        foreach (var component in components)
        {
            _tabs[component] = false;
        }

        _ctx = ctx;
        _device = device;
        _window = window;
        _gameWindow = gameWindow;
        var renderTarget = gameWindow.RenderTarget;
        var extent = gameWindow.WindowSize;
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_gameWindow.WindowSize.Width,
            _gameWindow.WindowSize.Height);
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
        io.FontGlobalScale =
            _window.FramebufferSize.X / (float)_window.Size.X;
        inputContext.Mice[0].MouseMove += (m, x) =>
        {
            var newX = x.X / _window.Size.X *
                       _gameWindow.WindowSize.Width;
            var newY = x.Y / _window.Size.Y *
                       _gameWindow.WindowSize.Height;
            ImGui.GetIO()
                .AddMousePosEvent(newX, newY);
        };

        inputContext.Mice[0].MouseDown += (m, x) =>
        {
            ImGui.GetIO()
                .AddMouseButtonEvent((int)x,
                    true);
        };

        inputContext.Mice[0].MouseUp += (m, x) =>
        {
            ImGui.GetIO()
                .AddMouseButtonEvent((int)x,
                    false);
        };

        gameWindow.OnRender += RecordImGuiRender;
        gameWindow.OnUpdate += Update;
        _allocator = allocator;
        _stagingAllocator = stagingAllocator;

        _commandPool = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.FamilyIndex);
        _cmdBuffer =
            _commandPool.AllocateBuffers(CommandBufferLevel.Primary,
                1)[0];

        UploadFonts();

        _fontImageView = new VkImageView(_ctx, _device,
            _fontTexture.Image, new ComponentMapping(
                ComponentSwizzle.Identity,
                ComponentSwizzle.Identity,
                ComponentSwizzle.Identity,
                ComponentSwizzle.Identity),
            new ImageSubresourceRange(ImageAspectFlags.ColorBit,
                0, 1, 0, 1));

        _fontSampler = new VkSampler(_ctx, _device,
            SamplerCreateFlags.None, Filter.Linear, Filter.Linear,
            SamplerMipmapMode.Nearest);


        var imageInfo = new DescriptorImageInfo(_fontSampler.Sampler,
            _fontImageView.ImageView,
            ImageLayout.General);

        using var vertModule = new VkShaderModule(ctx, device,
            "shader_objects/imgui.vert.spv");
        using var fragModule = new VkShaderModule(ctx, device,
            "shader_objects/imgui.frag.spv");


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
            LoadOp = AttachmentLoadOp.Load,
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
                extent.Width,
                extent.Height, 1, [renderTarget]);

        var pushConstantRange = new PushConstantRange(
            ShaderStageFlags.VertexBit, 0,
            (uint)Marshal.SizeOf<ImGuiPushConstant>());


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

        DescriptorSetLayoutBinding fontAtlasBinding =
            new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
                PImmutableSamplers = null
            };

        using var layout =
            new VkSetLayout(_ctx, _device, [fontAtlasBinding]);

        _descriptorPool = new VkDescriptorPool(_ctx, _device, [
            new DescriptorPoolSize()
            {
                DescriptorCount = 1,
                Type = DescriptorType.CombinedImageSampler
            }
        ], 1);
        _descriprotSet =
            _descriptorPool.AllocateDescriptors(layout, 1)[0];
        new VkDescriptorSetUpdater(_ctx, _device)
            .AppendWrite(_descriprotSet, 0,
                DescriptorType.CombinedImageSampler,
                [imageInfo]).Update();

        ImGui.GetIO().Fonts
            .SetTexID((IntPtr)_descriprotSet.Handle);

        _pipeline = new GraphicsPipelineBuilder()
            .ForRenderPass(_renderPass)
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
                .AddBindingFor<ImGuiVertex>(0,
                    VertexInputRate.Vertex))
            .WithInputAssembly(PrimitiveTopology.TriangleList)
            .WithViewportAndScissor(viewport, scissor)
            .WithPipelineStages(z =>
                z.Vertex(new VkShaderInfo(vertModule, "main"))
                    .Fragment(
                        new VkShaderInfo(fragModule, "main")))
            .WithLayouts([layout], [pushConstantRange])
            .Build(ctx, device, 0);

        _vertexBuffer = new VkBuffer<ImGuiVertex>(128 * 1024,
            BufferUsageFlags.VertexBufferBit, SharingMode.Exclusive,
            _stagingAllocator);
        _indexBuffer = new VkBuffer<ushort>(128 * 1024,
            BufferUsageFlags.IndexBufferBit, SharingMode.Exclusive,
            _stagingAllocator);

        _vertexMapped = _vertexBuffer.Map(0, 128 * 1024);
        _indexBufferMapped = _indexBuffer.Map(0, 128 * 1024);
    }

    bool _fistRun = true;

    public void Update(double a, double b)
    {
        ImGui.GetIO().DeltaTime = (float)a;
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_gameWindow.WindowSize.Width,
            _gameWindow.WindowSize.Height);

        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;


        ImGui.NewFrame();


        //ImGui.SetNextWindowDockID();
        float startPos = 0;
        foreach (var component in _components)
        {
            ImGui.SetNextWindowCollapsed(true, ImGuiCond.Once);
            ImGui.SetNextWindowSize(
                io.DisplaySize with
                {
                    X = io.DisplaySize.X /
                        Math.Max(3, _components.Length)
                }, ImGuiCond.Once);
            ImGui.SetNextWindowPos(new Vector2(startPos, 0));
            ImGui.Begin(
                $"{component.Name}###{component.Guid}",
                ImGuiWindowFlags.NoMove);
            component.UpdateGui();
            startPos += ImGui.GetWindowSize().X;
            ImGui.End();
        }


        ImGui.EndFrame();
    }

    public void RecordImGuiRender(VkCommandRecordingScope recording,
        Rect2D renderArea)
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        int fbWidth = (int)(drawData.DisplaySize.X *
                            drawData.FramebufferScale.X);
        int fbHeight = (int)(drawData.DisplaySize.Y *
                             drawData.FramebufferScale.Y);

        unsafe
        {
            int offset = 0;
            for (var i = 0; i < drawData.CmdListsCount; i++)
            {
                var cmdList = drawData.CmdLists[i];
                var verticiesLength = cmdList.VtxBuffer.Size;
                var verticies =
                    (ImGuiVertex*)cmdList.VtxBuffer.Data.ToPointer();

                for (var k = 0; k < verticiesLength; k++)
                {
                    _vertexMapped[offset + k] = verticies[k];
                }

                offset += verticiesLength;
            }

            offset = 0;
            for (var i = 0; i < drawData.CmdListsCount; i++)
            {
                var cmdList = drawData.CmdLists[i];
                var indiciesLength = cmdList.IdxBuffer.Size;
                var indicies =
                    (ushort*)cmdList.IdxBuffer.Data.ToPointer();

                for (var k = 0; k < indiciesLength; k++)
                {
                    _indexBufferMapped[offset + k] = indicies[k];
                }

                offset += indiciesLength;
            }
        }

        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = fbWidth,
            Height = fbHeight,
            MinDepth = 0.0f,
            MaxDepth = 1.0f
        };
        using (var pass =
               recording.BeginRenderPass(_renderPass, _framebuffer,
                   renderArea))
        {
            recording.BindPipeline(_pipeline);
            pass.SetViewport(ref viewport);
            recording.BindVertexBuffers(0, [_vertexBuffer], [0]);
            recording.BindIndexBuffer(_indexBuffer, 0,
                IndexType.Uint16);
            var pushConstant = new ImGuiPushConstant()
            {
                Scale = new Vector2D<float>(
                    2.0f / drawData.DisplaySize.X,
                    2.0f / drawData.DisplaySize.Y),
                Translate =
                    new Vector2D<float>(-1.0f -
                                        drawData.DisplayPos.X * 2.0f /
                                        drawData.DisplaySize.Y,
                        -1.0f - drawData.DisplayPos.Y * 2.0f /
                        drawData.DisplaySize.X)
            };
            recording.SetPushConstant(
                _pipeline,
                ShaderStageFlags.VertexBit, ref pushConstant);
            var vtxOffset = 0;
            var idxOffset = 0;
            for (var i = 0; i < drawData.CmdListsCount; i++)
            {
                var cmdList = drawData.CmdLists[i];

                for (var j = 0; j < cmdList.CmdBuffer.Size; j++)
                {
                    var cmdBuffer = cmdList.CmdBuffer[j];
                    Rect2D scissor = new()
                    {
                        Offset = new Offset2D
                        {
                            X = (int)(cmdBuffer.ClipRect.X *
                                      drawData.FramebufferScale.X),
                            Y = (int)(cmdBuffer.ClipRect.Y *
                                      drawData.FramebufferScale.X)
                        },
                        Extent = new Extent2D
                        {
                            Width = (uint)(cmdBuffer.ClipRect.Z -
                                           cmdBuffer.ClipRect.X) *
                                    (uint)drawData.FramebufferScale.X,
                            Height = (uint)(cmdBuffer.ClipRect.W -
                                            cmdBuffer.ClipRect.Y) *
                                     (uint)drawData.FramebufferScale.X
                        }
                    };


                    pass.SetScissor(ref scissor);
                    var desc =
                        new DescriptorSet((ulong)cmdBuffer.TextureId);
                    recording.BindDescriptorSets(
                        PipelineBindPoint.Graphics,
                        _pipeline.PipelineLayout,
                        [desc]);

                    pass.DrawIndexed(cmdBuffer.ElemCount, 1,
                        (uint)idxOffset + cmdBuffer.IdxOffset, 0,
                        (uint)vtxOffset + cmdBuffer.VtxOffset);
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }
        }
    }

    private unsafe void UploadFonts()
    {
        ImGui.GetIO().Fonts.AddFontFromFileTTF("ImGui/JetBrainsMonoNerdFont-Regular.ttf",  18.0f,
            null, ImGui.GetIO().Fonts.GetGlyphRangesCyrillic());
        ImGui.GetIO().Fonts.Build();
        ImGui.GetIO().Fonts
            .GetTexDataAsRGBA32(out byte* pFonts, out var width,
                out var height);
        ulong uploadSize = (ulong)(width * height * 4);

        using var stagingBuffer = new VkBuffer<byte>(uploadSize,
            BufferUsageFlags.TransferSrcBit,
            SharingMode.Exclusive, _stagingAllocator);

        _fontTexture = new VkTexture(ImageType.Type2D,
            new Extent3D((uint)width, (uint)height, 1), 1, 1,
            Format.R8G8B8A8Unorm, ImageTiling.Linear,
            ImageLayout.Undefined,
            ImageUsageFlags.SampledBit |
            ImageUsageFlags.TransferDstBit,
            SampleCountFlags.Count1Bit, SharingMode.Exclusive,
            _allocator);

        var copyBuffer =
            _commandPool.AllocateBuffers(CommandBufferLevel.Primary,
                1)[0];

        using (var map = stagingBuffer.Map(0, uploadSize))
        {
            for (ulong i = 0; i < uploadSize; i++)
            {
                map[i] = pFonts[i];
            }
        }

        using (var recording =
               copyBuffer.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit, DependencyFlags.None,
                imageMemoryBarriers:
                [
                    new ImageMemoryBarrier()
                    {
                        SType = StructureType.ImageMemoryBarrier,
                        DstAccessMask = AccessFlags.TransferWriteBit,
                        SrcAccessMask = AccessFlags.None,
                        Image = _fontTexture.Image.Image,
                        NewLayout = ImageLayout.TransferDstOptimal,
                        OldLayout = ImageLayout.Undefined,
                        SubresourceRange =
                            new ImageSubresourceRange(
                                ImageAspectFlags.ColorBit, 0, 1, 0, 1)
                    }
                ]);
            var region = new BufferImageCopy()
            {
                BufferImageHeight = 0,
                BufferRowLength = 0,
                BufferOffset = 0,
                ImageExtent =
                    new Extent3D((uint)width, (uint)height, 1),
                ImageOffset = new Offset3D(0, 0, 0),
                ImageSubresource = new ImageSubresourceLayers()
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                    MipLevel = 0
                },
            };
            _ctx.Api.CmdCopyBufferToImage(copyBuffer.Buffer,
                stagingBuffer.Buffer, _fontTexture.Image.Image,
                ImageLayout.TransferDstOptimal,
                1, in region);
            recording.PipelineBarrier(PipelineStageFlags.TransferBit,
                PipelineStageFlags.BottomOfPipeBit,
                DependencyFlags.None,
                imageMemoryBarriers:
                [
                    new ImageMemoryBarrier()
                    {
                        SType = StructureType.ImageMemoryBarrier,
                        DstAccessMask = AccessFlags.None,
                        SrcAccessMask = AccessFlags.TransferWriteBit,
                        Image = _fontTexture.Image.Image,
                        NewLayout = ImageLayout.General,
                        OldLayout = ImageLayout.TransferDstOptimal,
                        SubresourceRange =
                            new ImageSubresourceRange(
                                ImageAspectFlags.ColorBit, 0, 1, 0, 1)
                    }
                ]);
        }

        copyBuffer.Submit(_device.TransferQueue, VkFence.NullHandle,
            [], []);

        _ctx.Api.QueueWaitIdle(_device.TransferQueue);
    }

    public void Dispose()
    {
        _fontSampler.Dispose();
        _fontTexture.Dispose();
        _fontImageView.Dispose();
        _commandPool.Dispose();
        _renderPass.Dispose();
        _framebuffer.Dispose();
        _descriptorPool.Dispose();
        _vertexMapped.Dispose();
        _vertexBuffer.Dispose();
        _indexBufferMapped.Dispose();
        _indexBuffer.Dispose();
        _pipeline.Dispose();
    }
}