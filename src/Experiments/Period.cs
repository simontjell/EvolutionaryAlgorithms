using SimpleSystemer.EA.DE;
using System.Collections.Generic;
using System.Linq;

namespace Experiments
{
    public class Period
    {
        public IList<Day> Days { get; }

        public Period(IEnumerable<Observation> observations)
        {
            Days = observations.GroupBy(o => o.Time.Date).Select((day, index) => new Day(day.ToList(), index, this)).ToList();
        }

    }
}
