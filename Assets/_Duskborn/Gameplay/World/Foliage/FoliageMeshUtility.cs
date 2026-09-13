using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.World.Foliage
{
    /// <summary>
    /// Utilitário procedural para geração de malhas low-poly de folhagem estilizada
    /// (tufo de grama com lâminas curvadas, arbustos em esferas agrupadas com normais esféricas anime,
    /// e copas de árvores estilizadas com vertex colors e pesos de vento).
    /// </summary>
    public static class FoliageMeshUtility
    {
        /// <summary>
        /// Gera uma malha de tufo de grama estilizado com múltiplas lâminas curvadas.
        /// Base (UV.y=0, Alpha=0) não sofre vento.
        /// Ponta (UV.y=1, Alpha=1) oscila suavemente com o vento.
        /// </summary>
        public static Mesh CreateGrassTuftMesh(int bladeCount = 5, float height = 0.95f, float baseWidth = 0.18f, float tipWidth = 0.02f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_GrassTuft_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            float angleStep = 360f / bladeCount;

            for (int i = 0; i < bladeCount; i++)
            {
                float baseAngle = i * angleStep + Random.Range(-12f, 12f);
                float rad = baseAngle * Mathf.Deg2Rad;
                Vector3 bladeDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 bladeSide = new Vector3(-bladeDir.z, 0f, bladeDir.x);

                float bladeHeight = height * Random.Range(0.85f, 1.18f);
                float bladeLean = Random.Range(0.18f, 0.38f);
                Vector3 curveOffset = bladeDir * bladeLean;

                int baseIndex = verts.Count;

                // Segmento 0: Base no solo (sem vento)
                Vector3 v0_left = -bladeSide * (baseWidth * 0.5f);
                Vector3 v0_right = bladeSide * (baseWidth * 0.5f);

                // Segmento 1: Meio da lâmina com curvatura
                float midY = bladeHeight * 0.52f;
                Vector3 v1_left = -bladeSide * (baseWidth * 0.38f) + Vector3.up * midY + curveOffset * 0.45f;
                Vector3 v1_right = bladeSide * (baseWidth * 0.38f) + Vector3.up * midY + curveOffset * 0.45f;

                // Segmento 2: Ponta da lâmina
                Vector3 v2_tip = Vector3.up * bladeHeight + curveOffset;

                // Normais apontando para cima/fora para iluminação uniforme
                Vector3 nBase = (Vector3.up * 0.6f + bladeDir * 0.4f).normalized;
                Vector3 nMid = (Vector3.up * 0.7f + bladeDir * 0.5f).normalized;
                Vector3 nTip = Vector3.up;

                // Adiciona vértices
                verts.Add(v0_left);  // 0
                verts.Add(v0_right); // 1
                verts.Add(v1_left);  // 2
                verts.Add(v1_right); // 3
                verts.Add(v2_tip);   // 4

                normals.Add(nBase);
                normals.Add(nBase);
                normals.Add(nMid);
                normals.Add(nMid);
                normals.Add(nTip);

                // UVs: x = horizontal (0 a 1), y = altura vertical (0 no solo a 1 no topo)
                uvs.Add(new Vector2(0.0f, 0.0f));
                uvs.Add(new Vector2(1.0f, 0.0f));
                uvs.Add(new Vector2(0.15f, 0.52f));
                uvs.Add(new Vector2(0.85f, 0.52f));
                uvs.Add(new Vector2(0.5f, 1.0f));

                // Cores de vértice: RGB = branco (multiplicado pelo shader), Alpha = peso de deslocamento do vento
                colors.Add(new Color(1f, 1f, 1f, 0.0f)); // Solo estático
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
                colors.Add(new Color(1f, 1f, 1f, 0.45f)); // Meio
                colors.Add(new Color(1f, 1f, 1f, 0.45f));
                colors.Add(new Color(1f, 1f, 1f, 1.0f));  // Ponta com balanço total

                // Triângulos (Front e Back para garantir renderização perfeita)
                // Quad inferior (0, 2, 1) e (1, 2, 3)
                tris.Add(baseIndex + 0);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);

                // Triângulo superior até a ponta (2, 4, 3)
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 3);

                // Verso dos triângulos (Backface)
                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 0);

                tris.Add(baseIndex + 3);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 3);
                tris.Add(baseIndex + 4);
                tris.Add(baseIndex + 2);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de arbusto low-poly composta por agrupamentos esféricos (Puffs)
        /// com normais esféricas anime voltadas para fora do centro do tufo, produzindo sombreamento suave e volumoso.
        /// </summary>
        public static Mesh CreateBushMesh(int lobes = 4, float baseRadius = 0.65f, float height = 1.0f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_Bush_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            // Centros dos agrupamentos/lobos
            Vector3[] lobeCenters = new Vector3[lobes];
            float[] lobeRadii = new float[lobes];

            lobeCenters[0] = new Vector3(0f, height * 0.48f, 0f);
            lobeRadii[0] = baseRadius * 0.95f;

            for (int l = 1; l < lobes; l++)
            {
                float angle = (l - 1) * (360f / (lobes - 1)) + 25f;
                float rad = angle * Mathf.Deg2Rad;
                float dist = baseRadius * 0.45f;
                lobeCenters[l] = new Vector3(Mathf.Cos(rad) * dist, height * 0.40f + (l % 2 == 0 ? 0.12f : -0.05f), Mathf.Sin(rad) * dist);
                lobeRadii[l] = baseRadius * Random.Range(0.68f, 0.85f);
            }

            // Gera geometria icosaédrica subdividida leve para cada lobo
            for (int l = 0; l < lobes; l++)
            {
                Vector3 center = lobeCenters[l];
                float radius = lobeRadii[l];
                int startV = verts.Count;

                // Esfera low-poly simplificada (lat/long com 6 segmentos e 5 anéis)
                int rings = 4;
                int segments = 6;

                for (int r = 0; r <= rings; r++)
                {
                    float v = (float)r / rings;
                    float phi = v * Mathf.PI; // 0 a PI

                    for (int s = 0; s <= segments; s++)
                    {
                        float u = (float)s / segments;
                        float theta = u * Mathf.PI * 2.0f;

                        Vector3 spherePos = new Vector3(
                            Mathf.Sin(phi) * Mathf.Cos(theta),
                            Mathf.Cos(phi),
                            Mathf.Sin(phi) * Mathf.Sin(theta)
                        );

                        Vector3 vertPos = center + spherePos * radius;
                        // Achata a base do arbusto em contato com o solo
                        if (vertPos.y < 0.05f) vertPos.y = 0.02f;

                        // Técnica Anime Ghibli: Normal radial a partir do centro do lobo ou centro do arbusto
                        Vector3 animeNormal = (vertPos - new Vector3(0f, height * 0.35f, 0f)).normalized;

                        verts.Add(vertPos);
                        normals.Add(animeNormal);

                        float vertH = Mathf.Clamp01(vertPos.y / height);
                        uvs.Add(new Vector2(u, vertH));

                        // Base estática, topo e laterais oscilando
                        float windStrength = Mathf.Clamp01((vertPos.y - 0.12f) / height) * 0.65f;
                        colors.Add(new Color(1f, 1f, 1f, windStrength));
                    }
                }

                int stride = segments + 1;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int current = startV + r * stride + s;
                        int next = current + stride;

                        tris.Add(current);
                        tris.Add(next);
                        tris.Add(next + 1);

                        tris.Add(current);
                        tris.Add(next + 1);
                        tris.Add(current + 1);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Gera uma malha de copa de árvore estilizada com lóbulos volumosos
        /// e gradiente vertical de vento e cores.
        /// </summary>
        public static Mesh CreateTreeCanopyMesh(float radius = 2.2f, float height = 3.5f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Mesh_TreeCanopy_LowPoly";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            Vector3[] lobes = new Vector3[]
            {
                new Vector3(0f, height * 0.62f, 0f),
                new Vector3(radius * 0.42f, height * 0.45f, radius * 0.25f),
                new Vector3(-radius * 0.38f, height * 0.48f, radius * 0.35f),
                new Vector3(0.08f, height * 0.42f, -radius * 0.45f),
                new Vector3(-radius * 0.25f, height * 0.72f, -radius * 0.20f)
            };

            float[] lobeRadii = new float[]
            {
                radius * 0.82f,
                radius * 0.65f,
                radius * 0.62f,
                radius * 0.68f,
                radius * 0.58f
            };

            for (int l = 0; l < lobes.Length; l++)
            {
                Vector3 center = lobes[l];
                float rLobe = lobeRadii[l];
                int startV = verts.Count;

                int rings = 5;
                int segments = 7;

                for (int r = 0; r <= rings; r++)
                {
                    float v = (float)r / rings;
                    float phi = v * Mathf.PI;

                    for (int s = 0; s <= segments; s++)
                    {
                        float u = (float)s / segments;
                        float theta = u * Mathf.PI * 2.0f;

                        Vector3 spherePos = new Vector3(
                            Mathf.Sin(phi) * Mathf.Cos(theta),
                            Mathf.Cos(phi) * 0.88f, // Ligeiramente achatado no topo
                            Mathf.Sin(phi) * Mathf.Sin(theta)
                        );

                        Vector3 vertPos = center + spherePos * rLobe;
                        Vector3 animeNormal = (vertPos - new Vector3(0f, height * 0.5f, 0f)).normalized;

                        verts.Add(vertPos);
                        normals.Add(animeNormal);

                        float vertH = Mathf.Clamp01(vertPos.y / height);
                        uvs.Add(new Vector2(u, vertH));

                        float windWeight = Mathf.Clamp01(vertH * 0.85f);
                        colors.Add(new Color(1f, 1f, 1f, windWeight));
                    }
                }

                int stride = segments + 1;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int current = startV + r * stride + s;
                        int next = current + stride;

                        tris.Add(current);
                        tris.Add(next);
                        tris.Add(next + 1);

                        tris.Add(current);
                        tris.Add(next + 1);
                        tris.Add(current + 1);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
