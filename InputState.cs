using Microsoft.Xna.Framework.Input;

namespace FactoryGame;

internal sealed class InputState
{
    private KeyboardState _prevKeyboard;
    private KeyboardState _currKeyboard;
    private MouseState _prevMouse;
    private MouseState _currMouse;

    public void Update()
    {
        _prevKeyboard = _currKeyboard;
        _prevMouse = _currMouse;
        _currKeyboard = Keyboard.GetState();
        _currMouse = Mouse.GetState();
    }

    public bool KeyPressed(Keys key) => _currKeyboard.IsKeyDown(key) && !_prevKeyboard.IsKeyDown(key);
    public bool KeyDown(Keys key) => _currKeyboard.IsKeyDown(key);

    public bool LeftClicked => _currMouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
    public bool RightClicked => _currMouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;

    public int ScrollDelta => _currMouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;

    public int MouseX => _currMouse.X;
    public int MouseY => _currMouse.Y;
}
