using System;
using System.IO;
using System.Threading.Tasks;
using NSubstitute;
using Prometheus.Client.Collectors;
using Prometheus.Client.Collectors.Abstractions;
using Prometheus.Client.MetricsWriter;
using Prometheus.Client.MetricsWriter.Abstractions;
using Prometheus.Client.Tests.Resources;
using Xunit;

namespace Prometheus.Client.Tests
{
    public class HistogramTests : BaseTests
    {
        [Theory]
        [MemberData(nameof(GetLabels))]
        public void ThrowOnLabelsMismatch(params string[] labels)
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram = factory.CreateHistogram("test_Histogram", string.Empty, "label1", "label2");
            Assert.ThrowsAny<ArgumentException>(() => histogram.WithLabels(labels));
        }

        [Theory]
        [MemberData(nameof(InvalidLabels))]
        public void ThrowOnInvalidLabels(string label)
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            Assert.ThrowsAny<ArgumentException>(() => factory.CreateHistogram("test_Histogram", string.Empty, label));
        }

        [Theory]
        [InlineData("le")]
        [InlineData("le", "label")]
        [InlineData("label", "le")]
        public void ThrowOnReservedLabelNames(params string[] labels)
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            Assert.ThrowsAny<ArgumentException>(() => factory.CreateHistogram("test_Histogram", string.Empty, labels));
        }

        private static Histogram CreateHistogram1(MetricFactory metricFactory)
        {
            var histogram = metricFactory.CreateHistogram("hist1", "help", new[] { 1.0, 2.0, 3.0 });
            histogram.Observe(1.5);
            histogram.Observe(2.5);
            histogram.Observe(1);
            histogram.Observe(2.4);
            histogram.Observe(2.1);
            histogram.Observe(0.4);
            histogram.Observe(1.4);
            histogram.Observe(1.5);
            histogram.Observe(3.9);
            return histogram;
        }

        [Fact]
        public async Task Collection()
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram1 = CreateHistogram1(factory);

            var histogram2 = factory.CreateHistogram("hist2", "help2", new[] { -5.0, 0, 5.0, 10 });
            histogram2.Observe(-20);
            histogram2.Observe(-1);
            histogram2.Observe(0);
            histogram2.Observe(2.5);
            histogram2.Observe(5);
            histogram2.Observe(9);
            histogram2.Observe(11);

            string formattedText = null;

            using (var stream = new MemoryStream())
            {
                using (var writer = new MetricsTextWriter(stream))
                {
                    ((ICollector)histogram1).Collect(writer);
                    ((ICollector)histogram2).Collect(writer);
                    await writer.CloseWriterAsync();
                }

                stream.Seek(0, SeekOrigin.Begin);

                using (var streamReader = new StreamReader(stream))
                {
                    formattedText = streamReader.ReadToEnd();
                }
            }

            Assert.Equal(ResourcesHelper.GetFileContent("HistogramTests_Collection.txt"), formattedText);
        }

        [Fact]
        public async Task EmptyCollection()
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram = factory.CreateHistogram("hist1", "help", false, false, new[] { 1.0, 2.0, 3.0 });

            string formattedText = null;

            using (var stream = new MemoryStream())
            {
                using (var writer = new MetricsTextWriter(stream))
                {
                    ((ICollector)histogram).Collect(writer);
                    await writer.CloseWriterAsync();
                }

                stream.Seek(0, SeekOrigin.Begin);

                using (var streamReader = new StreamReader(stream))
                {
                    formattedText = streamReader.ReadToEnd();
                }
            }

            Assert.Equal(ResourcesHelper.GetFileContent("HistogramTests_Empty.txt"), formattedText);
        }

        [Fact]
        public async Task SuppressEmpty()
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram = factory.CreateHistogram("hist1", "help", new[] { -5.0, 0, 5.0, 10 }, "type");
            histogram.WithLabels("a").Observe(-20);
            histogram.WithLabels("a").Observe(-1);
            histogram.WithLabels("a").Observe(0);
            histogram.WithLabels("a").Observe(2.5);
            histogram.WithLabels("a").Observe(5);
            histogram.WithLabels("a").Observe(9);
            histogram.WithLabels("a").Observe(11);

            string formattedText = null;

            using (var stream = new MemoryStream())
            {
                using (var writer = new MetricsTextWriter(stream))
                {
                    ((ICollector)histogram).Collect(writer);
                    await writer.CloseWriterAsync();
                }

                stream.Seek(0, SeekOrigin.Begin);

                using (var streamReader = new StreamReader(stream))
                {
                    formattedText = streamReader.ReadToEnd();
                }
            }

            Assert.Equal(ResourcesHelper.GetFileContent("HistogramTests_SuppressEmpty.txt"), formattedText);
        }

        [Fact]
        public async Task Empty_SuppressEmpty()
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram = factory.CreateHistogram("hist1", "help", new[] { -5.0, 0, 5.0, 10 }, "type");
            // No any Observe
            string formattedText;

            using (var stream = new MemoryStream())
            {
                using (var writer = new MetricsTextWriter(stream))
                {
                    ((ICollector)histogram).Collect(writer);
                    await writer.CloseWriterAsync();
                }

                stream.Seek(0, SeekOrigin.Begin);

                using (var streamReader = new StreamReader(stream))
                {
                    formattedText = streamReader.ReadToEnd();
                }
            }

            Assert.Equal(ResourcesHelper.GetFileContent("HistogramTests_Empty_SuppressEmpty.txt"), formattedText);
        }

        [Fact]
        public void MetricsWriterApiUsage()
        {
            var writer = Substitute.For<IMetricsWriter>();
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram1 = CreateHistogram1(factory);

            ((ICollector)histogram1).Collect(writer);

            Received.InOrder(() =>
            {
                writer.StartMetric("hist1");
                writer.WriteHelp("help");
                writer.WriteType(MetricType.Histogram);

                var sample = writer.StartSample("_bucket");
                var lbl = sample.StartLabels();
                lbl.WriteLabel("le", "1");
                lbl.EndLabels();
                sample.WriteValue(2);
                sample.EndSample();

                sample = writer.StartSample("_bucket");
                lbl = sample.StartLabels();
                lbl.WriteLabel("le", "2");
                lbl.EndLabels();
                sample.WriteValue(5);
                sample.EndSample();

                sample = writer.StartSample("_bucket");
                lbl = sample.StartLabels();
                lbl.WriteLabel("le", "3");
                lbl.EndLabels();
                sample.WriteValue(8);
                sample.EndSample();

                sample = writer.StartSample("_bucket");
                lbl = sample.StartLabels();
                lbl.WriteLabel("le", "+Inf");
                lbl.EndLabels();
                sample.WriteValue(9);
                sample.EndSample();

                sample = writer.StartSample("_sum");
                sample.WriteValue(16.7);
                sample.EndSample();

                sample = writer.StartSample("_count");
                sample.WriteValue(9);
                sample.EndSample();

                writer.EndMetric();
            });
        }

        [Fact]
        public void SameLabelReturnsSameMetric()
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram = factory.CreateHistogram("test_histogram", string.Empty, "label");
            var labeled1 = histogram.WithLabels("value");
            var labeled2 = histogram.WithLabels("value");

            Assert.Equal(labeled1, labeled2);
        }

        [Fact]
        public void SameLabelsReturnsSameMetric()
        {
            var registry = new CollectorRegistry();
            var factory = new MetricFactory(registry);

            var histogram = factory.CreateHistogram("test_histogram", string.Empty, "label1", "label2");
            var labeled1 = histogram.WithLabels("value1", "value2");
            var labeled2 = histogram.WithLabels("value1", "value2");

            Assert.Equal(labeled1, labeled2);
        }
    }
}
