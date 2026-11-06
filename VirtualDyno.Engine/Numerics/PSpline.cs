namespace VirtualDyno.Engine.Numerics;

/// <summary>
/// Penalized cubic B-spline (Eilers &amp; Marx P-spline) with the amount of smoothing chosen
/// automatically by restricted maximum likelihood (REML).
/// <para>
/// Power comes from the derivative of RPM over time, and differentiating a logged signal amplifies
/// its noise. Instead of asking the user to pick a smoothing level, the smoothing is estimated from the
/// data: noisy logs get more, clean logs get less. REML is used rather than cross-validation because
/// logger timestamp jitter makes neighbouring errors correlated, and cross-validation then undersmooths
/// and lets noise spikes through as fake peaks. On real AccessPort logs REML gives the same answer
/// whether or not the tip-in transient is included; cross-validation swung by almost 90 hp.
/// </para>
/// <para>
/// The third-order difference penalty means heavy smoothing bends the fit toward a smooth quadratic
/// (acceleration changing linearly) instead of flattening real peaks.
/// </para>
/// </summary>
public static class PSpline
{
    public sealed record Fit(double[] Values, double[] Derivative, double Lambda, double EffectiveDegreesOfFreedom)
    {
        internal double[] Coefficients { get; init; } = [];
        internal double Start { get; init; }
        internal double SegmentWidth { get; init; }
        internal int Segments { get; init; }

        /// <summary>Value and slope of the fitted curve anywhere in (or just beyond) the fitted range.</summary>
        public (double Value, double Derivative) At(double x)
        {
            double pos = (x - Start) / SegmentWidth;
            int seg = Math.Clamp((int)Math.Floor(pos), 0, Segments - 1);
            double u = pos - seg, um = 1 - u;
            Span<double> b = [um * um * um / 6.0, (3 * u * u * u - 6 * u * u + 4) / 6.0, (-3 * u * u * u + 3 * u * u + 3 * u + 1) / 6.0, u * u * u / 6.0];
            Span<double> d = [-um * um / 2.0, (3 * u * u - 4 * u) / 2.0, (-3 * u * u + 2 * u + 1) / 2.0, u * u / 2.0];
            double v = 0, dv = 0;
            for (int a = 0; a < 4; a++)
            {
                v += b[a] * Coefficients[seg + a];
                dv += d[a] * Coefficients[seg + a] / SegmentWidth;
            }
            return (v, dv);
        }
    }

    private const int PenaltyOrder = 3;

    /// <param name="x">Strictly increasing sample positions (seconds).</param>
    /// <param name="y">Values to smooth (RPM).</param>
    /// <param name="lambdaMultiplier">1 = automatic. Above 1 smooths more, below 1 smooths less.</param>
    public static Fit FitAuto(IReadOnlyList<double> x, IReadOnlyList<double> y, double lambdaMultiplier = 1.0)
    {
        int n = x.Count;
        if (n != y.Count) throw new ArgumentException("x and y must be the same length.");
        if (n < 8) throw new ArgumentException("At least 8 samples are needed to fit a curve.");
        for (int i = 1; i < n; i++)
        {
            if (!(x[i] > x[i - 1])) throw new ArgumentException("x must be strictly increasing.");
        }

        int segments = Math.Clamp(n / 4, 10, 40);
        int nb = segments + 3;
        double lo = x[0], hi = x[n - 1];
        double dx = (hi - lo) / segments;

        // Each sample touches exactly four basis functions; store them sparsely.
        var firstIndex = new int[n];
        var basis = new double[n, 4];
        var basisDeriv = new double[n, 4];
        for (int i = 0; i < n; i++)
        {
            double pos = (x[i] - lo) / dx;
            int seg = Math.Clamp((int)Math.Floor(pos), 0, segments - 1);
            double u = pos - seg;
            firstIndex[i] = seg;
            double um = 1 - u;
            basis[i, 0] = um * um * um / 6.0;
            basis[i, 1] = (3 * u * u * u - 6 * u * u + 4) / 6.0;
            basis[i, 2] = (-3 * u * u * u + 3 * u * u + 3 * u + 1) / 6.0;
            basis[i, 3] = u * u * u / 6.0;
            basisDeriv[i, 0] = -um * um / 2.0 / dx;
            basisDeriv[i, 1] = (3 * u * u - 4 * u) / 2.0 / dx;
            basisDeriv[i, 2] = (-3 * u * u + 2 * u + 1) / 2.0 / dx;
            basisDeriv[i, 3] = u * u / 2.0 / dx;
        }

        // Normal equations: BᵀB and Bᵀy.
        var btb = new double[nb, nb];
        var bty = new double[nb];
        for (int i = 0; i < n; i++)
        {
            int f = firstIndex[i];
            for (int a = 0; a < 4; a++)
            {
                bty[f + a] += basis[i, a] * y[i];
                for (int b = 0; b < 4; b++) btb[f + a, f + b] += basis[i, a] * basis[i, b];
            }
        }

        double[,] penalty = DifferencePenalty(nb, PenaltyOrder);

        double bestScore = double.PositiveInfinity, bestLambda = double.NaN;
        for (double exponent = -6; exponent <= 10.0001; exponent += 0.25)
        {
            double lambda = Math.Pow(10, exponent);
            var score = RemlScore(btb, bty, penalty, lambda, x, y, firstIndex, basis, out _);
            if (score < bestScore)
            {
                bestScore = score;
                bestLambda = lambda;
            }
        }

        if (double.IsNaN(bestLambda)) throw new InvalidOperationException("Could not fit a smoothing curve to this pull.");

        double chosen = bestLambda * Math.Max(lambdaMultiplier, 1e-6);
        RemlScore(btb, bty, penalty, chosen, x, y, firstIndex, basis, out var solution);

        var values = new double[n];
        var derivative = new double[n];
        for (int i = 0; i < n; i++)
        {
            int f = firstIndex[i];
            double v = 0, d = 0;
            for (int a = 0; a < 4; a++)
            {
                v += basis[i, a] * solution.Coefficients[f + a];
                d += basisDeriv[i, a] * solution.Coefficients[f + a];
            }
            values[i] = v;
            derivative[i] = d;
        }

        return new Fit(values, derivative, chosen, solution.Trace)
        {
            Coefficients = solution.Coefficients,
            Start = lo,
            SegmentWidth = dx,
            Segments = segments,
        };
    }

