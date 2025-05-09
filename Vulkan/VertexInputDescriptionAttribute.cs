using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace FluidsVulkan.Vulkan;

public interface IVertexData<TSelf>
    where TSelf : unmanaged, IVertexData<TSelf>
{
    (VertexInputAttributeDescription[], VertexInputBindingDescription)
        CreateVertexInputDescription(int binding,
            VertexInputRate inputRate = VertexInputRate.Vertex)
    {
        var result = new List<VertexInputAttributeDescription>();

        foreach (var (property, attribute) in typeof(TSelf)
                     .GetFields()
                     .Select(z => (z,
                         z.GetCustomAttribute<
                             VertexInputDescriptionAttribute>()))
                     .Where(z => z.Item2 != null))
            result.Add(new VertexInputAttributeDescription
            {
                Binding = (uint)binding,
                Location = (uint)attribute.Location,
                Format = attribute.Format,
                Offset =
                    (uint)Marshal.OffsetOf<TSelf>(property.Name),
            });

        var bind = new VertexInputBindingDescription
        {
            Binding = (uint)binding,
            Stride = (uint)Marshal.SizeOf<TSelf>(),
            InputRate = inputRate,
        };

        return ([.. result], bind);
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class VertexInputDescriptionAttribute(int location,
    Format format
)
    : Attribute
{
    public int Location { get; init; } = location;
    public Format Format { get; init; } = format;
}