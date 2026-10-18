using System.Linq;
using UnityEngine;

namespace DevCity.EditorTools
{
    /// <summary>Mostra no Console o tamanho de cada modelo dos City Kits (para escolher a escala).</summary>
    public static class CityKitInfo
    {
        [UnityEditor.MenuItem("Dev City/Medir modelos da cidade", false, 32)]
        public static void Run()
        {
            foreach (var kit in new[] { "Comercial", "Suburbio", "Industrial", "Ruas" })
                foreach (var m in KitModels.All(kit))
                    Debug.Log(System.FormattableString.Invariant($"[Cidade] {kit}/{m.Name}: {m.Bounds.size.x:0.00} x {m.Bounds.size.y:0.00} (altura) x {m.Bounds.size.z:0.00} | centro {m.Bounds.center}"));
        }
    }
}
