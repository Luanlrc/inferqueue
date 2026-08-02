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
        // O tamanho do modelo vai na frente para que os campos nao possam se confundir.
        // Um separador simples nao bastaria: com "a\nb" + "c" e "a" + "b\nc" a concatenacao
        // daria o mesmo texto, e dois pedidos diferentes teriam o mesmo hash.
        var bytes = Encoding.UTF8.GetBytes($"{model.Length}:{model}:{inputText}");
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
