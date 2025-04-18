using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace FluidsVulkan;

public class EventHandler : IDisposable
{
    private readonly HashSet<Key> _currentKeys = [];
    private readonly HashSet<MouseButton> _currentMouseButtons = [];
    private readonly IKeyboard _keyboard;
    private readonly IMouse _mouse;
    private Vector2 _lastMousePosition;
    private Vector2 _mouseDelta = Vector2.Zero;
    private Vector2 _mousePosition;


    private float _mouseScrollDelta;

    private HashSet<Key> _prevKeys = [];


    private HashSet<MouseButton> _prevMouseButtons = [];

    public EventHandler(IWindow window)
    {
        InputContext = window.CreateInput();
        _keyboard = InputContext.Keyboards[0];
        _mouse = InputContext.Mice[0];

        _mousePosition = _mouse.Position;
        _lastMousePosition = _mousePosition;

        _mouse.Scroll += (_, scrollDelta) =>
        {
            _mouseScrollDelta = scrollDelta.Y;
            OnMouseScrolled?.Invoke(scrollDelta.Y);
        };
    }

    public IInputContext InputContext { get; }

    public void Dispose()
    {
        InputContext?.Dispose();
    }

    public event Action<Key> OnKeyJustPressed;
    public event Action<Key> OnKeyPressed;
    public event Action<Key> OnKeyReleased;
    public event Action<MouseButton> OnMousePressed;
    public event Action<MouseButton> OnMouseReleased;
    public event Action<Vector2> OnMouseMoved;
    public event Action<float> OnMouseScrolled;

    public void Update()
    {
        // Клавиатура
        _prevKeys = new HashSet<Key>(_currentKeys);
        _currentKeys.Clear();

        foreach (var key in Enum.GetValues<Key>())
            if (_keyboard.IsKeyPressed(key))
            {
                if (!_prevKeys.Contains(key))
                    OnKeyJustPressed?.Invoke(key);
                else
                    OnKeyPressed?.Invoke(key);

                _currentKeys.Add(key);
            }
            else if (_prevKeys.Contains(key))
            {
                OnKeyReleased?.Invoke(key);
            }


        _prevMouseButtons =
            new HashSet<MouseButton>(_currentMouseButtons);
        _currentMouseButtons.Clear();

        foreach (var button in Enum.GetValues<MouseButton>())
            if (_mouse.IsButtonPressed(button))
            {
                if (!_prevMouseButtons.Contains(button))
                    OnMousePressed?.Invoke(button);
                _currentMouseButtons.Add(button);
            }
            else if (_prevMouseButtons.Contains(button))
            {
                OnMouseReleased?.Invoke(button);
            }

        _lastMousePosition = _mousePosition;
        _mousePosition = _mouse.Position;
        _mouseDelta = _mousePosition - _lastMousePosition;


        if (_mousePosition != _lastMousePosition)
            OnMouseMoved?.Invoke(_mousePosition);
    }

    public bool IsKeyHeld(Key key)
    {
        return _currentKeys.Contains(key);
    }

    public bool IsKeyJustPressed(Key key)
    {
        return _currentKeys.Contains(key) && !_prevKeys.Contains(key);
    }

    public bool IsKeyJustReleased(Key key)
    {
        return !_currentKeys.Contains(key) && _prevKeys.Contains(key);
    }


    public bool IsMouseHeld(MouseButton button)
    {
        return _currentMouseButtons.Contains(button);
    }

    public bool IsMouseJustPressed(MouseButton button)
    {
        return _currentMouseButtons.Contains(button) &&
               !_prevMouseButtons.Contains(button);
    }

    public bool IsMouseJustReleased(MouseButton button)
    {
        return !_currentMouseButtons.Contains(button) &&
               _prevMouseButtons.Contains(button);
    }

    public Vector2 GetMousePosition()
    {
        return _mousePosition;
    }

    public Vector2 GetMouseDelta()
    {
        return _mouseDelta;
    }


    public float GetMouseScrollDelta()
    {
        return _mouseScrollDelta;
    }
}