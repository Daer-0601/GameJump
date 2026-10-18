using System;

namespace Gamejam2026
{
    // Evita que una presentación recibida por red vuelva a enviarse al otro equipo.
    public static class LanEcho
    {
        public static bool Applying { get; private set; }

        public static void Apply(Action action)
        {
            bool previous = Applying;
            Applying = true;
            try { action?.Invoke(); }
            finally { Applying = previous; }
        }
    }
}
