using System.Reflection;
using ImGuiNET;

namespace FluidsVulkan.ImGui;

using ImGui = ImGuiNET.ImGui;

public interface IParametrized
{
}

public interface IEditorComponent
{
    public Guid Guid { get; }
    public string Name { get; }
    public void UpdateGui();
}

public class Editor : IEditorComponent
{
    private readonly Dictionary<object, PropertyInfo[]> _properties;

    private readonly Dictionary<PropertyInfo, (IRefLikeProperty,
        ImGuiAttribute
        )> _refs;

    public Editor(IParametrized[] parametrizedEntities)
    {
        _properties = parametrizedEntities.Select(z => ((object)z,
            z.GetType().GetProperties()
                .Where(u =>
                    u.GetCustomAttribute<ImGuiAttribute>() != null)
                .ToArray())).ToDictionary();
        _refs =
            new Dictionary<PropertyInfo, (IRefLikeProperty,
                ImGuiAttribute)>();
        foreach (var (_, properties) in _properties)
        foreach (var prop in properties)
            _refs[prop] = (null,
                prop.GetCustomAttribute<ImGuiAttribute>()!);
    }

    public Guid Guid { get; } = Guid.NewGuid();

    public string Name => "Objects Properties";

    public void UpdateGui()
    {
        var vx = ImGui.GetContentRegionAvail();

        var dy = vx.Y / _properties.Count;
        foreach (var (obj, properties) in _properties)
        {
            ImGui.BeginChild($"###{obj}", vx with { Y = dy },
                ImGuiChildFlags.Borders | ImGuiChildFlags.FrameStyle);
            var name = obj.GetType().Name;
            var wrapL = "###    ";
            var wrapR = "    ###";

            ImGui.Text(wrapL + name + wrapR);
            foreach (var property in properties)
            {
                var (refProp, attr) = _refs[property];
                if (refProp != null && refProp.IsChange(out var val))
                    property.SetValue(obj, val);

                _refs[property] = (
                    attr.ApplyAttribute(property.GetValue(obj)),
                    attr);
                ImGui.Separator();
            }

            ImGui.EndChild();
        }
    }
}