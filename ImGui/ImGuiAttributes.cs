using ImGuiNET;
//WARNING!!!! AUTOGENERATED CODE!!!!! (=^x.x^=🤮)
namespace FluidsVulkan.ImGui;
using ImGui = ImGuiNET.ImGui;
public interface IRefLikeProperty
{
    bool IsChange(out object val);
}


public class RefLikeProperty<T>(T value) : IRefLikeProperty
    where T : unmanaged, IEquatable<T>
{
    public T Value = value; 
    private T _oldValue = value;

    public bool IsChange(out object value)
    {
        value = _oldValue;
        if (!_oldValue.Equals(Value))
        {
            _oldValue  =Value;
            value = _oldValue;
            return true;
        }

        return false;
    }
}


public abstract class ImGuiAttribute : Attribute
{
    public abstract IRefLikeProperty ApplyAttribute(object value);
}

public class CheckboxAttribute : ImGuiAttribute
{
    private string _label;

    public CheckboxAttribute(string label)
    {
        _label = label;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<bool>(
                (bool)value);
        ImGui.Checkbox(_label, ref result.Value);
        return result;
    }
}

public class CheckboxFlagsAttribute : ImGuiAttribute
{
    private string _label;
    private int _flags_value;

    public CheckboxFlagsAttribute(string label,
        int flags_value)
    {
        _label = label;
        _flags_value = flags_value;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<int>((int)value);
        ImGui.CheckboxFlags(_label, ref result.Value, _flags_value);
        return result;
    }
}

public class ComboAttribute : ImGuiAttribute
{
    private string _label;
    private string[] _items;
    private int _items_count;
    private int _popup_max_height_in_items;

    public ComboAttribute(string label,
        string[] items,
        int popup_max_height_in_items = 3)
    {
        _label = label;
        _items = items;
        _items_count = items.Length;
        _popup_max_height_in_items = popup_max_height_in_items;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<int>((int)value);
        ImGui.Combo(_label, ref result.Value, _items, _items_count,
            _popup_max_height_in_items);
        return result;
    }
}

public class RadioButtonAttribute : ImGuiAttribute
{
    private string _label;
    private int _v_button;

    public RadioButtonAttribute(string label,
        int v_button)
    {
        _label = label;
        _v_button = v_button;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<int>((int)value);
        ImGui.RadioButton(_label, ref result.Value, _v_button);
        return result;
    }
}

public class SliderAngleAttribute : ImGuiAttribute
{
    private string _label;
    private float _v_degrees_min;
    private float _v_degrees_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderAngleAttribute(string label,
        float v_degrees_min,
        float v_degrees_max,
        string format,
        ImGuiSliderFlags flags)
    {
        _label = label;
        _v_degrees_min = v_degrees_min;
        _v_degrees_max = v_degrees_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<float>((float)value);
        ImGui.SliderAngle(_label, ref result.Value, _v_degrees_min,
            _v_degrees_max, _format, _flags);
        return result;
    }
}

public class SliderFloatAttribute : ImGuiAttribute
{
    private string _label;
    private float _v_min;
    private float _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderFloatAttribute(string label,
        float v_min,
        float v_max,
        string format = "%.4f",
        ImGuiSliderFlags flags= ImGuiSliderFlags.None)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<float>((float)value);
        ImGui.SliderFloat(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}

public class SliderFloat2Attribute : ImGuiAttribute
{
    private string _label;
    private float _v_min;
    private float _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderFloat2Attribute(string label,
        float v_min,
        float v_max,
        string format,
        ImGuiSliderFlags flags)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<System.Numerics.Vector2>(
                (System.Numerics.Vector2)value);
        ImGui.SliderFloat2(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}

public class SliderFloat3Attribute : ImGuiAttribute
{
    private string _label;
    private float _v_min;
    private float _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderFloat3Attribute(string label,
        float v_min,
        float v_max,
        string format = "%f",
        ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<System.Numerics.Vector3>(
                (System.Numerics.Vector3)value);
        ImGui.SliderFloat3(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}

public class SliderFloat4Attribute : ImGuiAttribute
{
    private string _label;
    private float _v_min;
    private float _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderFloat4Attribute(string label,
        float v_min,
        float v_max,
        string format,
        ImGuiSliderFlags flags)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<System.Numerics.Vector4>(
                (System.Numerics.Vector4)value);
        ImGui.SliderFloat4(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}

public class SliderIntAttribute : ImGuiAttribute
{
    private string _label;
    private int _v_min;
    private int _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderIntAttribute(string label,
        int v_min,
        int v_max,
        string format,
        ImGuiSliderFlags flags)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<int>((int)value);
        ImGui.SliderInt(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}

public class SliderInt2Attribute : ImGuiAttribute
{
    private string _label;
    private int _v_min;
    private int _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderInt2Attribute(string label,
        int v_min,
        int v_max,
        string format,
        ImGuiSliderFlags flags)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<int>((int)value);
        ImGui.SliderInt2(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}

public class SliderInt3Attribute : ImGuiAttribute
{
    private string _label;
    private int _v_min;
    private int _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderInt3Attribute(string label,
        int v_min,
        int v_max,
        string format,
        ImGuiSliderFlags flags)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<int>((int)value);
        ImGui.SliderInt3(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}

public class SliderInt4Attribute : ImGuiAttribute
{
    private string _label;
    private int _v_min;
    private int _v_max;
    private string _format;
    private ImGuiSliderFlags _flags;

    public SliderInt4Attribute(string label,
        int v_min,
        int v_max,
        string format,
        ImGuiSliderFlags flags)
    {
        _label = label;
        _v_min = v_min;
        _v_max = v_max;
        _format = format;
        _flags = flags;
    }

    public override IRefLikeProperty ApplyAttribute(object value)
    {
        var result =
            new RefLikeProperty<int>((int)value);
        ImGui.SliderInt4(_label, ref result.Value, _v_min, _v_max,
            _format, _flags);
        return result;
    }
}