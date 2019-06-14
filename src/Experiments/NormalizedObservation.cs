namespace Experiments
{
    public class NormalizedObservation
    {
        public double Time { get; set; }
        public double Value { get; set; }
        public Observation Original { get; internal set; }

        public override string ToString()
            => $"{Time:0.00}: {Value:0.00} ({Original?.ToString() ?? "null"})";
    }
}