    private readonly record struct Solution(double[] Coefficients, double Trace);

    /// <summary>
    /// −2 × restricted log-likelihood with the noise variance profiled out (up to a constant):
    /// (n − k)·log(RSS + λ·cᵀPc) + log|BᵀB + λP| − rank(P)·log λ, where k is the penalty order
    /// (the size of the polynomial null space the penalty leaves alone).
    /// </summary>
    private static double RemlScore(double[,] btb, double[] bty, double[,] penalty, double lambda,
        IReadOnlyList<double> x, IReadOnlyList<double> y, int[] firstIndex, double[,] basis, out Solution solution)
    {
        int nb = bty.Length;
        int n = x.Count;
        var m = new double[nb, nb];
        for (int r = 0; r < nb; r++)
            for (int c = 0; c < nb; c++)
                m[r, c] = btb[r, c] + lambda * penalty[r, c];

        if (!Cholesky.TryDecompose(m, out var chol))
        {
            solution = new Solution(new double[nb], double.NaN);
            return double.PositiveInfinity;
        }

        double[] coef = Cholesky.Solve(chol, bty);

        // Effective degrees of freedom: trace((BᵀB + λP)⁻¹ BᵀB).
        double trace = 0;
        var column = new double[nb];
        for (int c = 0; c < nb; c++)
        {
            for (int r = 0; r < nb; r++) column[r] = btb[r, c];
            double[] solved = Cholesky.Solve(chol, column);
            trace += solved[c];
        }

        double rss = 0;
        for (int i = 0; i < n; i++)
        {
            int f = firstIndex[i];
            double fit = 0;
            for (int a = 0; a < 4; a++) fit += basis[i, a] * coef[f + a];
            double e = y[i] - fit;
            rss += e * e;
        }

        double roughness = 0;
        for (int r = 0; r < nb; r++)
            for (int c = 0; c < nb; c++)
                roughness += coef[r] * penalty[r, c] * coef[c];

        double logDet = 0;
        for (int i = 0; i < nb; i++) logDet += 2 * Math.Log(chol[i, i]);

        solution = new Solution(coef, trace);
        double total = rss + lambda * roughness;
        if (total <= 0 || n <= PenaltyOrder) return double.PositiveInfinity;
        int rank = nb - PenaltyOrder;
        return (n - PenaltyOrder) * Math.Log(total) + logDet - rank * Math.Log(lambda);
    }

    /// <summary>DᵀD for the k-th order difference matrix D.</summary>
    internal static double[,] DifferencePenalty(int size, int order)
    {
        // Coefficients of the k-th forward difference: (-1)^(k-j) C(k, j).
        var stencil = new double[order + 1];
        for (int j = 0; j <= order; j++)
        {
            stencil[j] = Binomial(order, j) * ((order - j) % 2 == 0 ? 1 : -1);
        }

        var p = new double[size, size];
        for (int row = 0; row + order < size; row++)
        {
            for (int a = 0; a <= order; a++)
                for (int b = 0; b <= order; b++)
                    p[row + a, row + b] += stencil[a] * stencil[b];
        }
        return p;
    }

    private static double Binomial(int n, int k)
    {
        double r = 1;
        for (int i = 1; i <= k; i++) r = r * (n - k + i) / i;
        return r;
    }
}
