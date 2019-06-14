using System;

namespace Experiments
{
    public class Observation
    {
        public DateTimeOffset Time { get; set; }
        public double Value { get; set; }

        public Observation(DateTimeOffset time, double value)
        {
            Time = time;
            Value = value;
        }

        public override string ToString()
            => $"{Time}: {Value:0.00}";
    }
}
