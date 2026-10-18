namespace Gamejam2026
{
    public static class LanAuthority
    {
        public static bool InSession => LanFlow.InSession;
        public static bool IsHost => LanFlow.IsHost;
        public static bool IsClientView => LanFlow.IsClientView;
        public static bool Simulates => LanFlow.Simulates;
        public static bool LanMatch => LanFlow.LanMatch;
        public static int LocalSlot => LanFlow.LocalSlot;
    }
}
