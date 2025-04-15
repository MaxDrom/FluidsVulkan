using System.Numerics;
using System.Reflection;

namespace FluidsVulkan.ImGui;

using ImGuiNET;
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
    private Dictionary<object, PropertyInfo[]> _properties;

    private Dictionary<PropertyInfo, (IRefLikeProperty, ImGuiAttribute
        )> _refs;

    private Guid _guid = Guid.NewGuid();
    public Guid Guid => _guid;
    public string Name => "Objects Properties";
    public Editor(IParametrized[] parametrizedEntities)
    {
        
        _properties = parametrizedEntities.Select(z => ((object)z,
            z.GetType().GetProperties()
                .Where(u =>
                    u.GetCustomAttribute<ImGuiAttribute>() != null)
                .ToArray())).ToDictionary();
        _refs = new Dictionary<PropertyInfo, (IRefLikeProperty, ImGuiAttribute)>();
        foreach (var (_,properties) in _properties)
        {
            foreach (var prop in properties)
            {
                _refs[prop] = (null, prop.GetCustomAttribute<ImGuiAttribute>()!);
            }
        }
    }

    public void UpdateGui()
    {
        var vx = ImGui.GetContentRegionAvail();
        
        float dy = vx.Y/_properties.Count;
        float y = 0;
        foreach (var (obj, properties) in _properties)
        {
            ImGui.BeginChild($"###{obj}", new(vx.X,dy));
            var name = obj.GetType().Name;
            var wrapL = "###    ";
            var wrapR = "    ###";
            
            ImGui.Text(wrapL+name+wrapR);
            foreach (var property in properties)
            {
                var (refProp, attr) = _refs[property];
                if (refProp != null && refProp.IsChange(out var val))
                    property.SetValue(obj, val);

                _refs[property] = (
                    attr.ApplyAttribute(property.GetValue(obj)),
                    attr);
            }
            ImGui.Separator();
            ImGui.EndChild();
        }
        
    }
}