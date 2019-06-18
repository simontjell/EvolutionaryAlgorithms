using SimpleSystemer.EA.DE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Experiments
{
    public class Period
    {
        public Period(IList<Observation> observations)
        {
            Observations = observations;
        }

        public IList<Observation> Observations { get; private set; }

        private NormalizedCollection _normalizedCollection = null;
        public NormalizedCollection NormalizedObservations => _normalizedCollection ?? (_normalizedCollection = Normalizer.Instance.Normalize(Observations));
    }

    public abstract class Period<TSubPeriod> : Period, IEnumerable<TSubPeriod> where TSubPeriod : Period
    {
        public Period(IList<Observation> observations) : base(observations)
        {
        }

        public  IEnumerator<TSubPeriod> GetEnumerator()
            => Observations.GroupBy(GetSubPeriodGroupingKey).Select(MakeSubPeriod).GetEnumerator();

        protected abstract long GetSubPeriodGroupingKey(Observation observation);
        protected abstract TSubPeriod MakeSubPeriod(IGrouping<long, Observation> observations);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    public class Years : Period<Year>
    {
        public Years(IList<Observation> observations) : base(observations)
        {
        }

        public IEnumerable<Day> Days => this.SelectMany(year => year.SelectMany(month => month.Select(day => day)));

        protected override long GetSubPeriodGroupingKey(Observation observation)
            => observation.Time.Year;

        protected override Year MakeSubPeriod(IGrouping<long, Observation> observations)
            => new Year(observations.ToList());
    }


    public class Year : Period<Month>
    {
        public Year(IList<Observation> observations) : base(observations)
        {
        }

        protected override long GetSubPeriodGroupingKey(Observation observation)
            => observation.Time.Year * 10000 + observation.Time.Month * 100;

        protected override Month MakeSubPeriod(IGrouping<long, Observation> observations)
            => new Month(observations.ToList());
    }

    public class Month : Period<Day>
    {
        public Month(IList<Observation> observations) : base(observations)
        {
        }

        protected override long GetSubPeriodGroupingKey(Observation observation)
            => observation.Time.Year * 10000 + observation.Time.Month * 100 +  observation.Time.Day;

        protected override Day MakeSubPeriod(IGrouping<long, Observation> observations)
            => new Day(observations.ToList());
    }

    public class Day : Period
    {
        public Day(IList<Observation> observations) : base(observations)
        {
        }
    }
}
