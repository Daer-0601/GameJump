using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamejam2026
{
    // Centraliza los controles; no mueve objetos ni modifica el estado de la partida.
    public static class DemoPlayerInput
    {
        public const string Help = "J1: WASD, E usar o mantener, Shift izq. correr, Q soltar, F/H y T/G mirar (o mouse), Tab cuaderno.\n"
            + "J2: flechas, U usar o mantener, Shift der. correr, O soltar, J/L e I/K mirar, Ctrl der. precisión, P cuaderno. Esc pausa.";

        public readonly struct State
        {
            public readonly Vector2 Move;
            public readonly Vector2 Look;
            public readonly bool Run;
            public readonly bool Use;
            public readonly bool UseHeld;
            public readonly bool Drop;

            public State(Vector2 move, Vector2 look, bool run, bool use, bool useHeld, bool drop)
            {
                Move = Vector2.ClampMagnitude(move, 1f);
                Look = look;
                Run = run;
                Use = use;
                UseHeld = useHeld;
                Drop = drop;
            }
        }

        public static string UseLabel(int playerIndex) => LanAuthority.LanMatch ? "E" : playerIndex == 0 ? "E" : "U";
        public static string DropLabel(int playerIndex) => LanAuthority.LanMatch ? "Q" : playerIndex == 0 ? "Q" : "O";
        public static string NotebookLabel(int playerIndex) => playerIndex == 0 ? "Tab" : "P";
        public static string PageLabel(int playerIndex) => playerIndex == 0 ? "A / D" : "flechas izq. / der.";

        public static bool NotebookPressed(int playerIndex)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return playerIndex == 0 ? keyboard.tabKey.wasPressedThisFrame : playerIndex == 1 && keyboard.pKey.wasPressedThisFrame;
        }

        // -1 hoja anterior, +1 siguiente; solo se consulta con el cuaderno abierto.
        public static int PageTurn(int playerIndex)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return 0;
            if (playerIndex == 0)
                return (keyboard.dKey.wasPressedThisFrame ? 1 : 0) - (keyboard.aKey.wasPressedThisFrame ? 1 : 0);
            if (playerIndex == 1)
                return (keyboard.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftArrowKey.wasPressedThisFrame ? 1 : 0);
            return 0;
        }

        public static State Read(int playerIndex, float keyboardLookSpeed, float mouseSensitivity, float deltaTime,
            float preciseLookMultiplier = 0.35f)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || playerIndex < 0 || playerIndex > 1) return default;

            Vector2 move;
            Vector2 look;
            bool run;
            bool use;
            bool useHeld;
            bool drop;
            if (playerIndex == 0)
            {
                move = new Vector2(Axis(keyboard.aKey.isPressed, keyboard.dKey.isPressed),
                    Axis(keyboard.sKey.isPressed, keyboard.wKey.isPressed));
                look = new Vector2(Axis(keyboard.fKey.isPressed, keyboard.hKey.isPressed),
                    Axis(keyboard.gKey.isPressed, keyboard.tKey.isPressed)) * keyboardLookSpeed * deltaTime;
                if (Mouse.current != null) look += Mouse.current.delta.ReadValue() * mouseSensitivity;
                run = keyboard.leftShiftKey.isPressed;
                use = keyboard.eKey.wasPressedThisFrame;
                useHeld = keyboard.eKey.isPressed;
                drop = keyboard.qKey.wasPressedThisFrame;
            }
            else
            {
                move = new Vector2(Axis(keyboard.leftArrowKey.isPressed, keyboard.rightArrowKey.isPressed),
                    Axis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed));
                var lookAxis = new Vector2(Axis(keyboard.jKey.isPressed, keyboard.lKey.isPressed),
                    Axis(keyboard.kKey.isPressed, keyboard.iKey.isPressed));
                float lookMultiplier = keyboard.rightCtrlKey.isPressed ? Mathf.Clamp(preciseLookMultiplier, 0.1f, 1f) : 1f;
                look = Vector2.ClampMagnitude(lookAxis, 1f) * keyboardLookSpeed * lookMultiplier * deltaTime;
                run = keyboard.rightShiftKey.isPressed;
                use = keyboard.uKey.wasPressedThisFrame;
                useHeld = keyboard.uKey.isPressed;
                drop = keyboard.oKey.wasPressedThisFrame;
            }
            return new State(move, look, run, use, useHeld, drop);
        }

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
