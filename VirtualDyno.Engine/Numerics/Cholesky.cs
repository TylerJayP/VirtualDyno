namespace VirtualDyno.Engine.Numerics;

/// <summary>Small dense Cholesky solver for symmetric positive-definite systems.</summary>
internal static class Cholesky
{
    /// <summary>Factor A = L·Lᵀ. Returns false if A is not positive definite.</summary>
    public static bool TryDecompose(double[,] a, out double[,] lower)
    {
        int n = a.GetLength(0);
        lower = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = a[i, j];
                for (int k = 0; k < j; k++) sum -= lower[i, k] * lower[j, k];
                if (i == j)
                {
                    if (sum <= 0 || double.IsNaN(sum)) return false;
                    lower[i, i] = Math.Sqrt(sum);
                }
                else
                {
                    lower[i, j] = sum / lower[j, j];
                }
            }
        }
        return true;
    }

    /// <summary>Solve L·Lᵀ·x = b.</summary>
    public static double[] Solve(double[,] lower, double[] b)
    {
        int n = b.Length;
        var y = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = b[i];
            for (int k = 0; k < i; k++) sum -= lower[i, k] * y[k];
            y[i] = sum / lower[i, i];
        }
        var x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = y[i];
            for (int k = i + 1; k < n; k++) sum -= lower[k, i] * x[k];
            x[i] = sum / lower[i, i];
        }
        return x;
    }
}
