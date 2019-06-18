using System;
using System.Collections.Generic;
using System.Linq;

namespace Experiments
{
    public class NormalizedCollection
    {
        private List<NormalizedObservation> _normalizedObservations;

        public NormalizedCollection(List<NormalizedObservation> normalizedObservations)
        {
            _normalizedObservations = normalizedObservations;
        }

        public NormalizedObservation this[double index]
            =>
                ValidateIndex(index) ??
                Interpolate(
                    _normalizedObservations.Zip(
                        _normalizedObservations.Skip(1), (arg1, arg2) => (arg1, arg2)
                    )
                    .First(((NormalizedObservation before, NormalizedObservation after) arg) => arg.before.Time <= index && arg.after.Time >= index),
                    index
                );

        private NormalizedObservation ValidateIndex(double index)
            => index >= 0.0 && index <= 1.0 ? (NormalizedObservation)null : throw new ArgumentOutOfRangeException(nameof(index));

        private NormalizedObservation Interpolate((NormalizedObservation before, NormalizedObservation after) beforeAfter, double index)
            => new NormalizedObservation {
                Time = index,
                Value = (index - beforeAfter.before.Time) / (beforeAfter.after.Time - beforeAfter.before.Time) * (beforeAfter.after.Value - beforeAfter.before.Value) + beforeAfter.before.Value,
                AbsoluteTime = new DateTime( 
                    beforeAfter.before.Original.Time.AddTicks(
                            (long)(index * (beforeAfter.after.Original.Time - beforeAfter.before.Original.Time).Ticks)
                        )
                        .Ticks
                    )
            };

    }
}
