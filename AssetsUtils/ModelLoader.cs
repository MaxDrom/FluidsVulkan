using FluidsVulkan.RenderGraph;
using FluidsVulkan.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace FluidsVulkan.AssetsUtils;

using glTFLoader;
using glTFLoader.Schema;

public static class ModelLoader
{
    public static List<Mesh<MeshVertex>> LoadMesh(string path)
    {
        var model = Interface.LoadModel(path);
        var allBuffers = model.Buffers
            .Select(b =>
                File.ReadAllBytes(Path.Combine(
                    Path.GetDirectoryName(path)!, b.Uri ?? "")))
            .ToArray();

        var result = new List<Mesh<MeshVertex>>();
        foreach (var mesh in model.Meshes)
        {
            foreach (var prim in mesh.Primitives)
            {
                var acc = model.Accessors[prim.Indices!.Value];
                var bv = model.BufferViews[acc.BufferView!.Value];
                var buf = allBuffers[bv.Buffer];
                Console.WriteLine(acc.ComponentType);
                var indices = new uint[acc.Count];
                System.Buffer.BlockCopy(buf, bv.ByteOffset, indices,
                    0, acc.Count * sizeof(uint));
                var vertices = LoadVertices(model, prim, allBuffers);


                var meshData = new Mesh<MeshVertex>()
                {
                    Id = mesh.Name + "##" + Guid.NewGuid(),
                    Indices = indices,
                    PrimitiveTopology =
                        ConvertModeToPrimitiveTopology(prim.Mode),
                    Vertices = vertices
                };
                result.Add(meshData);
            }
        }

        return result;
    }

    private static PrimitiveTopology ConvertModeToPrimitiveTopology(
        MeshPrimitive.ModeEnum mode)
    {
        return mode switch
        {
            MeshPrimitive.ModeEnum.POINTS => PrimitiveTopology
                .PointList,
            MeshPrimitive.ModeEnum.LINES =>
                PrimitiveTopology.LineList,
            MeshPrimitive.ModeEnum.LINE_LOOP => PrimitiveTopology
                .LineStrip,
            MeshPrimitive.ModeEnum.LINE_STRIP => PrimitiveTopology
                .LineStrip,
            MeshPrimitive.ModeEnum.TRIANGLES => PrimitiveTopology
                .TriangleList,
            MeshPrimitive.ModeEnum.TRIANGLE_STRIP => PrimitiveTopology
                .TriangleStrip,
            MeshPrimitive.ModeEnum.TRIANGLE_FAN => PrimitiveTopology
                .TriangleFan,
            _ => PrimitiveTopology.TriangleList
        };
    }

    private static MeshVertex[] LoadVertices(Gltf model,
        MeshPrimitive prim,
        byte[][] allBuffers)
    {
        var positions = LoadAccessorAsFloatArray(model, allBuffers,
            prim.Attributes["POSITION"]);

        var posLength =
            NumComponents(model.Accessors[prim.Attributes["POSITION"]]
                .Type);
        var uv = LoadAccessorAsFloatArray(model, allBuffers,
            prim.Attributes["TEXCOORD_0"]);
        var vertexLength = positions.Length / 3;
        var vertices = new MeshVertex[vertexLength];

        for (var i = 0; i < vertexLength; i++)
        {
            vertices[i] = new MeshVertex()
            {
                Position = new Vector4D<float>(
                    positions[posLength * i],
                    positions[posLength * i + 1],
                    posLength > 2 ? positions[posLength * i + 2] : 0.0f,
                    posLength > 3
                        ? positions[posLength * i + 3]
                        : 1.0f),
                UV = new Vector2D<float>(uv[2 * i], uv[2 * i + 1]),
            };
        }

        return vertices;
    }

    private static float[] LoadAccessorAsFloatArray(Gltf model,
        byte[][] buffers,
        int accessorIndex)
    {
        var acc = model.Accessors[accessorIndex];
        var bv = model.BufferViews[acc.BufferView!.Value];
        var buf = buffers[bv.Buffer];
        int count = acc.Count;

        int numComponents = NumComponents(acc.Type);

        var result = new float[count * numComponents];
        int stride = bv.ByteStride ??
                     (numComponents *
                      SizeOfComponent(acc.ComponentType));
        int baseOffset = bv.ByteOffset + acc.ByteOffset;

        for (int i = 0; i < count; i++)
        {
            int offset = baseOffset + i * stride;
            for (int c = 0; c < numComponents; c++)
            {
                int componentOffset = offset +
                                      c * SizeOfComponent(
                                          acc.ComponentType);
                float val = ReadComponentAsFloat(buf, componentOffset,
                    acc.ComponentType, acc.Normalized);
                result[i * numComponents + c] = val;
            }
        }

        return result;
    }

    private static int NumComponents(Accessor.TypeEnum type) =>
        type switch
        {
            Accessor.TypeEnum.SCALAR => 1,
            Accessor.TypeEnum.VEC2 => 2,
            Accessor.TypeEnum.VEC3 => 3,
            Accessor.TypeEnum.VEC4 => 4,
            _ => throw new Exception(
                $"Unknown accessor type {type}")
        };


    private static int SizeOfComponent(
        Accessor.ComponentTypeEnum componentType) =>
        componentType switch
        {
            Accessor.ComponentTypeEnum.BYTE => 1,
            Accessor.ComponentTypeEnum.UNSIGNED_BYTE => 1,
            Accessor.ComponentTypeEnum.SHORT => 2,
            Accessor.ComponentTypeEnum.UNSIGNED_SHORT => 2,
            Accessor.ComponentTypeEnum.UNSIGNED_INT => 4,
            Accessor.ComponentTypeEnum.FLOAT => 4,
            _ => throw new Exception(
                $"Unknown componentType {componentType}")
        };

    private static float ReadComponentAsFloat(byte[] buf,
        int offset,
        Accessor.ComponentTypeEnum componentType,
        bool normalized)
    {
        return componentType switch
        {
            Accessor.ComponentTypeEnum.FLOAT => BitConverter.ToSingle(
                buf,
                offset),
            Accessor.ComponentTypeEnum.UNSIGNED_BYTE => normalized
                ? buf[offset] / 255f
                : buf[offset],
            Accessor.ComponentTypeEnum.BYTE => normalized
                ? Math.Max((sbyte)buf[offset] / 127f, -1f)
                : (sbyte)buf[offset],
            Accessor.ComponentTypeEnum.UNSIGNED_SHORT => normalized
                ? BitConverter.ToUInt16(buf, offset) / 65535f
                : BitConverter.ToUInt16(buf, offset),
            Accessor.ComponentTypeEnum.SHORT => normalized
                ? Math.Max(BitConverter.ToInt16(buf, offset) / 32767f,
                    -1f)
                : BitConverter.ToInt16(buf, offset),
            Accessor.ComponentTypeEnum.UNSIGNED_INT => BitConverter
                .ToUInt32(buf, offset),
            _ => throw new Exception(
                $"Unknown componentType {componentType}")
        };
    }
}