using System;
using System.Collections.Generic;
using System.Linq;

namespace Experiments
{
    public class Day
    {
        private readonly Period _parentPeriod;
        public int _parentIndex { get; }

        private NormalizedCollection _normalizedCollection = null;
        public NormalizedCollection NormalizedObservations => _normalizedCollection ?? (_normalizedCollection = Normalizer.Instance.Normalize(Observations));

        public Day Previous => _parentIndex == 0 ? null : _parentPeriod.Days[_parentIndex - 1];


        public Day(List<Observation> observations, int index, Period parentPeriod)
        {
            Observations = observations;
            _parentPeriod = parentPeriod;
            _parentIndex = index;            
        }

        public IList<Observation> Observations { get; }

        public override string ToString()
            => Observations.First().Time.Date.ToShortDateString();
    }
}
