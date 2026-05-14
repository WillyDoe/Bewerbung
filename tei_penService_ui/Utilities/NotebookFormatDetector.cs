using System;
using System.Collections.Generic;
using System.Linq;
using TeiPenServiceConnectionManager.Utilities;
using tei_penService_ui.Models;

namespace tei_penService_ui.Utilities
{
    /// <summary>
    /// Schätzt Notizbuch-Papierformat und horizontalen Seiten-Stride aus pro-Seiten-Bounds.
    /// </summary>
    public static class NotebookFormatDetector
    {
        private readonly struct StandardPaper
        {
            public StandardPaper(string label, float widthMm, float heightMm)
            {
                Label = label;
                WidthMm = widthMm;
                HeightMm = heightMm;
                WidthCells = widthMm / NCodeCoordinateConverter.NCodeCellSizeMm;
                HeightCells = heightMm / NCodeCoordinateConverter.NCodeCellSizeMm;
                Aspect = heightMm / widthMm;
            }

            public string Label { get; }
            public float WidthMm { get; }
            public float HeightMm { get; }
            public float WidthCells { get; }
            public float HeightCells { get; }
            public float Aspect { get; }
        }

        private static readonly StandardPaper[] StandardPapers =
        {
            new StandardPaper("A4", 210f, 297f),
            new StandardPaper("A5", 148f, 210f),
            new StandardPaper("B5", 176f, 250f),
            new StandardPaper("Letter", 216f, 279f),
            new StandardPaper("Legal", 216f, 356f),
        };

        private const float MaxAspectDeviation = 0.12f;
        private const float StrideMarginFactor = 1.02f;

        /// <summary>
        /// Ermittelt Stride und Format-Schätzung. <paramref name="userMinStrideCells"/> optionaler Unterboden (0 = ignorieren).
        /// </summary>
        public static NotebookFormatEstimate Estimate(
            string bookKey,
            IReadOnlyDictionary<int, PageDotBounds> boundsByPage,
            float maxObservedMaxXAcrossPages,
            float userMinStrideCells,
            float minInferredStrideCells,
            float maxPhysicalPageWidthCells)
        {
            if (boundsByPage == null || boundsByPage.Count == 0)
            {
                float fb = userMinStrideCells > 0f ? userMinStrideCells : 72f;
                return new NotebookFormatEstimate
                {
                    BookKey = bookKey,
                    StandardFormatLabel = "",
                    WidthCells = fb,
                    HeightCells = 0f,
                    WidthMm = 0f,
                    HeightMm = 0f,
                    Confidence = 0f,
                    SamplePageCount = 0,
                    HorizontalStrideCells = fb,
                    UsedStandardFormatMatch = false
                };
            }

            var maxXList = new List<float>();
            var maxYList = new List<float>();
            var widthSpanList = new List<float>();
            var heightSpanList = new List<float>();
            foreach (var kv in boundsByPage)
            {
                var b = kv.Value;
                if (b.HasX) maxXList.Add(b.MaxX);
                if (b.HasY) maxYList.Add(b.MaxY);
                if (b.WidthSpan > 0.5f) widthSpanList.Add(b.WidthSpan);
                if (b.HeightSpan > 0.5f) heightSpanList.Add(b.HeightSpan);
            }

            int pageCount = boundsByPage.Count;
            float medianMaxX = Median(maxXList);
            float medianMaxY = Median(maxYList);
            float medianWidthSpan = Median(widthSpanList);
            float medianHeightSpan = Median(heightSpanList);
            float maxMaxX = maxXList.Count > 0 ? maxXList.Max() : 0f;

            float observedStrideFallback = Math.Max(
                maxObservedMaxXAcrossPages * StrideMarginFactor,
                Math.Max(medianMaxX * StrideMarginFactor, minInferredStrideCells));

            observedStrideFallback = Math.Min(observedStrideFallback, maxPhysicalPageWidthCells);
            if (userMinStrideCells > 0f)
                observedStrideFallback = Math.Max(observedStrideFallback, userMinStrideCells);

            bool canUseAspectFromSpan = medianWidthSpan > 1f && medianHeightSpan > 1f;
            bool canUseAspectFromMax = medianMaxX > 1f && medianMaxY > 1f;
            float ratioObs = 0f;
            if (canUseAspectFromSpan)
                ratioObs = medianHeightSpan / medianWidthSpan;
            else if (canUseAspectFromMax)
                ratioObs = medianMaxY / medianMaxX;
            bool canUseAspect = canUseAspectFromSpan || canUseAspectFromMax;

            StandardPaper? best = null;
            float bestAspectScore = float.MaxValue;

            if (canUseAspect)
            {
                foreach (var p in StandardPapers)
                {
                    float d = Math.Abs(ratioObs - p.Aspect);
                    if (d < bestAspectScore)
                    {
                        bestAspectScore = d;
                        best = p;
                    }
                }
            }

            bool matchOk = best.HasValue && bestAspectScore <= MaxAspectDeviation;
            float strideFromStandard = 0f;
            if (matchOk && best.HasValue)
            {
                var paper = best.Value;
                strideFromStandard = paper.WidthCells * StrideMarginFactor;
                strideFromStandard = Math.Min(strideFromStandard, maxPhysicalPageWidthCells);
                strideFromStandard = Math.Max(strideFromStandard, maxMaxX * StrideMarginFactor);
                strideFromStandard = Math.Max(strideFromStandard, minInferredStrideCells);
                if (userMinStrideCells > 0f)
                    strideFromStandard = Math.Max(strideFromStandard, userMinStrideCells);
            }

            float horizontalStride = matchOk && strideFromStandard > 0f
                ? strideFromStandard
                : observedStrideFallback;

            float confidence = ComputeConfidence(pageCount, bestAspectScore, matchOk);
            string label = matchOk && best.HasValue ? best.Value.Label : "Unbekannt (aus Strichen)";

            float wMm = medianMaxX * NCodeCoordinateConverter.NCodeCellSizeMm;
            float hMm = medianMaxY * NCodeCoordinateConverter.NCodeCellSizeMm;

            float widthCellsOut = matchOk && best.HasValue ? best.Value.WidthCells : medianMaxX;
            float heightCellsOut = matchOk && best.HasValue ? best.Value.HeightCells : medianMaxY;

            return new NotebookFormatEstimate
            {
                BookKey = bookKey,
                StandardFormatLabel = label,
                WidthCells = widthCellsOut,
                HeightCells = heightCellsOut,
                WidthMm = matchOk && best.HasValue ? best.Value.WidthMm : wMm,
                HeightMm = matchOk && best.HasValue ? best.Value.HeightMm : hMm,
                Confidence = confidence,
                SamplePageCount = pageCount,
                HorizontalStrideCells = horizontalStride,
                UsedStandardFormatMatch = matchOk
            };
        }

        private static float ComputeConfidence(int pageCount, float aspectDeviation, bool matched)
        {
            float pagesPart = Math.Min(1f, pageCount / 5f) * 0.35f;
            float aspectPart = matched ? (1f - Math.Min(1f, aspectDeviation / MaxAspectDeviation)) * 0.55f : 0f;
            return Math.Min(1f, 0.1f + pagesPart + aspectPart);
        }

        private static float Median(List<float> values)
        {
            if (values == null || values.Count == 0)
                return 0f;
            if (values.Count == 1)
                return values[0];
            var sorted = values.OrderBy(x => x).ToList();
            int n = sorted.Count;
            if (n % 2 == 1)
                return sorted[n / 2];
            return (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
        }
    }
}
