using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamejam2026
{
    // Centraliza los controles; no mueve objetos ni modifica el estado de la partida.
    public static class DemoPlayerInput
    {
        public const string Help = "J1: WASD, E usar, Shift izq. correr, Q soltar; F/H y T/G mirar (o mouse).\n"
            + "J2: flechas, U usar, Shift der. correr, O soltar; J/L e I/K mirar. Esc pausa.";

        public readonly struct State
        {
            public readonly Vector2 Move;
            public readonly Vector2 Look;
            public readonly bool Run;
            public readonly bool Use;
            public readonly bool Drop;

            public State(Vector2 move, Vector2 look, bool run, bool use, bool drop)
            {
                Move = Vector2.ClampMagnitude(move, 1f);
                Look = look;
                Run = run;
                Use = use;
                Drop = drop;
            }
        }

        public static string UseLabel(int playerIndex) => playerIndex == 0 ? "E" : "U";
        public static string DropLabel(int playerIndex) => playerIndex == 0 ? "Q" : "O";

        public static State Read(int playerIndex, float keyboardLookSpeed, float mouseSensitivity, float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || playerIndex < 0 || playerIndex > 1) return default;

            Vector2 move;
            Vector2 look;
            bool run;
            bool use;
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
                drop = keyboard.qKey.wasPressedThisFrame;
            }
            else
            {
                move = new Vector2(Axis(keyboard.leftArrowKey.isPressed, keyboard.rightArrowKey.isPressed),
                    Axis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed));
                look = new Vector2(Axis(keyboard.jKey.isPressed, keyboard.lKey.isPressed),
                    Axis(keyboard.kKey.isPressed, keyboard.iKey.isPressed)) * keyboardLookSpeed * deltaTime;
                run = keyboard.rightShiftKey.isPressed;
                use = keyboard.uKey.wasPressedThisFrame;
                drop = keyboard.oKey.wasPressedThisFrame;
            }
            return new State(move, look, run, use, drop);
        }

        static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
