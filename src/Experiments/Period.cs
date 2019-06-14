using SimpleSystemer.EA.DE;
using System.Collections.Generic;
using System.Linq;

namespace Experiments
{
    public class Period
    {
        public IList<Period> Days => Observations.GroupBy(o => o.Time.Date).Select((day, index) => new Period(day.ToList(), index, this)).ToList();

        public Period(List<Observation> observations, int? parentIndex = null, Period parentPeriod = null)
        {
            Observations = observations;
            _parentPeriod = parentPeriod;
            _parentIndex = parentIndex;
        }

        public IList<Observation> Observations { get; }
        private readonly Period _parentPeriod;
        public int? _parentIndex { get; }

        private NormalizedCollection _normalizedCollection = null;
        public NormalizedCollection NormalizedObservations => _normalizedCollection ?? (_normalizedCollection = Normalizer.Instance.Normalize(Observations));

        public Period Previous => (_parentIndex ?? 0) == 0 ? null : _parentPeriod.Days[_parentIndex.Value - 1];

    }
}
