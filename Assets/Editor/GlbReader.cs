using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DevCity.EditorTools
{
    /// <summary>
    /// Leitor de arquivos .glb (glTF binário) usado pelas ferramentas de motos e armas.
    /// Um .glb é um JSON (a "receita": peças, materiais, cores) seguido de um bloco de bytes
    /// (as posições dos vértices, normais, coordenadas de textura e imagens).
    /// Lê só malhas estáticas (sem esqueleto/animação) — para personagens animados use o glTFast.
    /// </summary>
    public static class Glb
    {
        // ===================== 1) leitura do .glb =====================

        public class Prim { public List<Vector3> pos = new List<Vector3>(); public List<Vector3> nrm; public List<Vector2> uv; public List<int> tri = new List<int>(); public int material; }

        public static List<Prim> ReadGlb(string path, out JObject json, out byte[] bin)
        {
            var bytes = File.ReadAllBytes(path);
            if (BitConverter.ToUInt32(bytes, 0) != 0x46546C67) throw new Exception("não é um arquivo .glb");
            int jsonLen = BitConverter.ToInt32(bytes, 12);
            json = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLen));
            int binStart = 20 + jsonLen;
            int binLen = BitConverter.ToInt32(bytes, binStart);
            bin = new byte[binLen];
            Buffer.BlockCopy(bytes, binStart + 8, bin, 0, binLen);

            var prims = new List<Prim>();
            var scene = json["scenes"]?[json.Value<int?>("scene") ?? 0];
            foreach (var n in scene["nodes"]) Walk(json, bin, n.Value<int>(), Matrix4x4.identity, prims);
            return prims;
        }

        public static void Walk(JObject j, byte[] bin, int nodeIndex, Matrix4x4 parent, List<Prim> prims)
        {
            var node = (JObject)j["nodes"][nodeIndex];
            Matrix4x4 local = Matrix4x4.identity;
            if (node["matrix"] is JArray m)
            {
                for (int i = 0; i < 16; i++) local[i % 4, i / 4] = m[i].Value<float>(); // glTF guarda por colunas
            }
            else
            {
                var t = node["translation"] is JArray ta ? new Vector3(ta[0].Value<float>(), ta[1].Value<float>(), ta[2].Value<float>()) : Vector3.zero;
                var r = node["rotation"] is JArray ra ? new Quaternion(ra[0].Value<float>(), ra[1].Value<float>(), ra[2].Value<float>(), ra[3].Value<float>()) : Quaternion.identity;
                var s = node["scale"] is JArray sa ? new Vector3(sa[0].Value<float>(), sa[1].Value<float>(), sa[2].Value<float>()) : Vector3.one;
                local = Matrix4x4.TRS(t, r, s);
            }
            var world = parent * local;

            if (node["mesh"] != null)
            {
                foreach (JObject p in j["meshes"][node.Value<int>("mesh")]["primitives"])
                {
                    if ((p.Value<int?>("mode") ?? 4) != 4) continue; // só triângulos
                    var attr = (JObject)p["attributes"];
                    var prim = new Prim { material = p.Value<int?>("material") ?? -1 };
                    foreach (var v in ReadFloats(j, bin, attr.Value<int>("POSITION"), 3))
                    {
                        var w = world.MultiplyPoint3x4(new Vector3(v[0], v[1], v[2]));
                        prim.pos.Add(new Vector3(-w.x, w.y, w.z)); // glTF é "mão direita": inverte o X
                    }
                    if (attr["NORMAL"] != null)
                        prim.nrm = ReadFloats(j, bin, attr.Value<int>("NORMAL"), 3).Select(v => { var w = world.MultiplyVector(new Vector3(v[0], v[1], v[2])).normalized; return new Vector3(-w.x, w.y, w.z); }).ToList();
                    if (attr["TEXCOORD_0"] != null)
                        prim.uv = ReadFloats(j, bin, attr.Value<int>("TEXCOORD_0"), 2).Select(v => new Vector2(v[0], 1f - v[1])).ToList();
                    var idx = p["indices"] != null ? ReadInts(j, bin, p.Value<int>("indices")) : Enumerable.Range(0, prim.pos.Count).ToList();
                    for (int i = 0; i + 2 < idx.Count; i += 3) { prim.tri.Add(idx[i]); prim.tri.Add(idx[i + 2]); prim.tri.Add(idx[i + 1]); } // inverte a ordem (por causa do X)
                    prims.Add(prim);
                }
            }
            if (node["children"] is JArray ch) foreach (var c in ch) Walk(j, bin, c.Value<int>(), world, prims);
        }

        public static (int offset, int stride, int count, int type, bool norm) View(JObject j, int accessor, int comps)
        {
            var a = j["accessors"][accessor];
            var bv = j["bufferViews"][a.Value<int>("bufferView")];
            int type = a.Value<int>("componentType");
            int size = type == 5126 || type == 5125 ? 4 : type == 5123 || type == 5122 ? 2 : 1;
            int offset = (bv.Value<int?>("byteOffset") ?? 0) + (a.Value<int?>("byteOffset") ?? 0);
            int stride = bv.Value<int?>("byteStride") ?? size * comps;
            return (offset, stride, a.Value<int>("count"), type, a.Value<bool?>("normalized") ?? false);
        }

        public static List<float[]> ReadFloats(JObject j, byte[] bin, int accessor, int comps)
        {
            var (offset, stride, count, type, norm) = View(j, accessor, comps);
            var list = new List<float[]>(count);
            for (int i = 0; i < count; i++)
            {
                var v = new float[comps];
                int o = offset + i * stride;
                for (int c = 0; c < comps; c++)
                {
                    switch (type)
                    {
                        case 5126: v[c] = BitConverter.ToSingle(bin, o + c * 4); break;
                        case 5123: v[c] = BitConverter.ToUInt16(bin, o + c * 2) / (norm ? 65535f : 1f); break;
                        case 5121: v[c] = bin[o + c] / (norm ? 255f : 1f); break;
                        default: throw new Exception("tipo de número não suportado: " + type);
                    }
                }
                list.Add(v);
            }
            return list;
        }

        public static List<int> ReadInts(JObject j, byte[] bin, int accessor)
        {
            var (offset, stride, count, type, _) = View(j, accessor, 1);
            var list = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int o = offset + i * stride;
                list.Add(type == 5125 ? (int)BitConverter.ToUInt32(bin, o) : type == 5123 ? BitConverter.ToUInt16(bin, o) : bin[o]);
            }
            return list;
        }


        /// <summary>Um triângulo já "achatado" (posições, normais, uv e material).</summary>
        public struct Tri { public Vector3 a, b, c, na, nb, nc; public Vector2 ua, ub, uc; public int mat; }

        /// <summary>Lê o .glb e devolve todos os triângulos numa lista só (já no espaço do Unity).</summary>
        public static List<Tri> ReadTris(string path, out JObject json, out byte[] bin)
        {
            var tris = new List<Tri>();
            foreach (var p in ReadGlb(path, out json, out bin))
                for (int i = 0; i < p.tri.Count; i += 3)
                {
                    int a = p.tri[i], b = p.tri[i + 1], c = p.tri[i + 2];
                    var t = new Tri { a = p.pos[a], b = p.pos[b], c = p.pos[c], mat = p.material };
                    Vector3 fn = Vector3.Cross(t.b - t.a, t.c - t.a).normalized;
                    t.na = p.nrm != null ? p.nrm[a] : fn; t.nb = p.nrm != null ? p.nrm[b] : fn; t.nc = p.nrm != null ? p.nrm[c] : fn;
                    if (p.uv != null) { t.ua = p.uv[a]; t.ub = p.uv[b]; t.uc = p.uv[c]; }
                    tris.Add(t);
                }
            return tris;
        }

        public static GameObject MakePart(Transform parent, string partName, Vector3 pivot, List<Tri> tris, Func<int, bool> pick, Material[] mats, string dir)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var bySub = new Dictionary<int, List<int>>();
            for (int i = 0; i < tris.Count; i++)
            {
                if (!pick(i)) continue;
                var t = tris[i];
                if (!bySub.TryGetValue(t.mat, out var list)) bySub[t.mat] = list = new List<int>();
                int n = verts.Count;
                verts.Add(t.a - pivot); verts.Add(t.b - pivot); verts.Add(t.c - pivot);
                norms.Add(t.na); norms.Add(t.nb); norms.Add(t.nc);
                uvs.Add(t.ua); uvs.Add(t.ub); uvs.Add(t.uc);
                list.Add(n); list.Add(n + 1); list.Add(n + 2);
            }
            var go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            if (verts.Count == 0) return go;

            var mesh = new Mesh { name = partName, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            var keys = bySub.Keys.OrderBy(k => k).ToList();
            mesh.subMeshCount = keys.Count;
            for (int k = 0; k < keys.Count; k++) mesh.SetTriangles(bySub[keys[k]], k);
            mesh.RecalculateBounds();
            string meshPath = $"{dir}/{partName}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = keys.Select(k => k >= 0 && k < mats.Length ? mats[k] : mats.Last()).ToArray();
            return go;
        }

        /// <summary>Cria um material Standard para cada material do .glb (cor base e textura).</summary>
        public static Material[] BuildMaterials(JObject j, byte[] bin, string dir)
        {
            var list = new List<Material>();
            var jm = j["materials"] as JArray ?? new JArray();
            var texCache = new Dictionary<int, Texture2D>();
            for (int i = 0; i <= jm.Count; i++) // +1 = material padrão (para primitivas sem material)
            {
                var m = new Material(Shader.Find("Standard"));
                m.SetFloat("_Glossiness", 0.35f);
                if (i < jm.Count)
                {
                    var pbr = jm[i]["pbrMetallicRoughness"];
                    if (pbr?["baseColorFactor"] is JArray cf)
                        m.color = new Color(cf[0].Value<float>(), cf[1].Value<float>(), cf[2].Value<float>(), 1f).gamma; // glTF guarda a cor "linear"
                    if (pbr?["baseColorTexture"] != null)
                    {
                        int texIndex = pbr["baseColorTexture"].Value<int>("index");
                        if (!texCache.TryGetValue(texIndex, out var tex)) texCache[texIndex] = tex = SaveTexture(j, bin, texIndex, dir);
                        m.mainTexture = tex;
                    }
                    m.SetFloat("_Metallic", Mathf.Clamp01(pbr?.Value<float?>("metallicFactor") ?? 0f) * 0.5f);
                }
                string matPath = $"{dir}/material_{i}.mat";
                AssetDatabase.DeleteAsset(matPath);
                AssetDatabase.CreateAsset(m, matPath);
                list.Add(m);
            }
            return list.ToArray();
        }

        public static Texture2D SaveTexture(JObject j, byte[] bin, int texIndex, string dir)
        {
            var img = j["images"][j["textures"][texIndex].Value<int>("source")];
            var bv = j["bufferViews"][img.Value<int>("bufferView")];
            var data = new byte[bv.Value<int>("byteLength")];
            Buffer.BlockCopy(bin, bv.Value<int?>("byteOffset") ?? 0, data, 0, data.Length);
            string ext = (img.Value<string>("mimeType") ?? "image/png").Contains("jpeg") ? "jpg" : "png";
            string texPath = $"{dir}/textura_{texIndex}.{ext}";
            File.WriteAllBytes(texPath, data);
            AssetDatabase.ImportAsset(texPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }

        // ===================== ajudantes =====================

        public static Bounds Bounds(IEnumerable<Vector3> pts)
        {
            bool first = true;
            var b = new Bounds();
            foreach (var p in pts) { if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
            return b;
        }

        /// <summary>Desloca e escala só as posições (as normais não mudam).</summary>
        public static void MoveAndScale(List<Tri> tris, Vector3 offset, float scale)
        {
            for (int i = 0; i < tris.Count; i++)
            {
                var t = tris[i];
                t.a = (t.a + offset) * scale; t.b = (t.b + offset) * scale; t.c = (t.c + offset) * scale;
                tris[i] = t;
            }
        }

        public static void Transform(List<Tri> tris, Func<Vector3, Vector3> f)
        {
            for (int i = 0; i < tris.Count; i++)
            {
                var t = tris[i];
                t.a = f(t.a); t.b = f(t.b); t.c = f(t.c);
                t.na = f(t.na); t.nb = f(t.nb); t.nc = f(t.nc);
                tris[i] = t;
            }
        }

        /// <summary>Agrupa os triângulos em ilhas (union-find pelos vértices de mesma posição).</summary>
        public static List<List<int>> Islands(List<Tri> tris, float weld)
        {
            var parent = Enumerable.Range(0, tris.Count).ToArray();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            var owner = new Dictionary<Vector3Int, int>();
            Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x / weld), Mathf.RoundToInt(v.y / weld), Mathf.RoundToInt(v.z / weld));
            for (int i = 0; i < tris.Count; i++)
                foreach (var v in new[] { tris[i].a, tris[i].b, tris[i].c })
                {
                    var k = Key(v);
                    if (owner.TryGetValue(k, out var o)) parent[Find(i)] = Find(o);
                    else owner[k] = i;
                }
            return Enumerable.Range(0, tris.Count).GroupBy(Find).Select(g => g.ToList()).ToList();
        }
    }
}
