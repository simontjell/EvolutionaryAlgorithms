using System;
using System.Collections.Immutable;

namespace SimpleSystemer.EA
{
    public interface IEvolutionaryAlgorithm
    {
        IImmutableList<Generation> Generations { get; }

        event EventHandler OnGenerationFinished;

        IImmutableList<ParetoEvaluatedIndividual> GetBestIndividuals(Generation generation);
        void Optimize();
    }
}