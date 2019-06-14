using SimpleSystemer.EA.DE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Experiments
{
    public class Normalizer
    {

        public NormalizedCollection Normalize(IList<Observation> observations)
            => new NormalizedCollection(
                observations.Zip(
                    Normalize(observations.Select(o => o.Time)).Zip(Normalize(observations.Select(o => o.Value)), (time, value) => (time, value)),
                    (original, normalized) => new NormalizedObservation { Time = normalized.time, Value = normalized.value, Original = original }
                ).OrderBy(o => o.Time).ToList()
            );

        private IEnumerable<double> Normalize(IEnumerable<DateTimeOffset> timestamps)
            => Normalize(timestamps.Select(t => (double)t.UtcTicks));

        private IEnumerable<double> Normalize(IEnumerable<double> values)
        {
            var (min, max) = (values.Min(), values.Max());
            var range = max - min;

            return values.Select(v => (v - min) / range);
        }

        public static Normalizer Instance { get; } = new Normalizer();
    }
}
