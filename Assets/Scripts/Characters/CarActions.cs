using UnityEngine;

namespace DevCity
{
    /// <summary>
    /// Quanto o corpo anda e gira nas animações da Mixamo "Entering Car" e "Exiting Car".
    /// Os números são medidos pelo MixamoImporter e gravados em Resources/Personagens/AcoesCarro.json.
    ///
    /// Por que precisa disso: a animação move o corpo (anda até o banco e senta), mas o objeto do
    /// jogador fica parado. Sabendo onde o corpo termina, colocamos o jogador no ponto certo da porta
    /// para que, no fim da animação, ele esteja exatamente em cima do banco.
    /// </summary>
    public static class CarActions
    {
        [System.Serializable]
        public class Action
        {
            public float dx, dz;     // deslocamento do corpo do começo ao fim (metros "humanos", olhando para +Z)
            public float y0, y1;     // altura do corpo no começo e no fim
            public float yaw0, yaw1; // direção do corpo no começo e no fim (graus)
            public float len;        // duração (s)

            /// <summary>Quanto o corpo gira até o fim (-95 = vira 95° para a esquerda).</summary>
            public float Turn => Mathf.DeltaAngle(yaw0, yaw1);
            public Vector3 Move => new Vector3(dx, 0f, dz);
        }

        [System.Serializable]
        class File { public Action entrar, sair; }

        static File data;
        static bool loaded;

        public static Action Enter { get { Load(); return data?.entrar; } }
        public static Action Exit { get { Load(); return data?.sair; } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var txt = Resources.Load<TextAsset>("Personagens/AcoesCarro");
            if (txt == null) return;
            data = JsonUtility.FromJson<File>(txt.text);
            if (data.entrar != null && data.entrar.len <= 0f) data.entrar = null;
            if (data.sair != null && data.sair.len <= 0f) data.sair = null;
        }
    }
}
