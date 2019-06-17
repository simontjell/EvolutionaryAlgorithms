using SimpleSystemer.EA.DE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Experiments
{
    public class Period
    {
        public Period(List<Observation> observations)
        {
            Observations = observations;
        }

        public IList<Observation> Observations { get; private set; }

        private NormalizedCollection _normalizedCollection = null;
        public NormalizedCollection NormalizedObservations => _normalizedCollection ?? (_normalizedCollection = Normalizer.Instance.Normalize(Observations));

        protected Period(){}

        public Period WithObservations(List<Observation> observations)
        {
            Observations = observations;
            return this;
        }
    }

    public abstract class Period<TSubPeriod> : Period, IEnumerable<TSubPeriod> where TSubPeriod : Period, new()
    {
        public Period(List<Observation> observations) : base(observations)
        {
        }

        public  IEnumerator<TSubPeriod> GetEnumerator()
            => Observations.GroupBy(GetSubPeriodGroupingKey).Select((group, index) => new TSubPeriod().WithObservations(group.ToList())).Cast<TSubPeriod>().GetEnumerator();

        protected abstract long GetSubPeriodGroupingKey(Observation observation);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    public class Years : Period<Year>
    {
        public IEnumerable<Day> Days => this.SelectMany(year => year.SelectMany(month => month.Select(day => day)));

        public Years(List<Observation> observations) : base(observations)
        {
        }

        public Years() : this(null)
        {

        }

        protected override long GetSubPeriodGroupingKey(Observation observation)
            => observation.Time.Year;
    }


    public class Year : Period<Month>
    {
        public Year(List<Observation> observations) : base(observations)
        {
        }

        public Year() : this(null)
        {

        }

        protected override long GetSubPeriodGroupingKey(Observation observation)
            => observation.Time.Year * 10000 + observation.Time.Month * 100;
    }

    public class Month : Period<Day>
    {
        public Month(List<Observation> observations) : base(observations)
        {
        }

        public Month() : this(null)
        {

        }

        protected override long GetSubPeriodGroupingKey(Observation observation)
            => observation.Time.Year * 10000 + observation.Time.Month * 100 +  observation.Time.Day;
    }

    public class Day : Period
    {
        public Day(List<Observation> observations) : base(observations)
        {
        }

        public Day() : this(null)
        {

        }
    }
}
