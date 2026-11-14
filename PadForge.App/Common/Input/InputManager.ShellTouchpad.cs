using System;
using PadForge.Engine.Data;
using PadForge.ViewModels;
using System.Linq;

namespace PadForge.Common.Input
{
    public partial class InputManager
    {
        private (Guid Device, int Contact, float X, float Y, long Tick)? _shellPointer;

        private bool _shellTouchpadPressed;
        private bool _shellTwoFingerTap;
        private bool _shellTapConsumed;
        private long _shellTapStarted;

        private static void ShellMouseClick(MacroMouseButton button)
        {
            SendMouseButtonInput(button, down: true);
            SendMouseButtonInput(button, down: false);
        }

        private void UpdateShellTouchpadMouse(UserDevice device)
        {
            var pads = device.InputState.Touchpads;
            var pad = pads != null && pads.Length > 0 ? pads[0] : null;
            bool pressed = pad?.Clicked == true;
            int fingers = pad?.FingerDown?.Count(down => down) ?? 0;
            long now = Environment.TickCount64;
            if (pressed && !_shellTouchpadPressed)
            {
                ShellMouseClick(MacroMouseButton.Left);
                _shellTapConsumed = true;
            }
            _shellTouchpadPressed = pressed;
            if (fingers == 2 && !_shellTapConsumed && !_shellTwoFingerTap)
            {
                _shellTwoFingerTap = true;
                _shellTapStarted = now;
            }
            if (fingers > 2 || pressed) _shellTapConsumed = true;
            if (fingers == 0)
            {
                if (_shellTwoFingerTap && !_shellTapConsumed && now - _shellTapStarted <= 300)
                    ShellMouseClick(MacroMouseButton.Right);
                _shellTwoFingerTap = false;
                _shellTapConsumed = pressed;
            }
            if (pad?.FingerDown == null || pad.FingerDown.Length == 0 || !pad.FingerDown[0])
            {
                _shellPointer = null;
                return;
            }
            for (int i = 1; i < pad.FingerDown.Length; i++)
            {
                if (!pad.FingerDown[i]) continue;
                _shellPointer = null;
                return;
            }
            float x = pad.FingerX[0], y = pad.FingerY[0];
            if (!float.IsFinite(x) || !float.IsFinite(y)) { _shellPointer = null; return; }
            int contact = pad.FingerContactId?[0] ?? -1;
            if (_shellPointer is { } previous && previous.Device == device.InstanceGuid &&
                previous.Contact == contact && now - previous.Tick < 100)
            {
                float dx = x - previous.X, dy = y - previous.Y;
                if (Math.Abs(dx) < 0.15f && Math.Abs(dy) < 0.15f)
                {
                    int moveX = (int)Math.Round(dx * 1600), moveY = (int)Math.Round(dy * 900);
                    if (moveX != 0 || moveY != 0) AccumulateMouseMoveInput(moveX, moveY);
                }
            }
            _shellPointer = (device.InstanceGuid, contact, x, y, now);
        }
    }
}
