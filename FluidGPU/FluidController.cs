using Silk.NET.Input;
using Silk.NET.Maths;

namespace FluidsVulkan.FluidGPU;

public class FluidController
{
    private const float Speed = 0.001f;

    public FluidController(EventHandler eventHandler,
        FluidView view)
    {
        var view1 = view;
        Dictionary<Key, Vector2D<float>> directions = new()
        {
            [Key.W] = new Vector2D<float>(0, -1),
            [Key.S] = new Vector2D<float>(0, 1),
            [Key.A] = new Vector2D<float>(-1, 0),
            [Key.D] = new Vector2D<float>(1, 0),
        };
        eventHandler.OnMouseScrolled += (delta) =>
        {
            view1.Scale += 0.1f*delta;
        };

        eventHandler.OnKeyPressed += (key) =>
        {
            if (directions.TryGetValue(key, out var direction))
                view1.BoxCenter += Speed * direction;
        };
    }
}