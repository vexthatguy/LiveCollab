namespace LiveCollab.Specs
{
    public enum SyncQuality
    {
        Auto = 0,
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Sync profile: tick rate, presence rate, full-snapshot cadence.
    /// Auto profile picks a tier from detected hardware (see HardwareProbe).
    /// </summary>
    public class SyncProfile
    {
        public SyncQuality Quality = SyncQuality.Auto;
        public int TickIntervalMs = 100;
        public int PresenceIntervalMs = 250;
        public int FullSnapshotEveryXTicks = 30;

        public static SyncProfile Resolve(SyncQuality q)
        {
            var p = new SyncProfile { Quality = q };
            if (q == SyncQuality.Auto)
            {
                var specs = HardwareProbe.Detect();
                if (specs.Score >= 5500) q = SyncQuality.High;
                else if (specs.Score >= 3000) q = SyncQuality.Medium;
                else q = SyncQuality.Low;
                p.Quality = q;
            }
            switch (q)
            {
                case SyncQuality.High:
                    p.TickIntervalMs = 50;
                    p.PresenceIntervalMs = 150;
                    p.FullSnapshotEveryXTicks = 40;
                    break;
                case SyncQuality.Medium:
                    p.TickIntervalMs = 100;
                    p.PresenceIntervalMs = 250;
                    p.FullSnapshotEveryXTicks = 30;
                    break;
                default:
                    p.TickIntervalMs = 200;
                    p.PresenceIntervalMs = 400;
                    p.FullSnapshotEveryXTicks = 25;
                    break;
            }
            return p;
        }

        public static string Describe(SyncQuality q)
        {
            switch (q)
            {
                case SyncQuality.High: return "High (~20 Hz) — powerful machine";
                case SyncQuality.Medium: return "Medium (~10 Hz) — balanced";
                case SyncQuality.Low: return "Low (~5 Hz) — lightweight";
                default: return "Auto (chosen by your PC specs)";
            }
        }
    }
}
