using UnityEditor;

namespace DevCity.EditorTools
{
    /// <summary>
    /// Configura sozinho tudo que entra em Assets/Resources/Veiculos:
    /// - modelos com "Read/Write" ligado (o jogo lê as coordenadas de textura da lataria);
    /// - a textura colormap legível (o jogo cria versões com a lataria de outra cor).
    /// AssetPostprocessor = código que roda toda vez que o Unity importa um arquivo.
    /// </summary>
    public class VehicleImportSettings : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Veiculos";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var imp = (ModelImporter)assetImporter;
            imp.isReadable = true;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var imp = (TextureImporter)assetImporter;
            imp.isReadable = true;
        }

        public override uint GetVersion() => 1;
    }
}
