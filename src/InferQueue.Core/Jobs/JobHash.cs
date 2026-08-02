using System.Security.Cryptography;
using System.Text;

namespace InferQueue.Core.Jobs;

/// <summary>
/// Calcula a chave de deduplicacao de um job.
/// O modelo entra no hash porque o mesmo texto em modelos diferentes e trabalho diferente.
/// </summary>
public static class JobHash
{
    public static string Compute(string inputText, string model)
    {
        // O \n separa os campos para que ("ab", "c") e ("a", "bc") nao colidam.
        var bytes = Encoding.UTF8.GetBytes($"{model}\n{inputText}");
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
