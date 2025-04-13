using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace FluidsVulkan;

public class EventHandler : IDisposable
{
    private IKeyboard _keyboard;
    private IMouse _mouse;
    private IInputContext _inputContext;

    // Клавиши
    private HashSet<Key> _prevKeys = [];
    private HashSet<Key> _currentKeys = [];

    
    private HashSet<MouseButton> _prevMouseButtons = [];
    private HashSet<MouseButton> _currentMouseButtons = [];
    private Vector2 _mousePosition;
    private Vector2 _mouseDelta = Vector2.Zero;
    private Vector2 _lastMousePosition;

    
    private float _mouseScrollDelta = 0f;
    
    public event Action<Key> OnKeyJustPressed;
    public event Action<Key> OnKeyPressed;
    public event Action<Key> OnKeyReleased;
    public event Action<MouseButton> OnMousePressed;
    public event Action<MouseButton> OnMouseReleased;
    public event Action<Vector2> OnMouseMoved;
    public event Action<float> OnMouseScrolled;

    public EventHandler(IWindow window)
    {
        _inputContext = window.CreateInput();
        _keyboard = _inputContext.Keyboards[0];
        _mouse = _inputContext.Mice[0];

        _mousePosition = _mouse.Position;
        _lastMousePosition = _mousePosition;
        
        _mouse.Scroll += (mouse, scrollDelta) =>
        {
            _mouseScrollDelta = scrollDelta.Y;
            OnMouseScrolled?.Invoke(scrollDelta.Y);
        };
    }

    public void Update()
    {
        // Клавиатура
        _prevKeys = new HashSet<Key>(_currentKeys);
        _currentKeys.Clear();

        foreach (Key key in Enum.GetValues<Key>())
        {
            if (_keyboard.IsKeyPressed(key))
            {
                if (!_prevKeys.Contains(key))
                {
                    OnKeyJustPressed?.Invoke(key);
                }
                else
                {
                    OnKeyPressed?.Invoke(key);
                }

                _currentKeys.Add(key);
                
            }
            else if (_prevKeys.Contains(key))
            {
                OnKeyReleased?.Invoke(key);
            }
        }

        // Мышь
        _prevMouseButtons = new HashSet<MouseButton>(_currentMouseButtons);
        _currentMouseButtons.Clear();

        foreach (MouseButton button in Enum.GetValues<MouseButton>())
        {
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
        }

        _lastMousePosition = _mousePosition;
        _mousePosition = _mouse.Position;
        _mouseDelta = _mousePosition - _lastMousePosition;

        // Мышь движение
        if (_mousePosition != _lastMousePosition)
        {
            OnMouseMoved?.Invoke(_mousePosition);
        }
    }

    // ==== Клавиши ====
    public bool IsKeyHeld(Key key)
        => _currentKeys.Contains(key);

    public bool IsKeyJustPressed(Key key)
        => _currentKeys.Contains(key) && !_prevKeys.Contains(key);

    public bool IsKeyJustReleased(Key key)
        => !_currentKeys.Contains(key) && _prevKeys.Contains(key);

    // ==== Мышь ====
    public bool IsMouseHeld(MouseButton button)
        => _currentMouseButtons.Contains(button);

    public bool IsMouseJustPressed(MouseButton button)
        => _currentMouseButtons.Contains(button) && !_prevMouseButtons.Contains(button);

    public bool IsMouseJustReleased(MouseButton button)
        => !_currentMouseButtons.Contains(button) && _prevMouseButtons.Contains(button);

    public Vector2 GetMousePosition() => _mousePosition;
    public Vector2 GetMouseDelta() => _mouseDelta;
    

    // ==== Скроллинг ====
    public float GetMouseScrollDelta() => _mouseScrollDelta;

    public void Dispose()
    {
        _inputContext?.Dispose();
    }
}