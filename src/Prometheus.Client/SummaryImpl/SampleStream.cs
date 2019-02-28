using System;
using System.Collections.Generic;

namespace Prometheus.Client.SummaryImpl
{
    public class SampleStream
    {
        private readonly Invariant _invariant;
        private readonly List<Sample> _samples = new List<Sample>();

        public SampleStream(Invariant invariant)
        {
            _invariant = invariant;
        }

        public double N { get; set; }

        public int Count => (int)N;

        public int SampleCount => _samples.Count;

        public void Merge(List<Sample> samples)
        {
            // TODO(beorn7): This tries to merge not only individual samples, but
            // whole summaries. The paper doesn't mention merging summaries at
            // all. Unittests show that the merging is inaccurate. Find out how to
            // do merges properly.

            double r = 0;
            int i = 0;

            foreach (var sample in samples)
            {
                bool inserted = false;
                for (; i < _samples.Count; i++)
                {
                    var c = _samples[i];

                    if (c.Value > sample.Value)
                    {
                        // Insert at position i
                        _samples.Insert(i,
                            new Sample
                            {
                                Value = sample.Value,
                                Width = sample.Width,
                                Delta = Math.Max(sample.Delta, Math.Floor(_invariant(this, r)) - 1)
                            });

                        i++;
                        inserted = true;
                        break;
                    }

                    r += c.Width;
                }

                if (!inserted)
                {
                    _samples.Add(new Sample
                    {
                        Value = sample.Value,
                        Width = sample.Width,
                        Delta = 0
                    });
                    i++;
                }

                N += sample.Width;
                r += sample.Width;
            }

            Compress();
        }

        private void Compress()
        {
            if (_samples.Count < 2)
                return;

            var x = _samples[_samples.Count - 1];
            int xi = _samples.Count - 1;
            double r = N - 1 - x.Width;

            for (int i = _samples.Count - 2; i >= 0; i--)
            {
                var c = _samples[i];

                if (c.Width + x.Width + x.Delta <= _invariant(this, r))
                {
                    x.Width += c.Width;
                    _samples[xi] = x;
                    _samples.RemoveAt(i);
                    xi -= 1;
                }
                else
                {
                    x = c;
                    xi = i;
                }

                r -= c.Width;
            }
        }

        public void Reset()
        {
            _samples.Clear();
            N = 0;
        }

        public double Query(double q)
        {
            double t = Math.Ceiling(q * N);
            t += Math.Ceiling(_invariant(this, t) / 2);
            var p = _samples[0];
            double r = 0;

            for (int i = 1; i < _samples.Count; i++)
            {
                var c = _samples[i];
                r += p.Width;

                if (r + c.Width + c.Delta > t)
                    return p.Value;

                p = c;
            }

            return p.Value;
        }
    }
}
