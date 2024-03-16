using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DifferentialEvolution
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TResult> SelectInvoke<TResult>(this IEnumerable<Func<TResult>> source) 
            => source.Select(f => f());

        public static (TElement, TElement, TElement) TakeTriplet<TElement>(this IEnumerable<TElement> source)
        {
            var elements = source.Take(3).ToArray();
            return (elements[0], elements[1], elements[2]);
        }
    }
}
